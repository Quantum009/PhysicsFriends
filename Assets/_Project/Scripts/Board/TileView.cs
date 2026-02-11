// ============================================================
// TileView.cs — 格子视觉组件：显示类型、高亮、建筑图标
// ============================================================
using UnityEngine;
using System.Collections;
using PhysicsFriends.Core;

namespace PhysicsFriends.Board
{
    [RequireComponent(typeof(SpriteRenderer))]
    public class TileView : MonoBehaviour
    {
        public int TileIndex { get; private set; }
        public TileType TileType { get; private set; }
        public int OwnerColorIndex { get; private set; }  // -1 = 无归属
        public Vector2 WorldPosition { get; private set; }

        [Header("子物体引用（可选）")]
        [SerializeField] private SpriteRenderer highlightOverlay;
        [SerializeField] private SpriteRenderer buildingIcon;
        [SerializeField] private TMPro.TextMeshPro indexLabel;

        private SpriteRenderer _sr;
        private Color _baseColor;
        private bool _isHighlighted;

        public void Initialize(int index, TileType type, int ownerColor, Vector2 worldPos)
        {
            TileIndex = index;
            TileType = type;
            OwnerColorIndex = ownerColor;
            WorldPosition = worldPos;

            _sr = GetComponent<SpriteRenderer>();
            if (_sr != null) _baseColor = _sr.color;

            // 显示索引标签
            if (indexLabel != null)
                indexLabel.text = index.ToString();
        }

        public void SetHighlight(bool on, Color? color = null)
        {
            _isHighlighted = on;
            if (highlightOverlay != null)
            {
                highlightOverlay.enabled = on;
                if (on && color.HasValue)
                    highlightOverlay.color = color.Value;
            }
            else if (_sr != null)
            {
                _sr.color = on ? Color.Lerp(_baseColor, Color.white, 0.5f) : _baseColor;
            }
        }

        public void ShowBuilding(Sprite icon)
        {
            if (buildingIcon != null)
            {
                buildingIcon.sprite = icon;
                buildingIcon.enabled = icon != null;
            }
        }

        public void FadeOut(float duration)
        {
            if (_sr != null)
                StartCoroutine(FadeAlpha(_sr.color.a, 0f, duration));
        }

        public void FadeIn(float duration)
        {
            if (_sr != null)
            {
                var c = _sr.color; c.a = 0f; _sr.color = c;
                StartCoroutine(FadeAlpha(0f, 1f, duration));
            }
        }

        private IEnumerator FadeAlpha(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                var c = _sr.color;
                c.a = Mathf.Lerp(from, to, t);
                _sr.color = c;
                yield return null;
            }
            var final_ = _sr.color;
            final_.a = to;
            _sr.color = final_;
        }
    }
}
