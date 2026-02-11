// ============================================================
// HandPanel.cs — 手牌面板：展示当前玩家的手牌
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.UI.Components;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    /// <summary>
    /// 手牌面板：显示在屏幕底部，展示当前玩家所有手牌。
    /// 支持点击选中、拖拽、查看详情等交互。
    /// </summary>
    public class HandPanel : MonoBehaviour
    {
        [Header("布局")]
        [SerializeField] private Transform cardContainer;    // 卡牌容器
        [SerializeField] private GameObject cardPrefab;      // CardView预制体
        [SerializeField] private Text handCountText;         // "手牌 X/10"

        [Header("详情面板")]
        [SerializeField] private GameObject detailPanel;     // 卡牌详情弹出面板
        [SerializeField] private Text detailNameText;
        [SerializeField] private Text detailDimensionText;
        [SerializeField] private Text detailEffectText;
        [SerializeField] private Text detailBranchText;

        // 当前玩家和卡牌视图
        private PlayerState _currentPlayer;
        private List<CardView> _cardViews = new List<CardView>();
        private CardView _selectedCard;

        // 事件：卡牌被点击时触发
        public event Action<int> OnCardClicked;     // 参数：手牌索引
        public event Action<int> OnCardDoubleClick; // 双击使用

        /// <summary>刷新手牌显示</summary>
        public void Refresh(PlayerState player)
        {
            _currentPlayer = player;

            // 更新计数
            if (handCountText != null)
                handCountText.text = $"手牌 {player.handCards.Count}/{PlayerState.MaxHandCards}";

            // 确保视图数量匹配
            AdjustCardViews(player.handCards.Count);

            // 更新每张卡牌视图
            for (int i = 0; i < player.handCards.Count; i++)
            {
                _cardViews[i].SetCard(player.handCards[i], i);
                _cardViews[i].SetSelected(false);
                _cardViews[i].gameObject.SetActive(true);
            }

            // 隐藏多余的视图
            for (int i = player.handCards.Count; i < _cardViews.Count; i++)
            {
                _cardViews[i].gameObject.SetActive(false);
            }

            // 隐藏详情
            if (detailPanel != null)
                detailPanel.SetActive(false);

            _selectedCard = null;
        }

        /// <summary>调整CardView数量</summary>
        private void AdjustCardViews(int needed)
        {
            while (_cardViews.Count < needed)
            {
                GameObject obj;
                if (cardPrefab != null && cardContainer != null)
                {
                    obj = Instantiate(cardPrefab, cardContainer);
                }
                else
                {
                    obj = new GameObject($"Card_{_cardViews.Count}");
                    obj.transform.SetParent(cardContainer ?? transform);
                }

                var view = obj.GetComponent<CardView>();
                if (view == null) view = obj.AddComponent<CardView>();

                int idx = _cardViews.Count;
                view.OnClick += () => HandleCardClick(idx);

                _cardViews.Add(view);
            }
        }

        /// <summary>处理卡牌点击</summary>
        private void HandleCardClick(int index)
        {
            if (_currentPlayer == null || index >= _currentPlayer.handCards.Count)
                return;

            // 取消之前的选中
            if (_selectedCard != null)
                _selectedCard.SetSelected(false);

            // 选中新卡牌
            if (index < _cardViews.Count)
            {
                _selectedCard = _cardViews[index];
                _selectedCard.SetSelected(true);
            }

            // 显示详情
            ShowCardDetail(_currentPlayer.handCards[index]);

            // 触发外部事件
            OnCardClicked?.Invoke(index);
        }

        /// <summary>显示卡牌详情</summary>
        private void ShowCardDetail(CardInstance card)
        {
            if (detailPanel == null) return;

            detailPanel.SetActive(true);

            var def = CardDatabase.Get(card.cardId);
            if (def == null) return;

            if (detailNameText != null)
                detailNameText.text = def.nameZH;

            if (detailDimensionText != null)
                detailDimensionText.text = def.dimension.ToString();

            if (detailEffectText != null)
            {
                string effect = def.effectDescription ?? "";
                if (string.IsNullOrEmpty(effect))
                    effect = "基本物理量（无特殊效果）";
                if (card.isUsed && def.effectType == CardEffectType.Active)
                    effect += "\n（已使用）";
                detailEffectText.text = effect;
            }

            if (detailBranchText != null)
                detailBranchText.text = GetBranchName(def.branch);
        }

        /// <summary>获取选中的卡牌索引列表</summary>
        public List<int> GetSelectedIndices()
        {
            var indices = new List<int>();
            for (int i = 0; i < _cardViews.Count; i++)
            {
                if (_cardViews[i].gameObject.activeSelf && _cardViews[i].IsSelected)
                    indices.Add(i);
            }
            return indices;
        }

        /// <summary>设置多选模式（合成/弃牌时）</summary>
        public void SetMultiSelectMode(bool enabled)
        {
            foreach (var v in _cardViews)
                v.SetMultiSelectMode(enabled);
        }

        private string GetBranchName(PhysicsBranch branch)
        {
            switch (branch)
            {
                case PhysicsBranch.Basic: return "基本物理量";
                case PhysicsBranch.Mechanics: return "力学量";
                case PhysicsBranch.Electromagnetics: return "电磁学量";
                case PhysicsBranch.Thermodynamics: return "热学量";
                case PhysicsBranch.Optics: return "光学量";
                case PhysicsBranch.Special: return "特殊量";
                default: return "";
            }
        }
    }
}
