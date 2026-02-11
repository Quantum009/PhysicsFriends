// ============================================================
// UIScreens.cs — 全屏界面：主菜单、大厅、角色选择
// 需要在 Unity Editor 中创建对应 Canvas + 按钮
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PhysicsFriends.Core;
using PhysicsFriends.Network;

namespace PhysicsFriends.UI.Screens
{
    // ================================================================
    // 主菜单
    // ================================================================

    public class MainMenuScreen : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Button btnCreateRoom;
        [SerializeField] private Button btnJoinRoom;
        [SerializeField] private Button btnLocalPlay;
        [SerializeField] private Button btnSettings;
        [SerializeField] private TMP_InputField inputJoinCode;
        [SerializeField] private GameObject joinCodePanel;
        [SerializeField] private TextMeshProUGUI txtVersion;

        private void Start()
        {
            btnCreateRoom.onClick.AddListener(OnCreateRoom);
            btnJoinRoom.onClick.AddListener(OnJoinRoom);
            btnLocalPlay.onClick.AddListener(OnLocalPlay);
            if (btnSettings != null)
                btnSettings.onClick.AddListener(OnSettings);

            joinCodePanel?.SetActive(false);
            if (txtVersion != null)
                txtVersion.text = "v0.1.0 Alpha";
        }

        private async void OnCreateRoom()
        {
            btnCreateRoom.interactable = false;
            string code = await LobbyManager.Instance.CreateLobby("PhysicsFriends", GameMode.Standard);
            if (code != null)
            {
                // 切换到大厅界面
                LoadLobbyScene(code);
            }
            btnCreateRoom.interactable = true;
        }

        private void OnJoinRoom()
        {
            joinCodePanel.SetActive(true);
            inputJoinCode.Select();
        }

        public async void OnConfirmJoinCode()
        {
            string code = inputJoinCode.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code)) return;

            bool success = await LobbyManager.Instance.JoinLobby(code);
            if (success)
            {
                LoadLobbyScene(code);
            }
            else
            {
                // TODO: 显示错误提示
                Debug.LogError("加入房间失败");
            }
        }

        private void OnLocalPlay()
        {
            // 离线模式：直接进入角色选择
            UnityEngine.SceneManagement.SceneManager.LoadScene("CharacterSelect");
        }

        private void OnSettings()
        {
            // TODO: 打开设置面板
        }

        private void LoadLobbyScene(string code)
        {
            // 可以用 PlayerPrefs 或静态变量传递 code
            PlayerPrefs.SetString("LobbyCode", code);
            UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
        }
    }

    // ================================================================
    // 大厅界面
    // ================================================================

    public class LobbyScreen : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private TextMeshProUGUI txtRoomCode;
        [SerializeField] private TextMeshProUGUI txtPlayerCount;
        [SerializeField] private Transform playerListParent;
        [SerializeField] private GameObject playerSlotPrefab;
        [SerializeField] private Button btnStart;
        [SerializeField] private Button btnLeave;
        [SerializeField] private TMP_Dropdown dropdownGameMode;

        private void Start()
        {
            string code = PlayerPrefs.GetString("LobbyCode", "???");
            txtRoomCode.text = $"房间码: {code}";

            btnStart.onClick.AddListener(OnStartGame);
            btnLeave.onClick.AddListener(OnLeaveRoom);

            // 只有 Host 能看到开始按钮和模式选择
            bool isHost = LobbyManager.Instance != null && LobbyManager.Instance.IsHost;
            btnStart.gameObject.SetActive(isHost);
            if (dropdownGameMode != null)
                dropdownGameMode.interactable = isHost;

            // 监听玩家变化
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnPlayersUpdated += RefreshPlayerList;
        }

        private void OnDestroy()
        {
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.OnPlayersUpdated -= RefreshPlayerList;
        }

        private void RefreshPlayerList(List<LobbyPlayerInfo> players)
        {
            // 清除旧的
            for (int i = playerListParent.childCount - 1; i >= 0; i--)
                Destroy(playerListParent.GetChild(i).gameObject);

            // 创建新的
            foreach (var p in players)
            {
                var slot = Instantiate(playerSlotPrefab, playerListParent);
                var txt = slot.GetComponentInChildren<TextMeshProUGUI>();
                if (txt != null)
                    txt.text = p.isHost ? $"★ {p.playerId}" : p.playerId;
            }

            txtPlayerCount.text = $"玩家: {players.Count}/4";
            btnStart.interactable = players.Count >= 2; // 至少2人
        }

        private async void OnStartGame()
        {
            await LobbyManager.Instance.MarkGameStarted();
            // Host 通过 Netcode 的场景管理加载游戏场景
            Unity.Netcode.NetworkManager.Singleton.SceneManager
                .LoadScene("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }

        private async void OnLeaveRoom()
        {
            await LobbyManager.Instance.LeaveLobby();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    // ================================================================
    // 角色选择界面
    // ================================================================

    public class CharacterSelectScreen : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Button[] characterButtons;     // 4个角色按钮
        [SerializeField] private Image[] characterPortraits;
        [SerializeField] private TextMeshProUGUI txtTaskDesc;
        [SerializeField] private TextMeshProUGUI txtAbilityDesc;
        [SerializeField] private TextMeshProUGUI txtCharName;
        [SerializeField] private Button btnConfirm;

        public event Action<Character> OnCharacterSelected;

        private Character _selectedCharacter = Character.Newton;

        private void Start()
        {
            for (int i = 0; i < characterButtons.Length && i < 4; i++)
            {
                int idx = i; // 闭包捕获
                characterButtons[i].onClick.AddListener(() => SelectCharacter((Character)idx));
            }

            btnConfirm.onClick.AddListener(ConfirmSelection);
            SelectCharacter(Character.Newton); // 默认选牛顿
        }

        private void SelectCharacter(Character c)
        {
            _selectedCharacter = c;
            txtCharName.text = Systems.CharacterAbilitySystem.GetCharacterName(c);
            txtTaskDesc.text = Systems.CharacterAbilitySystem.GetTaskDescription(c);
            txtAbilityDesc.text = Systems.CharacterAbilitySystem.GetAbilityDescription(c);

            // 高亮选中的角色
            for (int i = 0; i < characterButtons.Length; i++)
            {
                var colors = characterButtons[i].colors;
                colors.normalColor = (i == (int)c) ? Color.yellow : Color.white;
                characterButtons[i].colors = colors;
            }
        }

        private void ConfirmSelection()
        {
            OnCharacterSelected?.Invoke(_selectedCharacter);
            Debug.Log($"[CharSelect] Selected: {_selectedCharacter}");
        }
    }
}
