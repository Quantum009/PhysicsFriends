// ============================================================
// HUDPanel.cs — 顶部HUD面板：显示游戏全局状态
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Core;
using PhysicsFriends.Player;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    /// <summary>
    /// HUD面板：始终显示在屏幕顶部/侧边。
    /// 显示当前玩家、回合数、纪元、每个玩家的mol和创举分。
    /// </summary>
    public class HUDPanel : MonoBehaviour
    {
        [Header("全局信息")]
        [SerializeField] private Text turnNumberText;      // "回合 N"
        [SerializeField] private Text eraText;             // "自然哲学 / 经典物理 / 现代物理"
        [SerializeField] private Text currentPlayerText;   // "当前：玩家X"

        [Header("玩家状态")]
        [SerializeField] private Transform playerStatusParent; // 玩家状态条目的父物体
        [SerializeField] private GameObject playerStatusPrefab; // PlayerStatusEntry预制体

        // 缓存的状态条目
        private List<PlayerStatusEntry> _entries = new List<PlayerStatusEntry>();

        /// <summary>刷新HUD数据</summary>
        public void Refresh(List<PlayerState> players, int currentPlayerIndex,
            int roundNumber, Era era)
        {
            // 更新全局信息
            if (turnNumberText != null)
                turnNumberText.text = $"回合 {roundNumber}";

            if (eraText != null)
                eraText.text = GetEraName(era);

            if (currentPlayerText != null && currentPlayerIndex < players.Count)
                currentPlayerText.text = $"当前：{players[currentPlayerIndex].playerName}";

            // 确保有足够的状态条目
            EnsureEntries(players.Count);

            // 更新每个玩家的状态
            for (int i = 0; i < players.Count; i++)
            {
                _entries[i].Refresh(players[i], i == currentPlayerIndex);
            }
        }

        /// <summary>确保条目数量足够</summary>
        private void EnsureEntries(int count)
        {
            while (_entries.Count < count)
            {
                if (playerStatusPrefab != null && playerStatusParent != null)
                {
                    var obj = Instantiate(playerStatusPrefab, playerStatusParent);
                    var entry = obj.GetComponent<PlayerStatusEntry>();
                    if (entry == null) entry = obj.AddComponent<PlayerStatusEntry>();
                    _entries.Add(entry);
                }
                else
                {
                    // 没有预制体时创建简单文字
                    var obj = new GameObject($"PlayerStatus_{_entries.Count}");
                    obj.transform.SetParent(playerStatusParent ?? transform);
                    var entry = obj.AddComponent<PlayerStatusEntry>();
                    _entries.Add(entry);
                }
            }
        }

        private string GetEraName(Era era)
        {
            switch (era)
            {
                case Era.NaturalPhilosophy: return "第一纪元：自然哲学";
                case Era.ClassicalPhysics:  return "第二纪元：经典物理";
                case Era.ModernPhysics:     return "第三纪元：现代物理";
                default: return era.ToString();
            }
        }
    }

    /// <summary>单个玩家的状态显示条目</summary>
    public class PlayerStatusEntry : MonoBehaviour
    {
        [SerializeField] private Text nameText;
        [SerializeField] private Text molText;
        [SerializeField] private Text achievementText;
        [SerializeField] private Text handCountText;
        [SerializeField] private Text statusText;        // buff/debuff状态
        [SerializeField] private Image backgroundImage;  // 背景（当前玩家高亮）

        public void Refresh(PlayerState player, bool isCurrentPlayer)
        {
            if (nameText != null)
                nameText.text = player.playerName;

            if (molText != null)
                molText.text = $"{player.mol} mol";

            if (achievementText != null)
                achievementText.text = $"创举 {player.achievementPoints}分";

            if (handCountText != null)
                handCountText.text = $"手牌 {player.handCards.Count}/{PlayerState.MaxHandCards}";

            // 状态效果摘要
            if (statusText != null)
            {
                var statuses = new List<string>();
                if (player.stunTurns > 0) statuses.Add($"眩晕({player.stunTurns})");
                if (player.absoluteZeroTurns > 0) statuses.Add($"绝对零度({player.absoluteZeroTurns})");
                if (player.heavyLayers > 0) statuses.Add($"沉重x{player.heavyLayers}");
                if (player.lightLayers > 0) statuses.Add($"轻盈x{player.lightLayers}");
                if (player.phaseState != PhaseState.None) statuses.Add($"相变:{player.phaseState}");
                if (player.emShieldTurns > 0) statuses.Add($"电磁屏蔽({player.emShieldTurns})");
                if (player.michelsonMorleyTurns > 0) statuses.Add($"迈-莫({player.michelsonMorleyTurns})");
                statusText.text = statuses.Count > 0 ? string.Join(" | ", statuses) : "";
            }

            // 当前玩家高亮
            if (backgroundImage != null)
            {
                var c = backgroundImage.color;
                c.a = isCurrentPlayer ? 0.3f : 0.1f;
                backgroundImage.color = c;
            }
        }
    }
}
