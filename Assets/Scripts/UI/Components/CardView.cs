// ============================================================
// CardView.cs — 单张卡牌视觉组件
// 用于手牌面板和各种卡牌选择场景
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using PhysicsFriends.Cards;
using PhysicsFriends.Core;
using PhysicsFriends.Data;

namespace PhysicsFriends.UI.Components
{
    /// <summary>
    /// 单张卡牌的视觉组件。
    /// 显示卡牌名称、类型标记、效果图标等。
    /// 支持点击选中、多选模式。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CardView : MonoBehaviour, IPointerClickHandler
    {
        [Header("UI引用")]
        [SerializeField] private Text nameText;
        [SerializeField] private Text dimensionText;
        [SerializeField] private Text effectTypeText;     // "被动"/"主动"/"抉择"/空
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image selectionOutline;
        [SerializeField] private Image usedOverlay;       // 已使用遮罩

        // 数据
        private CardInstance _card;
        private int _handIndex;
        private bool _isSelected;
        private bool _multiSelectMode;
        private bool _isEnabled = true;

        // 事件
        public event Action OnClick;

        public bool IsSelected => _isSelected;

        /// <summary>设置卡牌数据</summary>
        public void SetCard(CardInstance card, int handIndex)
        {
            _card = card;
            _handIndex = handIndex;

            var def = CardDatabase.Get(card.cardId);
            if (def == null) return;

            if (nameText != null)
                nameText.text = def.nameZH;

            if (dimensionText != null)
                dimensionText.text = def.dimension.ToString();

            if (effectTypeText != null)
            {
                switch (def.effectType)
                {
                    case CardEffectType.Passive: effectTypeText.text = "被动"; break;
                    case CardEffectType.Active:  effectTypeText.text = "主动"; break;
                    case CardEffectType.Choice:  effectTypeText.text = "抉择"; break;
                    default:                     effectTypeText.text = ""; break;
                }
            }

            if (backgroundImage != null)
                backgroundImage.color = GetBranchColor(def.branch);

            if (usedOverlay != null)
                usedOverlay.gameObject.SetActive(card.isUsed);

            SetSelected(false);
        }

        /// <summary>设置选中状态</summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (selectionOutline != null)
                selectionOutline.gameObject.SetActive(selected);

            // 选中时抬高卡牌
            var rt = GetComponent<RectTransform>();
            if (rt != null)
            {
                var pos = rt.anchoredPosition;
                pos.y = selected ? 15f : 0f;
                rt.anchoredPosition = pos;
            }
        }

        /// <summary>设置是否可交互</summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            if (backgroundImage != null)
            {
                var c = backgroundImage.color;
                c.a = enabled ? 1f : 0.4f;
                backgroundImage.color = c;
            }
        }

        /// <summary>设置多选模式</summary>
        public void SetMultiSelectMode(bool enabled)
        {
            _multiSelectMode = enabled;
        }

        /// <summary>点击事件</summary>
        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isEnabled) return;

            if (_multiSelectMode)
            {
                // 多选模式下切换选中
                SetSelected(!_isSelected);
            }

            OnClick?.Invoke();
        }

        /// <summary>根据学科分支获取背景色</summary>
        private Color GetBranchColor(PhysicsBranch branch)
        {
            switch (branch)
            {
                case PhysicsBranch.Basic:            return new Color(0.75f, 0.95f, 0.75f); // 浅绿
                case PhysicsBranch.Mechanics:        return new Color(0.95f, 0.8f, 0.6f);   // 浅橙
                case PhysicsBranch.Electromagnetics: return new Color(0.7f, 0.8f, 1f);      // 浅蓝
                case PhysicsBranch.Thermodynamics:   return new Color(1f, 0.7f, 0.7f);      // 浅红
                case PhysicsBranch.Optics:           return new Color(1f, 1f, 0.6f);        // 浅黄
                case PhysicsBranch.Special:          return new Color(0.85f, 0.7f, 0.95f);  // 浅紫
                default: return Color.white;
            }
        }
    }
}
