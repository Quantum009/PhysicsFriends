// ============================================================
// TradePanel.cs — 交易面板：玩家之间卡牌/mol交易
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Cards;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class TradePanel : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text buyerInfoText;
        [SerializeField] private Text sellerInfoText;
        [SerializeField] private Button confirmButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Slider molSlider;
        [SerializeField] private Text molExchangeText;

        private Action<TradeResponse> _onComplete;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirm);
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancel);
        }

        public void Show(TradeRequest request, Action<TradeResponse> onComplete)
        {
            _onComplete = onComplete;

            if (titleText != null) titleText.text = "交易";
            if (buyerInfoText != null)
                buyerInfoText.text = $"{request.buyer.playerName}\nmol: {request.buyer.mol}\n手牌: {request.buyer.handCards.Count}";
            if (sellerInfoText != null)
                sellerInfoText.text = $"{request.seller.playerName}\nmol: {request.seller.mol}\n手牌: {request.seller.handCards.Count}";
        }

        private void OnConfirm()
        {
            int molExchange = molSlider != null ? (int)molSlider.value : 0;
            _onComplete?.Invoke(new TradeResponse
            {
                tradeOccurred = true,
                buyerGave = new List<CardInstance>(),
                sellerGave = new List<CardInstance>(),
                molExchange = molExchange
            });
            _onComplete = null;
        }

        private void OnCancel()
        {
            _onComplete?.Invoke(new TradeResponse { tradeOccurred = false });
            _onComplete = null;
        }
    }
}
