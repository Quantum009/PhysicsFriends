// ============================================================
// CardSelectDialog.cs — 手牌选择对话框
// 让玩家从手牌中选择指定数量的卡牌
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Cards;
using PhysicsFriends.Data;
using PhysicsFriends.UI.Components;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class CardSelectDialog : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Text selectedCountText;
        [SerializeField] private Transform cardContainer;
        [SerializeField] private GameObject cardPrefab;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;

        private Action<CardSelectResponse> _onComplete;
        private CardSelectRequest _request;
        private List<CardView> _views = new List<CardView>();
        private HashSet<int> _selectedIndices = new HashSet<int>();

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirm);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancel);
        }

        public void Show(CardSelectRequest request, Action<CardSelectResponse> onComplete)
        {
            _request = request;
            _onComplete = onComplete;
            _selectedIndices.Clear();

            if (titleText != null) titleText.text = request.title;
            if (messageText != null) messageText.text = request.message;

            // 清除旧视图
            foreach (var v in _views)
                if (v != null) Destroy(v.gameObject);
            _views.Clear();

            // 生成卡牌视图
            var hand = request.player.handCards;
            for (int i = 0; i < hand.Count; i++)
            {
                bool canSelect = request.filter == null || request.filter(hand[i]);

                GameObject obj = cardPrefab != null
                    ? Instantiate(cardPrefab, cardContainer)
                    : CreateSimpleCard(cardContainer);

                var view = obj.GetComponent<CardView>();
                if (view == null) view = obj.AddComponent<CardView>();

                view.SetCard(hand[i], i);
                view.SetEnabled(canSelect);
                view.SetMultiSelectMode(true);

                int idx = i;
                view.OnClick += () => ToggleSelection(idx);

                _views.Add(view);
            }

            UpdateSelectionUI();
        }

        private void ToggleSelection(int index)
        {
            if (_selectedIndices.Contains(index))
            {
                _selectedIndices.Remove(index);
                _views[index].SetSelected(false);
            }
            else
            {
                if (_selectedIndices.Count < _request.maxSelect)
                {
                    _selectedIndices.Add(index);
                    _views[index].SetSelected(true);
                }
            }
            UpdateSelectionUI();
        }

        private void UpdateSelectionUI()
        {
            if (selectedCountText != null)
                selectedCountText.text = $"已选 {_selectedIndices.Count}/{_request.maxSelect}";

            if (confirmButton != null)
                confirmButton.interactable = _selectedIndices.Count >= _request.minSelect;
        }

        private void OnConfirm()
        {
            var selected = _selectedIndices
                .Where(i => i < _request.player.handCards.Count)
                .Select(i => _request.player.handCards[i])
                .ToList();

            _onComplete?.Invoke(new CardSelectResponse
            {
                selectedCards = selected,
                cancelled = false
            });
            _onComplete = null;
        }

        private void OnCancel()
        {
            _onComplete?.Invoke(new CardSelectResponse
            {
                selectedCards = new List<CardInstance>(),
                cancelled = true
            });
            _onComplete = null;
        }

        private GameObject CreateSimpleCard(Transform parent)
        {
            var obj = new GameObject("Card");
            obj.transform.SetParent(parent ?? transform);
            obj.AddComponent<RectTransform>();
            return obj;
        }
    }
}
