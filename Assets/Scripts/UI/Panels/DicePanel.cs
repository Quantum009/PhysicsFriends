// ============================================================
// DicePanel.cs — 骰子面板：展示投骰动画和结果
// ============================================================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Player;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    /// <summary>
    /// 骰子面板：展示投骰结果、动画效果。
    /// 支持普通展示和薛定谔重投询问。
    /// </summary>
    public class DicePanel : MonoBehaviour
    {
        [Header("骰子显示")]
        [SerializeField] private Text diceValueText;       // 骰子点数大字
        [SerializeField] private Text contextText;         // 上下文描述
        [SerializeField] private Text playerNameText;      // 投骰玩家名

        [Header("按钮")]
        [SerializeField] private Button confirmButton;     // 确认按钮
        [SerializeField] private Button rerollButton;      // 重投按钮（薛定谔）
        [SerializeField] private Button keepButton;        // 保留按钮（薛定谔）

        [Header("动画")]
        [SerializeField] private float rollAnimDuration = 0.8f; // 投骰动画时长

        // 回调
        private Action _onConfirm;
        private Action<bool> _onRerollChoice;

        private void Awake()
        {
            if (confirmButton != null)
                confirmButton.onClick.AddListener(OnConfirmClick);
            if (rerollButton != null)
                rerollButton.onClick.AddListener(() => OnRerollClick(true));
            if (keepButton != null)
                keepButton.onClick.AddListener(() => OnRerollClick(false));
        }

        /// <summary>展示骰子结果（只有确认按钮）</summary>
        public void ShowRoll(DiceRollRequest request, Action onConfirm)
        {
            _onConfirm = onConfirm;

            if (playerNameText != null)
                playerNameText.text = request.player.playerName;
            if (contextText != null)
                contextText.text = request.context ?? "投骰";

            // 显示确认按钮，隐藏重投按钮
            SetButtonActive(confirmButton, true);
            SetButtonActive(rerollButton, false);
            SetButtonActive(keepButton, false);

            // 播放投骰动画
            StartCoroutine(RollAnimation(request.result));
        }

        /// <summary>询问是否重投（薛定谔技能）</summary>
        public void AskReroll(DiceRollRequest request, Action<bool> onChoice)
        {
            _onRerollChoice = onChoice;

            if (playerNameText != null)
                playerNameText.text = $"{request.player.playerName}（薛定谔技能）";
            if (contextText != null)
                contextText.text = "是否重投骰子？";

            // 显示重投/保留按钮，隐藏确认按钮
            SetButtonActive(confirmButton, false);
            SetButtonActive(rerollButton, true);
            SetButtonActive(keepButton, true);

            StartCoroutine(RollAnimation(request.result));
        }

        /// <summary>投骰动画协程</summary>
        private IEnumerator RollAnimation(int finalValue)
        {
            float elapsed = 0f;
            while (elapsed < rollAnimDuration)
            {
                if (diceValueText != null)
                    diceValueText.text = UnityEngine.Random.Range(1, 7).ToString();
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (diceValueText != null)
                diceValueText.text = finalValue.ToString();
        }

        private void OnConfirmClick()
        {
            _onConfirm?.Invoke();
            _onConfirm = null;
        }

        private void OnRerollClick(bool wantsReroll)
        {
            _onRerollChoice?.Invoke(wantsReroll);
            _onRerollChoice = null;
        }

        private void SetButtonActive(Button btn, bool active)
        {
            if (btn != null) btn.gameObject.SetActive(active);
        }
    }
}
