// ============================================================
// NotificationPanel.cs — 通知/日志面板
// 在屏幕侧边显示游戏事件消息流
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class NotificationPanel : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Transform messageContainer;
        [SerializeField] private GameObject messagePrefab;
        [SerializeField] private ScrollRect scrollRect;

        [Header("设置")]
        [SerializeField] private int maxMessages = 50;
        [SerializeField] private float defaultDuration = 3f;
        [SerializeField] private float fadeDuration = 0.5f;

        private Queue<GameObject> _messagePool = new Queue<GameObject>();

        /// <summary>显示通知（不阻塞）</summary>
        public void ShowNotification(GameNotification notification)
        {
            CreateMessageEntry(notification);
        }

        /// <summary>显示通知并在淡出后回调</summary>
        public void ShowNotificationWithCallback(GameNotification notification, Action onComplete)
        {
            CreateMessageEntry(notification);
            float dur = notification.duration > 0 ? notification.duration : 1f;
            StartCoroutine(WaitThenCallback(dur, onComplete));
        }

        private void CreateMessageEntry(GameNotification notification)
        {
            GameObject obj;
            if (messagePrefab != null && messageContainer != null)
            {
                obj = Instantiate(messagePrefab, messageContainer);
            }
            else
            {
                obj = new GameObject("Msg");
                obj.transform.SetParent(messageContainer ?? transform);
                var rt = obj.AddComponent<RectTransform>();
                rt.sizeDelta = new Vector2(300, 30);
                obj.AddComponent<Text>();
            }

            var text = obj.GetComponentInChildren<Text>();
            if (text != null)
            {
                string prefix = notification.player != null ? $"[{notification.player.playerName}] " : "";
                text.text = $"{prefix}{notification.message}";
                text.color = GetNotificationColor(notification.type);
            }

            // 限制消息数量
            if (messageContainer != null && messageContainer.childCount > maxMessages)
            {
                Destroy(messageContainer.GetChild(0).gameObject);
            }

            // 自动滚动到底部
            if (scrollRect != null)
                StartCoroutine(ScrollToBottom());

            // 自动淡出（可选）
            float dur = notification.duration > 0 ? notification.duration : defaultDuration;
            if (dur > 0 && dur < 999)
                StartCoroutine(FadeOut(obj, dur));
        }

        private Color GetNotificationColor(NotificationType type)
        {
            switch (type)
            {
                case NotificationType.Achievement: return new Color(1f, 0.85f, 0f);
                case NotificationType.Victory:     return new Color(1f, 0.5f, 0f);
                case NotificationType.Warning:     return new Color(1f, 0.3f, 0.3f);
                case NotificationType.EraChange:   return new Color(0.5f, 0.8f, 1f);
                case NotificationType.MolChange:   return new Color(0.5f, 1f, 0.5f);
                case NotificationType.CardGained:  return new Color(0.7f, 0.9f, 1f);
                case NotificationType.CardLost:    return new Color(1f, 0.7f, 0.7f);
                default: return Color.white;
            }
        }

        private IEnumerator ScrollToBottom()
        {
            yield return new WaitForEndOfFrame();
            if (scrollRect != null)
                scrollRect.normalizedPosition = Vector2.zero;
        }

        private IEnumerator FadeOut(GameObject obj, float delay)
        {
            yield return new WaitForSeconds(delay);
            // 在日志模式下不淡出，保留消息
            // 如果需要淡出可在此实现CanvasGroup alpha动画
        }

        private IEnumerator WaitThenCallback(float seconds, Action callback)
        {
            yield return new WaitForSeconds(seconds);
            callback?.Invoke();
        }
    }
}
