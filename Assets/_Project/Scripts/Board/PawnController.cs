// ============================================================
// PawnController.cs — 棋子控制器：移动动画、位置同步
// 网络版本使用 NetworkBehaviour 同步位置
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PhysicsFriends.Core;

namespace PhysicsFriends.Board
{
    public class PawnController : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private float moveSpeed = 8f;        // 格/秒
        [SerializeField] private float hopHeight = 0.3f;      // 跳跃高度
        [SerializeField] private float teleportDuration = 0.5f;

        public int PlayerIndex { get; private set; }
        public int CurrentTileIndex { get; private set; }
        public bool IsMoving { get; private set; }

        private Vector3 _baseScale;

        public void Initialize(int playerIndex, PlayerColor color, Sprite pawnSprite = null)
        {
            PlayerIndex = playerIndex;
            CurrentTileIndex = 0;
            _baseScale = transform.localScale;

            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();

            if (spriteRenderer != null)
            {
                if (pawnSprite != null) spriteRenderer.sprite = pawnSprite;
                spriteRenderer.color = GetPlayerColor(color);
                spriteRenderer.sortingOrder = 10 + playerIndex; // 在格子之上
            }

            // 微调偏移避免棋子重叠
            float offsetAngle = playerIndex * (Mathf.PI * 2f / 4f);
            Vector3 offset = new Vector3(
                Mathf.Cos(offsetAngle) * 0.2f,
                Mathf.Sin(offsetAngle) * 0.2f,
                0
            );
            transform.localPosition += offset;
        }

        /// <summary>逐格移动动画，每走一格回调一次</summary>
        public IEnumerator MoveStepByStep(List<Vector2> tilePath,
            Func<int, bool> onEachStep = null)
        {
            IsMoving = true;

            for (int i = 0; i < tilePath.Count; i++)
            {
                Vector3 target = tilePath[i];
                yield return MoveToPosition(target);

                CurrentTileIndex = i; // 由调用者更新实际index

                // 每步回调
                if (onEachStep != null && onEachStep(i))
                    break; // 被阻挡
            }

            IsMoving = false;
        }

        /// <summary>直接移动到目标位置（带跳跃弧线）</summary>
        public IEnumerator MoveToPosition(Vector3 target)
        {
            Vector3 start = transform.position;
            float distance = Vector3.Distance(start, target);
            float duration = distance / moveSpeed;
            duration = Mathf.Max(duration, 0.1f);

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // 线性插值 + 抛物线跳跃
                Vector3 pos = Vector3.Lerp(start, target, t);
                float hop = hopHeight * 4f * t * (1f - t); // 抛物线
                pos.y += hop;

                transform.position = pos;
                yield return null;
            }

            transform.position = target;
        }

        /// <summary>传送动画（量子隧穿/虫洞）</summary>
        public IEnumerator TeleportTo(Vector3 target)
        {
            IsMoving = true;

            // 缩小消失
            float half = teleportDuration / 2f;
            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                transform.localScale = Vector3.Lerp(_baseScale, Vector3.zero, t);
                spriteRenderer.color = new Color(
                    spriteRenderer.color.r, spriteRenderer.color.g,
                    spriteRenderer.color.b, 1f - t);
                yield return null;
            }

            // 瞬移
            transform.position = target;

            // 放大出现
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / half;
                transform.localScale = Vector3.Lerp(Vector3.zero, _baseScale, t);
                spriteRenderer.color = new Color(
                    spriteRenderer.color.r, spriteRenderer.color.g,
                    spriteRenderer.color.b, t);
                yield return null;
            }

            transform.localScale = _baseScale;
            IsMoving = false;
        }

        /// <summary>抖动效果（受到攻击/眩晕）</summary>
        public IEnumerator Shake(float intensity = 0.15f, float duration = 0.4f)
        {
            Vector3 original = transform.localPosition;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float x = UnityEngine.Random.Range(-intensity, intensity);
                float y = UnityEngine.Random.Range(-intensity, intensity);
                transform.localPosition = original + new Vector3(x, y, 0);
                yield return null;
            }
            transform.localPosition = original;
        }

        private Color GetPlayerColor(PlayerColor pc)
        {
            return pc switch
            {
                PlayerColor.Red => new Color(0.9f, 0.2f, 0.2f),
                PlayerColor.Blue => new Color(0.2f, 0.4f, 0.9f),
                PlayerColor.Green => new Color(0.2f, 0.8f, 0.3f),
                PlayerColor.Yellow => new Color(0.95f, 0.85f, 0.2f),
                _ => Color.white
            };
        }
    }
}
