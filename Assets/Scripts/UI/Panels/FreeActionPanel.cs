// ============================================================
// FreeActionPanel.cs — 自由行动阶段菜单
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class FreeActionPanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Button endTurnButton;
        [SerializeField] private Button useActiveButton;
        [SerializeField] private Button synthesizeButton;
        [SerializeField] private Button innovationButton;
        [SerializeField] private Button discardButton;

        private Action<FreeActionResponse> _onComplete;

        private void Awake()
        {
            if (endTurnButton != null)
                endTurnButton.onClick.AddListener(() => Complete(FreeActionType.EndTurn));
            if (useActiveButton != null)
                useActiveButton.onClick.AddListener(() => Complete(FreeActionType.UseActiveCard));
            if (synthesizeButton != null)
                synthesizeButton.onClick.AddListener(() => Complete(FreeActionType.Synthesize));
            if (innovationButton != null)
                innovationButton.onClick.AddListener(() => Complete(FreeActionType.InnovationProject));
            if (discardButton != null)
                discardButton.onClick.AddListener(() => Complete(FreeActionType.DiscardCard));
        }

        public void Show(FreeActionRequest request, Action<FreeActionResponse> onComplete)
        {
            _onComplete = onComplete;

            if (titleText != null)
                titleText.text = $"{request.player.playerName} — 自由行动";

            var avail = request.availableActions ?? new List<FreeActionType>();
            SetButtonActive(useActiveButton, avail.Contains(FreeActionType.UseActiveCard));
            SetButtonActive(synthesizeButton, avail.Contains(FreeActionType.Synthesize));
            SetButtonActive(innovationButton, avail.Contains(FreeActionType.InnovationProject));
            SetButtonActive(discardButton, request.mustDiscard || avail.Contains(FreeActionType.DiscardCard));

            if (endTurnButton != null)
                endTurnButton.interactable = !request.mustDiscard;
        }

        private void Complete(FreeActionType action)
        {
            _onComplete?.Invoke(new FreeActionResponse { action = action });
            _onComplete = null;
        }

        private void SetButtonActive(Button btn, bool active)
        {
            if (btn != null) btn.gameObject.SetActive(active);
        }
    }
}
