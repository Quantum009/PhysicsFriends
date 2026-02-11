// ============================================================
// ShopPanel.cs — 商店面板：购买卡牌
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Core;
using PhysicsFriends.Data;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class ShopPanel : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text playerMolText;
        [SerializeField] private Button randomBuyButton;
        [SerializeField] private Text randomPriceText;
        [SerializeField] private Button chosenBuyButton;
        [SerializeField] private Text chosenPriceText;
        [SerializeField] private Button skipButton;
        [SerializeField] private Transform basicCardContainer;   // 指定购买的卡牌选择区
        [SerializeField] private GameObject basicCardButtonPrefab;

        private Action<ShopPurchaseResponse> _onComplete;
        private ShopPurchaseRequest _request;
        private PhysicsCardId _chosenCard = PhysicsCardId.Time;

        private void Awake()
        {
            if (randomBuyButton != null)
                randomBuyButton.onClick.AddListener(OnRandomBuy);
            if (chosenBuyButton != null)
                chosenBuyButton.onClick.AddListener(OnChosenBuy);
            if (skipButton != null)
                skipButton.onClick.AddListener(OnSkip);
        }

        public void Show(ShopPurchaseRequest request, Action<ShopPurchaseResponse> onComplete)
        {
            _request = request;
            _onComplete = onComplete;

            if (titleText != null) titleText.text = "商店";
            if (playerMolText != null) playerMolText.text = $"你的mol：{request.player.mol}";
            if (randomPriceText != null) randomPriceText.text = $"随机牌 {request.randomPrice}mol";
            if (chosenPriceText != null) chosenPriceText.text = $"指定牌 {request.chosenPrice}mol";

            if (randomBuyButton != null)
                randomBuyButton.interactable = request.player.mol >= request.randomPrice
                    && !request.player.IsHandFull();
            if (chosenBuyButton != null)
                chosenBuyButton.interactable = request.player.mol >= request.chosenPrice
                    && !request.player.IsHandFull();
        }

        private void OnRandomBuy()
        {
            _onComplete?.Invoke(new ShopPurchaseResponse { purchaseType = ShopPurchaseType.Random });
            _onComplete = null;
        }

        private void OnChosenBuy()
        {
            _onComplete?.Invoke(new ShopPurchaseResponse
            {
                purchaseType = ShopPurchaseType.Chosen,
                chosenCardId = _chosenCard
            });
            _onComplete = null;
        }

        private void OnSkip()
        {
            _onComplete?.Invoke(new ShopPurchaseResponse { purchaseType = ShopPurchaseType.None });
            _onComplete = null;
        }
    }
}
