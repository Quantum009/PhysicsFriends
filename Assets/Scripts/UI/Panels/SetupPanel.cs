// ============================================================
// SetupPanel.cs — 游戏设置面板：模式/人数/角色选择
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Core;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class SetupPanel : MonoBehaviour
    {
        [Header("游戏模式")]
        [SerializeField] private Button fastModeButton;
        [SerializeField] private Button standardModeButton;
        [SerializeField] private Button slowModeButton;

        [Header("玩家数量")]
        [SerializeField] private Button twoPlayerButton;
        [SerializeField] private Button threePlayerButton;
        [SerializeField] private Button fourPlayerButton;

        [Header("角色选择")]
        [SerializeField] private Transform characterContainer;

        [Header("开始")]
        [SerializeField] private Button startButton;
        [SerializeField] private Text summaryText;

        private Action<GameSetupResponse> _onComplete;
        private GameMode _selectedMode = GameMode.Standard;
        private int _selectedCount = 2;
        private Character[] _selectedChars = { Character.Newton, Character.Maxwell,
                                                Character.Einstein, Character.Schrodinger };

        private void Awake()
        {
            if (fastModeButton != null)
                fastModeButton.onClick.AddListener(() => SelectMode(GameMode.Fast));
            if (standardModeButton != null)
                standardModeButton.onClick.AddListener(() => SelectMode(GameMode.Standard));
            if (slowModeButton != null)
                slowModeButton.onClick.AddListener(() => SelectMode(GameMode.Slow));

            if (twoPlayerButton != null)
                twoPlayerButton.onClick.AddListener(() => SelectCount(2));
            if (threePlayerButton != null)
                threePlayerButton.onClick.AddListener(() => SelectCount(3));
            if (fourPlayerButton != null)
                fourPlayerButton.onClick.AddListener(() => SelectCount(4));

            if (startButton != null)
                startButton.onClick.AddListener(OnStart);
        }

        public void Show(GameSetupRequest request, Action<GameSetupResponse> onComplete)
        {
            _onComplete = onComplete;
            UpdateSummary();
        }

        private void SelectMode(GameMode mode)
        {
            _selectedMode = mode;
            UpdateSummary();
        }

        private void SelectCount(int count)
        {
            _selectedCount = count;
            UpdateSummary();
        }

        private void UpdateSummary()
        {
            if (summaryText != null)
            {
                string modeName = _selectedMode == GameMode.Fast ? "快速(~15分钟)"
                    : _selectedMode == GameMode.Standard ? "标准(~30分钟)" : "慢速(~45分钟)";
                summaryText.text = $"模式：{modeName}\n玩家：{_selectedCount}人";
            }
        }

        private void OnStart()
        {
            var colors = new[] { PlayerColor.Red, PlayerColor.Blue,
                                 PlayerColor.Green, PlayerColor.Yellow };
            var names = new string[_selectedCount];
            var chars = new Character[_selectedCount];
            var playerColors = new PlayerColor[_selectedCount];

            for (int i = 0; i < _selectedCount; i++)
            {
                chars[i] = _selectedChars[i];
                playerColors[i] = colors[i];
                names[i] = $"玩家{i + 1}";
            }

            _onComplete?.Invoke(new GameSetupResponse
            {
                gameMode = _selectedMode,
                playerCount = _selectedCount,
                characters = chars,
                colors = playerColors,
                playerNames = names
            });
            _onComplete = null;
        }
    }
}
