// ============================================================
// LobbyManager.cs — 房间管理：创建/加入/心跳/轮询
// ============================================================
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport.Relay;
using PhysicsFriends.Core;
using PhysicsFriends.Utils;

namespace PhysicsFriends.Network
{
    public class LobbyManager : Singleton<LobbyManager>
    {
        // ---- 事件 ----
        public event Action<string> OnLobbyCreated;         // lobbyCode
        public event Action OnLobbyJoined;
        public event Action<List<LobbyPlayerInfo>> OnPlayersUpdated;
        public event Action<string> OnError;

        // ---- 状态 ----
        private Lobby _currentLobby;
        private float _heartbeatTimer;
        private float _pollTimer;
        private const float HEARTBEAT_INTERVAL = 15f;
        private const float POLL_INTERVAL = 1.5f;

        public Lobby CurrentLobby => _currentLobby;
        public bool IsInLobby => _currentLobby != null;
        public bool IsHost => NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

        // ================================================================
        // Host: 创建房间
        // ================================================================

        public async Task<string> CreateLobby(string lobbyName, GameMode mode, int maxPlayers = 4)
        {
            try
            {
                // 1. 创建 Relay Allocation（最多 maxPlayers-1 个连接）
                Allocation allocation = await RelayService.Instance
                    .CreateAllocationAsync(maxConnections: maxPlayers - 1);
                string joinCode = await RelayService.Instance
                    .GetJoinCodeAsync(allocation.AllocationId);

                // 2. 配置 UnityTransport 使用 Relay
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

                // 3. 创建 Lobby
                var options = new CreateLobbyOptions
                {
                    IsPrivate = false,
                    Data = new Dictionary<string, DataObject>
                    {
                        { "joinCode", new DataObject(
                            DataObject.VisibilityOptions.Member, joinCode) },
                        { "gameMode", new DataObject(
                            DataObject.VisibilityOptions.Public, mode.ToString()) },
                        { "gameStarted", new DataObject(
                            DataObject.VisibilityOptions.Public, "false") }
                    }
                };

                _currentLobby = await Lobbies.Instance
                    .CreateLobbyAsync(lobbyName, maxPlayers, options);

                // 4. 启动 Host
                NetworkManager.Singleton.StartHost();

                Debug.Log($"[Lobby] 创建房间成功: {_currentLobby.LobbyCode}");
                OnLobbyCreated?.Invoke(_currentLobby.LobbyCode);

                return _currentLobby.LobbyCode;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] 创建房间失败: {e.Message}");
                OnError?.Invoke(e.Message);
                return null;
            }
        }

        // ================================================================
        // Client: 加入房间
        // ================================================================

        public async Task<bool> JoinLobby(string lobbyCode)
        {
            try
            {
                // 1. 加入 Lobby
                _currentLobby = await Lobbies.Instance.JoinLobbyByCodeAsync(lobbyCode);

                // 2. 获取 Relay join code
                string joinCode = _currentLobby.Data["joinCode"].Value;

                // 3. 加入 Relay
                JoinAllocation joinAllocation = await RelayService.Instance
                    .JoinAllocationAsync(joinCode);

                // 4. 配置 Transport
                var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
                transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

                // 5. 连接
                NetworkManager.Singleton.StartClient();

                Debug.Log($"[Lobby] 加入房间成功: {lobbyCode}");
                OnLobbyJoined?.Invoke();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[Lobby] 加入房间失败: {e.Message}");
                OnError?.Invoke(e.Message);
                return false;
            }
        }

        // ================================================================
        // 更新循环
        // ================================================================

        private void Update()
        {
            if (_currentLobby == null) return;

            // Host 发送心跳保活
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                _heartbeatTimer += Time.deltaTime;
                if (_heartbeatTimer >= HEARTBEAT_INTERVAL)
                {
                    _heartbeatTimer = 0;
                    SendHeartbeat();
                }
            }

            // 所有人轮询 Lobby 变化（玩家进出）
            _pollTimer += Time.deltaTime;
            if (_pollTimer >= POLL_INTERVAL)
            {
                _pollTimer = 0;
                PollLobby();
            }
        }

        private async void SendHeartbeat()
        {
            try
            {
                await Lobbies.Instance.SendHeartbeatPingAsync(_currentLobby.Id);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] 心跳失败: {e.Message}");
            }
        }

        private async void PollLobby()
        {
            try
            {
                _currentLobby = await Lobbies.Instance.GetLobbyAsync(_currentLobby.Id);

                var playerInfos = new List<LobbyPlayerInfo>();
                foreach (var p in _currentLobby.Players)
                {
                    playerInfos.Add(new LobbyPlayerInfo
                    {
                        playerId = p.Id,
                        isHost = p.Id == _currentLobby.HostId,
                        data = p.Data
                    });
                }
                OnPlayersUpdated?.Invoke(playerInfos);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] 轮询失败: {e.Message}");
            }
        }

        // ================================================================
        // Host: 标记游戏开始
        // ================================================================

        public async Task MarkGameStarted()
        {
            if (!IsHost || _currentLobby == null) return;

            try
            {
                await Lobbies.Instance.UpdateLobbyAsync(_currentLobby.Id,
                    new UpdateLobbyOptions
                    {
                        Data = new Dictionary<string, DataObject>
                        {
                            { "gameStarted", new DataObject(
                                DataObject.VisibilityOptions.Public, "true") }
                        }
                    });
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] 更新状态失败: {e.Message}");
            }
        }

        // ================================================================
        // 离开/清理
        // ================================================================

        public async Task LeaveLobby()
        {
            if (_currentLobby == null) return;

            try
            {
                if (IsHost)
                    await Lobbies.Instance.DeleteLobbyAsync(_currentLobby.Id);
                else
                    await Lobbies.Instance.RemovePlayerAsync(_currentLobby.Id,
                        Unity.Services.Authentication.AuthenticationService.Instance.PlayerId);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Lobby] 退出失败: {e.Message}");
            }

            _currentLobby = null;

            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient)
                NetworkManager.Singleton.Shutdown();
        }

        private void OnApplicationQuit()
        {
            _ = LeaveLobby();
        }
    }

    /// <summary>Lobby 内玩家信息</summary>
    public class LobbyPlayerInfo
    {
        public string playerId;
        public bool isHost;
        public Dictionary<string, PlayerDataObject> data;
    }
}
