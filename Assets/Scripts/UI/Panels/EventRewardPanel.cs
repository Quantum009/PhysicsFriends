// ============================================================
// EventRewardPanel.cs — 事件/奖励牌展示面板
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Data;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class EventRewardPanel : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Text cardTypeText;    // "事件牌" / "奖励牌"
        [SerializeField] private Text cardNameText;
        [SerializeField] private Text effectText;
        [SerializeField] private Text playerText;
        [SerializeField] private Image cardImage;
        [SerializeField] private Button confirmButton;

        private Action _onConfirm;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(() =>
                {
                    var cb = _onConfirm;
                    _onConfirm = null;
                    cb?.Invoke();
                });
        }

        public void ShowEvent(EventCardShowRequest request, Action onConfirm)
        {
            _onConfirm = onConfirm;

            var def = EventCardDatabase.Get(request.eventId);

            if (cardTypeText != null) cardTypeText.text = "事件牌";
            if (cardNameText != null) cardNameText.text = def?.nameZH ?? request.eventId.ToString();
            if (effectText != null) effectText.text = request.effectDescription ?? def?.descriptionZH ?? "";
            if (playerText != null) playerText.text = request.player.playerName;

            // 设置卡牌背景色（负面事件红色，正面事件绿色）
            if (cardImage != null)
            {
                cardImage.color = (def != null && def.isNegative)
                    ? new Color(1f, 0.7f, 0.7f)
                    : new Color(0.7f, 1f, 0.7f);
            }
        }

        public void ShowReward(RewardCardShowRequest request, Action onConfirm)
        {
            _onConfirm = onConfirm;

            var def = RewardCardDatabase.Get(request.rewardId);

            if (cardTypeText != null) cardTypeText.text = "奖励牌";
            if (cardNameText != null) cardNameText.text = def?.nameZH ?? request.rewardId.ToString();
            if (effectText != null) effectText.text = request.effectDescription ?? def?.descriptionZH ?? "";
            if (playerText != null) playerText.text = request.player.playerName;

            if (cardImage != null)
                cardImage.color = new Color(1f, 0.9f, 0.5f); // 金色
        }
    }
}
