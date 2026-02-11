// ============================================================
// ModificationPanel.cs — 骰子修正链面板
// 处理力/加速度/压强/弹性系数的修正交互
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class ModificationPanel : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text descriptionText;
        [SerializeField] private Text currentValueText;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button minusButton;
        [SerializeField] private Button skipButton;
        [SerializeField] private Button useButton;

        // === 力修正 ===
        public void AskForce(ForceModRequest request, Action<ForceModResponse> onComplete)
        {
            if (titleText != null)
                titleText.text = "力修正";
            if (descriptionText != null)
                descriptionText.text = $"{request.sourcePlayer.playerName} 对 {request.dicePlayer.playerName} 使用\"力\"？";
            if (currentValueText != null)
                currentValueText.text = $"当前骰子值：{request.currentDiceValue}";

            int maxMod = request.hasPrincipia ? 2 : 1;

            SetupTwoButtons(
                $"+{maxMod}", $"-{maxMod}", "不使用",
                () => onComplete(new ForceModResponse { useForce = true, direction = maxMod }),
                () => onComplete(new ForceModResponse { useForce = true, direction = -maxMod }),
                () => onComplete(new ForceModResponse { useForce = false, direction = 0 })
            );
        }

        // === 加速度修正 ===
        public void AskAccel(AccelModRequest request, Action<AccelModResponse> onComplete)
        {
            if (titleText != null)
                titleText.text = "加速度修正";
            if (descriptionText != null)
                descriptionText.text = $"{request.player.playerName} 使用加速度？";
            if (currentValueText != null)
                currentValueText.text = $"当前骰子值：{request.currentDiceValue}";

            SetupTwoButtons(
                "+1", "-1", "不使用",
                () => onComplete(new AccelModResponse { useAccel = true, direction = 1 }),
                () => onComplete(new AccelModResponse { useAccel = true, direction = -1 }),
                () => onComplete(new AccelModResponse { useAccel = false, direction = 0 })
            );
        }

        // === 压强无效化 ===
        public void AskPressure(PressureNullifyRequest request, Action<bool> onComplete)
        {
            if (titleText != null)
                titleText.text = "压强无效化";
            if (descriptionText != null)
                descriptionText.text = $"使用压强无效化修正：{request.modDescription}？";
            if (currentValueText != null)
                currentValueText.text = $"当前骰子值：{request.currentDiceValue}";

            SetupConfirmSkip(
                "无效化", "不使用",
                () => onComplete(true),
                () => onComplete(false)
            );
        }

        private void SetupTwoButtons(string plusLabel, string minusLabel, string skipLabel,
            Action onPlus, Action onMinus, Action onSkip)
        {
            ClearListeners();

            if (plusButton != null)
            {
                plusButton.gameObject.SetActive(true);
                plusButton.GetComponentInChildren<Text>().text = plusLabel;
                plusButton.onClick.AddListener(() => onPlus());
            }
            if (minusButton != null)
            {
                minusButton.gameObject.SetActive(true);
                minusButton.GetComponentInChildren<Text>().text = minusLabel;
                minusButton.onClick.AddListener(() => onMinus());
            }
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.GetComponentInChildren<Text>().text = skipLabel;
                skipButton.onClick.AddListener(() => onSkip());
            }
            if (useButton != null)
                useButton.gameObject.SetActive(false);
        }

        private void SetupConfirmSkip(string confirmLabel, string skipLabel,
            Action onConfirm, Action onSkip)
        {
            ClearListeners();

            if (useButton != null)
            {
                useButton.gameObject.SetActive(true);
                useButton.GetComponentInChildren<Text>().text = confirmLabel;
                useButton.onClick.AddListener(() => onConfirm());
            }
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                skipButton.GetComponentInChildren<Text>().text = skipLabel;
                skipButton.onClick.AddListener(() => onSkip());
            }
            if (plusButton != null) plusButton.gameObject.SetActive(false);
            if (minusButton != null) minusButton.gameObject.SetActive(false);
        }

        private void ClearListeners()
        {
            if (plusButton != null) plusButton.onClick.RemoveAllListeners();
            if (minusButton != null) minusButton.onClick.RemoveAllListeners();
            if (skipButton != null) skipButton.onClick.RemoveAllListeners();
            if (useButton != null) useButton.onClick.RemoveAllListeners();
        }
    }
}
