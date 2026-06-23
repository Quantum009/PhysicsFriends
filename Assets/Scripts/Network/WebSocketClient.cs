// ============================================================
// WebSocketClient.cs — WebSocket 客户端封装
// 无外部依赖版本：
//   Desktop/Editor → System.Net.WebSockets.ClientWebSocket
//   WebGL          → JavaScript interop (WebSocket.jslib)
// ============================================================
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using UnityEngine;

#if !UNITY_WEBGL || UNITY_EDITOR
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#else
using System.Runtime.InteropServices;
#endif

namespace PhysicsFriends.Network
{
    public class WebSocketClient : MonoBehaviour
    {
        public static WebSocketClient Instance { get; private set; }

        [Header("服务器配置")]
        // 空字符串 = WebGL 自动跟随当前页面域名；非 WebGL 回退到 localhost
        [SerializeField] private string serverUrl = "";
        [SerializeField] private bool autoReconnect = true;
        [SerializeField] private int maxReconnectAttempts = 6;
        [SerializeField] private float reconnectIntervalSeconds = 2f;

        // ---- 状态 ----
        public bool IsConnected { get; private set; }
        public int LocalPlayerIndex { get; private set; } = -1;
        public string RoomCode { get; private set; }
        public bool IsHost => LocalPlayerIndex == 0;
        public bool IsReconnecting { get; private set; }

        // ---- 事件 ----
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<int, int> OnReconnectAttempt;
        public event Action OnReconnected;
        public event Action<string> OnError;
        public event Action<string, string> OnRoomCreated;
        public event Action<int, string> OnRoomJoined;
        public event Action<int, string> OnPlayerJoined;
        public event Action<int> OnPlayerDisconnected;
        public event Action OnHostDisconnected;
        public event Action OnGameStarted;
        public event Action<int, string> OnClientMessage;
        public event Action<string> OnHostMessage;

        // 线程安全的消息队列（后台线程 → 主线程）
        private readonly ConcurrentQueue<string> _incomingMessages = new();
        private readonly Queue<string> _pendingOutgoing = new();
        private bool _intentionalDisconnect;
        private bool _wasConnectedOnce;
        private bool _reconnectJoinSent;
        private string _lastPlayerName = "";
        private Coroutine _reconnectRoutine;
        private int _reconnectAttemptCount;

        // ================================================================
        // 平台相关实现
        // ================================================================

#if !UNITY_WEBGL || UNITY_EDITOR
        // ---- Desktop / Editor: System.Net.WebSockets ----
        private ClientWebSocket _ws;
        private CancellationTokenSource _cts;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            while (_incomingMessages.TryDequeue(out string json))
            {
                HandleMessage(json);
            }
        }

        public async void Connect(string url = null)
        {
            serverUrl = ResolveServerUrl(url);
            _intentionalDisconnect = false;

            try
            {
                _cts?.Cancel();
                if (_ws != null) { _ws.Dispose(); _ws = null; }

                _cts = new CancellationTokenSource();
                _ws = new ClientWebSocket();

                Debug.Log($"[WS] 正在连接 {serverUrl}...");
                await _ws.ConnectAsync(new Uri(serverUrl), _cts.Token);

                IsConnected = true;
                _wasConnectedOnce = true;
                Debug.Log("[WS] 已连接");
                OnConnected?.Invoke();

                if (IsReconnecting && !_reconnectJoinSent)
                    TryRejoinRoomAfterReconnect();

                // 发送积压消息
                while (_pendingOutgoing.Count > 0)
                    await SendAsync(_pendingOutgoing.Dequeue());

                // 启动接收循环
                _ = ReceiveLoop(_cts.Token);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WS] 连接失败: {e.Message}");
                IsConnected = false;
                OnError?.Invoke(e.Message);
            }
        }

        private async Task ReceiveLoop(CancellationToken ct)
        {
            var buffer = new byte[8192];
            var msgBuffer = new List<byte>();

            try
            {
                while (_ws != null && _ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var result = await _ws.ReceiveAsync(
                        new ArraySegment<byte>(buffer), ct);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        IsConnected = false;
                        _incomingMessages.Enqueue("__DISCONNECTED__");
                        break;
                    }

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        msgBuffer.AddRange(new ArraySegment<byte>(buffer, 0, result.Count));

                        if (result.EndOfMessage)
                        {
                            string json = Encoding.UTF8.GetString(msgBuffer.ToArray());
                            msgBuffer.Clear();
                            _incomingMessages.Enqueue(json);
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (WebSocketException e)
            {
                Debug.LogError($"[WS] 接收错误: {e.Message}");
                IsConnected = false;
                _incomingMessages.Enqueue("__DISCONNECTED__");
            }
            catch (Exception e)
            {
                Debug.LogError($"[WS] 意外错误: {e.Message}");
                IsConnected = false;
                _incomingMessages.Enqueue("__DISCONNECTED__");
            }
        }

        private async Task SendAsync(string json)
        {
            if (_ws == null || _ws.State != WebSocketState.Open) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(json);
                await _ws.SendAsync(
                    new ArraySegment<byte>(bytes), WebSocketMessageType.Text,
                    true, _cts?.Token ?? CancellationToken.None);
            }
            catch (Exception e)
            {
                Debug.LogError($"[WS] 发送失败: {e.Message}");
            }
        }

        private void Send(string json)
        {
            if (IsConnected && _ws != null && _ws.State == WebSocketState.Open)
                _ = SendAsync(json);
            else
                _pendingOutgoing.Enqueue(json);
        }

        public async void Disconnect()
        {
            _intentionalDisconnect = true;
            StopReconnectRoutine();

            if (_ws != null && _ws.State == WebSocketState.Open)
            {
                try
                {
                    await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure,
                        "关闭", CancellationToken.None);
                }
                catch { }
            }
            _cts?.Cancel();
            _ws?.Dispose();
            _ws = null;
            IsConnected = false;
        }

#else
        // ---- WebGL: JavaScript interop ----
        [DllImport("__Internal")] private static extern int PF_WebSocketConnect(string url);
        [DllImport("__Internal")] private static extern void PF_WebSocketSend(int id, string msg);
        [DllImport("__Internal")] private static extern void PF_WebSocketClose(int id);

        private int _wsId = -1;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            while (_incomingMessages.TryDequeue(out string json))
            {
                HandleMessage(json);
            }
        }

        public void Connect(string url = null)
        {
            serverUrl = ResolveServerUrl(url);
            _intentionalDisconnect = false;

            if (_wsId >= 0) PF_WebSocketClose(_wsId);
            _wsId = PF_WebSocketConnect(serverUrl);
            Debug.Log($"[WS-WebGL] 连接 {serverUrl}, id={_wsId}");
        }

        private void Send(string json)
        {
            if (_wsId >= 0 && IsConnected)
                PF_WebSocketSend(_wsId, json);
            else
                _pendingOutgoing.Enqueue(json);
        }

        public void Disconnect()
        {
            _intentionalDisconnect = true;
            StopReconnectRoutine();
            if (_wsId >= 0) PF_WebSocketClose(_wsId);
            _wsId = -1;
            IsConnected = false;
        }

        // ---- 由 jslib 通过 SendMessage 回调 ----
        public void _OnWsOpen(string _)
        {
            IsConnected = true;
            _wasConnectedOnce = true;
            Debug.Log("[WS-WebGL] 已连接");
            OnConnected?.Invoke();
            if (IsReconnecting && !_reconnectJoinSent)
                TryRejoinRoomAfterReconnect();
            while (_pendingOutgoing.Count > 0)
                PF_WebSocketSend(_wsId, _pendingOutgoing.Dequeue());
        }

        public void _OnWsMsg(string json)
        {
            _incomingMessages.Enqueue(json);
        }

        public void _OnWsClose(string _)
        {
            IsConnected = false;
            _incomingMessages.Enqueue("__DISCONNECTED__");
        }

        public void _OnWsErr(string err)
        {
            Debug.LogError($"[WS-WebGL] 错误: {err}");
            OnError?.Invoke(err);
        }
#endif

        // ================================================================
        // 公共 API（两个平台共用）
        // ================================================================

        public void CreateRoom(string playerName)
        {
            _lastPlayerName = playerName ?? "";
            Send(JsonUtility.ToJson(new MsgCreateRoom { type = "create_room", name = playerName }));
        }

        public void JoinRoom(string code, string playerName)
        {
            _lastPlayerName = playerName ?? "";
            Send(JsonUtility.ToJson(new MsgJoinRoom { type = "join_room", code = code, name = playerName }));
        }

        public void StartGame()
            => Send("{\"type\":\"start_game\"}");

        public void SendToHost(string payloadJson)
            => Send("{\"type\":\"to_host\",\"payload\":" + payloadJson + "}");

        public void RequestStateSync()
            => SendToHost("{\"type\":\"sync_request\"}");

        public void SendToPlayer(int playerIndex, string payloadJson)
            => Send("{\"type\":\"to_player\",\"targetPlayer\":" + playerIndex +
                    ",\"payload\":" + payloadJson + "}");

        public void BroadcastToAll(string payloadJson)
            => Send("{\"type\":\"to_all\",\"payload\":" + payloadJson + "}");

        // ================================================================
        // 消息分发（主线程）
        // ================================================================

        private void HandleMessage(string json)
        {
            if (json == "__DISCONNECTED__")
            {
                OnDisconnected?.Invoke();
                if (!_intentionalDisconnect)
                    StartReconnectIfNeeded();
                return;
            }

            var msg = JsonUtility.FromJson<MsgBase>(json);
            if (msg == null) return;

            switch (msg.type)
            {
                case "room_created":
                    var created = JsonUtility.FromJson<MsgRoomCreated>(json);
                    LocalPlayerIndex = created.playerIndex;
                    RoomCode = created.code;
                    Debug.Log($"[WS] 房间已创建: {created.code}");
                    OnRoomCreated?.Invoke(created.code, json);
                    break;

                case "room_joined":
                    var joined = JsonUtility.FromJson<MsgRoomJoined>(json);
                    LocalPlayerIndex = joined.playerIndex;
                    RoomCode = joined.code;
                    if (IsReconnecting)
                    {
                        StopReconnectRoutine();
                        OnReconnected?.Invoke();
                    }
                    Debug.Log($"[WS] 已加入房间: {joined.code}, 我是玩家{joined.playerIndex}");
                    OnRoomJoined?.Invoke(joined.playerIndex, json);
                    break;

                case "player_joined":
                    var pj = JsonUtility.FromJson<MsgPlayerJoined>(json);
                    Debug.Log($"[WS] 玩家{pj.playerIndex}加入");
                    OnPlayerJoined?.Invoke(pj.playerIndex, pj.name);
                    break;

                case "player_disconnected":
                    var pd = JsonUtility.FromJson<MsgPlayerDisconnected>(json);
                    Debug.Log($"[WS] 玩家{pd.playerIndex}掉线");
                    OnPlayerDisconnected?.Invoke(pd.playerIndex);
                    break;

                case "game_started":
                    Debug.Log("[WS] 游戏开始");
                    OnGameStarted?.Invoke();
                    break;

                case "from_client":
                    var fc = JsonUtility.FromJson<MsgFromClient>(json);
                    OnClientMessage?.Invoke(fc.playerIndex, fc.payloadRaw ?? json);
                    break;

                case "from_host":
                    var fh = JsonUtility.FromJson<MsgFromHost>(json);
                    OnHostMessage?.Invoke(fh?.payloadJson ?? json);
                    break;

                case "host_disconnected":
                    Debug.LogWarning("[WS] Host 掉线");
                    OnHostDisconnected?.Invoke();
                    OnError?.Invoke("房主掉线，游戏结束");
                    break;

                case "error":
                    var err = JsonUtility.FromJson<MsgError>(json);
                    Debug.LogError($"[WS] 服务器错误: {err.message}");
                    OnError?.Invoke(err.message);
                    break;
            }
        }

        private void OnDestroy()
        {
            StopReconnectRoutine();
            Disconnect();
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        private static string NormalizeWebSocketUrl(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "ws://localhost:8080";

            string s = input.Trim();

            if (s.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return "ws://" + s.Substring("http://".Length);
            if (s.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return "wss://" + s.Substring("https://".Length);
            if (s.StartsWith("ws://", StringComparison.OrdinalIgnoreCase) ||
                s.StartsWith("wss://", StringComparison.OrdinalIgnoreCase))
                return s;

            // 无协议时默认按本地开发走 ws
            return "ws://" + s;
        }

        private string ResolveServerUrl(string providedUrl)
        {
            if (!string.IsNullOrWhiteSpace(providedUrl))
                return NormalizeWebSocketUrl(providedUrl);

#if UNITY_WEBGL && !UNITY_EDITOR
            // WebGL 发布环境（如 Render）：默认跟随当前页面域名
            string runtimeUrl = GetWebGLRuntimeWebSocketUrl();
            if (!string.IsNullOrEmpty(runtimeUrl))
            {
                Debug.Log($"[WS] 自动使用当前站点地址: {runtimeUrl}");
                return runtimeUrl;
            }
#endif

            return NormalizeWebSocketUrl(serverUrl);
        }

        private static string GetWebGLRuntimeWebSocketUrl()
        {
            var absolute = Application.absoluteURL;
            if (string.IsNullOrWhiteSpace(absolute)) return null;
            if (!Uri.TryCreate(absolute, UriKind.Absolute, out var uri)) return null;

            string wsScheme = uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                ? "wss"
                : "ws";

            string hostPort = uri.IsDefaultPort ? uri.Host : $"{uri.Host}:{uri.Port}";
            return $"{wsScheme}://{hostPort}";
        }

        private void StartReconnectIfNeeded()
        {
            if (!autoReconnect || IsReconnecting)
                return;
            if (!_wasConnectedOnce)
                return;
            if (IsHost)
                return;
            if (string.IsNullOrEmpty(RoomCode))
                return;

            Debug.Log("[WS] 连接中断，开始自动重连");
            IsReconnecting = true;
            _reconnectJoinSent = false;
            _reconnectAttemptCount = 0;
            _reconnectRoutine = StartCoroutine(ReconnectCoroutine());
        }

        private System.Collections.IEnumerator ReconnectCoroutine()
        {
            while (IsReconnecting && _reconnectAttemptCount < maxReconnectAttempts)
            {
                _reconnectAttemptCount++;
                OnReconnectAttempt?.Invoke(_reconnectAttemptCount, maxReconnectAttempts);
                Connect(serverUrl);

                float timeout = 8f;
                float elapsed = 0f;
                while (elapsed < timeout && IsReconnecting && !IsConnected)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                // 若已连接，会在 room_joined 中结束重连
                float waitJoinTimeout = 6f;
                float waited = 0f;
                while (waited < waitJoinTimeout && IsReconnecting && IsConnected)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }

                if (!IsReconnecting)
                    yield break;

                float retryWait = Mathf.Max(0.2f, reconnectIntervalSeconds);
                float retryElapsed = 0f;
                while (retryElapsed < retryWait && IsReconnecting)
                {
                    retryElapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (IsReconnecting)
            {
                IsReconnecting = false;
                _reconnectRoutine = null;
                OnError?.Invoke("重连失败，请返回大厅重新加入房间");
            }
        }

        private void TryRejoinRoomAfterReconnect()
        {
            if (string.IsNullOrEmpty(RoomCode) || string.IsNullOrEmpty(_lastPlayerName))
                return;

            _reconnectJoinSent = true;
            JoinRoom(RoomCode, _lastPlayerName);
            Debug.Log($"[WS] 重连后尝试重进房间 {RoomCode}");
        }

        private void StopReconnectRoutine()
        {
            if (_reconnectRoutine != null)
                StopCoroutine(_reconnectRoutine);
            _reconnectRoutine = null;
            IsReconnecting = false;
            _reconnectJoinSent = false;
            _reconnectAttemptCount = 0;
        }

        // ================================================================
        // 消息结构
        // ================================================================

        [Serializable] private class MsgBase { public string type; }
        [Serializable] private class MsgError { public string type; public string message; }
        [Serializable] private class MsgCreateRoom { public string type; public string name; }
        [Serializable] private class MsgJoinRoom { public string type; public string code; public string name; }
        [Serializable] private class MsgRoomCreated { public string type; public string code; public int playerIndex; }
        [Serializable] private class MsgRoomJoined { public string type; public string code; public int playerIndex; }
        [Serializable] private class MsgPlayerJoined { public string type; public int playerIndex; public string name; }
        [Serializable] private class MsgPlayerDisconnected { public string type; public int playerIndex; }
        [Serializable] private class MsgFromClient { public string type; public int playerIndex; public string payloadRaw; }
        [Serializable] private class MsgFromHost { public string type; public string payloadJson; }
    }
}
