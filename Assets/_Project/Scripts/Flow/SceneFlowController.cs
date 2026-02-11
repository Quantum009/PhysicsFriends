// ============================================================
// SceneFlowController.cs — 场景流程控制器
// MainMenu → Lobby → Game 的流转，包括离线模式直接进 Game
// 使用 DontDestroyOnLoad 跨场景保持数据
// ============================================================
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using PhysicsFriends.Core;

namespace PhysicsFriends.Flow
{
    /// <summary>
    /// 跨场景的全局配置传递。
    /// 在 MainMenu 创建，DontDestroyOnLoad，Game 场景的 GameManager 读取。
    /// </summary>
    public class GameSessionData
    {
        public bool isOnline;             // 联机 or 离线
        public GameMode gameMode;
        public int playerCount;
        public Character[] characters;
        public string[] playerNames;
        public PlayerColor[] playerColors;
        public string lobbyCode;

        // 联机时的 clientId → playerIndex 映射由 GameNetworkManager 管理
    }

    public class SceneFlowController : MonoBehaviour
    {
        public static SceneFlowController Instance { get; private set; }
        public GameSessionData SessionData { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ================================================================
        // 从 MainMenu 调用
        // ================================================================

        /// <summary>离线模式：选好配置后直接进 Game 场景</summary>
        public void StartOfflineGame(GameMode mode, int playerCount,
            Character[] characters, string[] names, PlayerColor[] colors)
        {
            SessionData = new GameSessionData
            {
                isOnline = false,
                gameMode = mode,
                playerCount = playerCount,
                characters = characters,
                playerNames = names,
                playerColors = colors
            };
            SceneManager.LoadScene("Game");
        }

        /// <summary>联机模式：房主创建房间后进入 Lobby</summary>
        public void GoToLobbyAsHost(string lobbyCode, GameMode mode)
        {
            SessionData = new GameSessionData
            {
                isOnline = true,
                gameMode = mode,
                lobbyCode = lobbyCode
            };
            SceneManager.LoadScene("Lobby");
        }

        /// <summary>联机模式：加入房间后进入 Lobby</summary>
        public void GoToLobbyAsClient(string lobbyCode)
        {
            SessionData = new GameSessionData
            {
                isOnline = true,
                lobbyCode = lobbyCode
            };
            SceneManager.LoadScene("Lobby");
        }

        // ================================================================
        // 从 Lobby 调用
        // ================================================================

        /// <summary>联机模式：所有人就绪，进入 Game 场景</summary>
        public void StartOnlineGame(int playerCount, Character[] characters,
            string[] names, PlayerColor[] colors)
        {
            if (SessionData == null) SessionData = new GameSessionData();
            SessionData.playerCount = playerCount;
            SessionData.characters = characters;
            SessionData.playerNames = names;
            SessionData.playerColors = colors;

            // 联机时由 Host 通过 NetworkManager.SceneManager 切换场景
            // 这里仅更新数据，实际切场景由 LobbyScreen.OnStartGame() 触发
        }

        // ================================================================
        // 从 Game 调用
        // ================================================================

        /// <summary>游戏结束回到主菜单</summary>
        public void ReturnToMainMenu()
        {
            SessionData = null;
            SceneManager.LoadScene("MainMenu");
        }

        /// <summary>再来一局（使用相同配置）</summary>
        public void RestartGame()
        {
            SceneManager.LoadScene("Game");
        }

        // ================================================================
        // 查询
        // ================================================================

        /// <summary>当前是否联机模式</summary>
        public bool IsOnline => SessionData?.isOnline ?? false;

        /// <summary>获取游戏模式（默认标准）</summary>
        public GameMode GetGameMode() => SessionData?.gameMode ?? GameMode.Standard;

        /// <summary>获取玩家数量（默认2）</summary>
        public int GetPlayerCount() => SessionData?.playerCount ?? 2;
    }
}
