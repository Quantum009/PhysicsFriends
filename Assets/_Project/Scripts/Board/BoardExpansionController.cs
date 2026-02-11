// ============================================================
// BoardExpansionController.cs — 棋盘展开动画控制器
// 当纪元从「自然哲学」→「经典物理」时：
//   1. 内圈24格淡出 + 缩小
//   2. 相机拉远（Size 6 → 10）
//   3. 外圈72格从中心扩散滑入
//   4. 棋子传送到新位置（内格i → 外格i×3）
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Board;
using PhysicsFriends.Player;

namespace PhysicsFriends.Board
{
    public class BoardExpansionController : MonoBehaviour
    {
        [Header("引用")]
        [SerializeField] private Transform boardParent;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private GameObject tilePrefab;

        [Header("动画参数")]
        [SerializeField] private float fadeOutDuration = 0.8f;
        [SerializeField] private float cameraZoomDuration = 1.0f;
        [SerializeField] private float slideInDuration = 1.2f;
        [SerializeField] private float slideInDelay = 0.02f; // 每格之间的延迟
        [SerializeField] private float targetCameraSize = 10f;

        // 内部状态
        private List<GameObject> _innerTiles = new();
        private List<GameObject> _outerTiles = new();
        private bool _isExpanded = false;

        public bool IsExpanded => _isExpanded;

        /// <summary>记录内圈格子对象（由 GameManager.Setup2DBoard 调用）</summary>
        public void RegisterInnerTile(GameObject tileObj)
        {
            _innerTiles.Add(tileObj);
        }

        /// <summary>
        /// 执行棋盘展开动画的完整协程
        /// </summary>
        public IEnumerator ExpandBoardAsync(
            BoardManager board,
            List<PlayerState> players,
            List<GameObject> playerTokens,
            System.Func<int, Vector2> getTileWorldPos)
        {
            if (_isExpanded) yield break;
            _isExpanded = true;

            Debug.Log("[BoardExpansion] 开始棋盘展开动画");

            // ---- 第1步：内圈格子淡出 ----
            yield return FadeOutInnerTiles();

            // ---- 第2步：相机拉远 ----
            yield return ZoomCamera();

            // ---- 第3步：生成外圈格子 + 滑入动画 ----
            yield return GenerateAndSlideInOuterTiles(board, getTileWorldPos);

            // ---- 第4步：移动棋子到新位置 ----
            yield return RelocatePawns(players, playerTokens, board, getTileWorldPos);

            Debug.Log("[BoardExpansion] 棋盘展开动画完成");
        }

        // ================================================================
        // 步骤实现
        // ================================================================

        private IEnumerator FadeOutInnerTiles()
        {
            float elapsed = 0f;

            // 记录每个格子的初始颜色
            var initialColors = new List<Color>();
            foreach (var tile in _innerTiles)
            {
                var sr = tile.GetComponent<SpriteRenderer>();
                initialColors.Add(sr != null ? sr.color : Color.white);
            }

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / fadeOutDuration;
                float alpha = Mathf.Lerp(1f, 0f, t);
                float scale = Mathf.Lerp(1f, 0.5f, t);

                for (int i = 0; i < _innerTiles.Count; i++)
                {
                    if (_innerTiles[i] == null) continue;

                    var sr = _innerTiles[i].GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        var c = initialColors[i];
                        c.a = alpha;
                        sr.color = c;
                    }

                    _innerTiles[i].transform.localScale = Vector3.one * (0.7f * scale);
                }
                yield return null;
            }

            // 销毁内圈格子
            foreach (var tile in _innerTiles)
            {
                if (tile != null) Destroy(tile);
            }
            _innerTiles.Clear();
        }

        private IEnumerator ZoomCamera()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) yield break;

            float startSize = mainCamera.orthographicSize;
            float elapsed = 0f;

            while (elapsed < cameraZoomDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / cameraZoomDuration);
                mainCamera.orthographicSize = Mathf.Lerp(startSize, targetCameraSize, t);
                yield return null;
            }
            mainCamera.orthographicSize = targetCameraSize;
        }

        private IEnumerator GenerateAndSlideInOuterTiles(
            BoardManager board,
            System.Func<int, Vector2> getTileWorldPos)
        {
            int totalTiles = board.TotalTiles; // 展开后应为72

            for (int i = 0; i < totalTiles; i++)
            {
                var tile = board.GetTile(i);
                if (tile == null) continue;

                Vector2 targetPos = getTileWorldPos(i);

                // 从中心(0,0)滑入到目标位置
                var tileObj = Instantiate(tilePrefab, boardParent);
                tileObj.name = $"OuterTile_{i}_{tile.tileType}";
                tileObj.transform.localPosition = Vector3.zero; // 起始在中心
                tileObj.transform.localScale = Vector3.zero;    // 起始缩放为0

                // 设置颜色
                var sr = tileObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = GetTileColor(tile.tileType, tile.colorId);
                }

                _outerTiles.Add(tileObj);

                // 启动每个格子的滑入动画（错开启动）
                StartCoroutine(SlideInTile(tileObj, targetPos, slideInDuration));
                yield return new WaitForSeconds(slideInDelay);
            }

            // 等待最后一个格子动画结束
            yield return new WaitForSeconds(slideInDuration);
        }

        private IEnumerator SlideInTile(GameObject tileObj, Vector2 targetPos, float duration)
        {
            float elapsed = 0f;
            Vector3 start = Vector3.zero;
            Vector3 end = new Vector3(targetPos.x, targetPos.y, 0f);
            Vector3 targetScale = new Vector3(0.7f, 0.7f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                tileObj.transform.localPosition = Vector3.Lerp(start, end, t);
                tileObj.transform.localScale = Vector3.Lerp(Vector3.zero, targetScale, t);
                yield return null;
            }
            tileObj.transform.localPosition = end;
            tileObj.transform.localScale = targetScale;
        }

        private IEnumerator RelocatePawns(
            List<PlayerState> players,
            List<GameObject> playerTokens,
            BoardManager board,
            System.Func<int, Vector2> getTileWorldPos)
        {
            for (int i = 0; i < players.Count && i < playerTokens.Count; i++)
            {
                // 内格 idx → 外格 idx×3
                int oldPos = players[i].position;
                int newPos = oldPos * 3;
                if (newPos >= board.TotalTiles) newPos = 0;
                players[i].position = newPos;

                Vector2 targetPos = getTileWorldPos(newPos);
                float offset = i * 0.25f;

                // 缩小→传送→放大动画
                yield return TeleportPawn(playerTokens[i], targetPos, offset);
            }
        }

        private IEnumerator TeleportPawn(GameObject pawn, Vector2 targetPos, float offset)
        {
            // 缩小
            float duration = 0.3f;
            float elapsed = 0f;
            Vector3 startScale = pawn.transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                pawn.transform.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                yield return null;
            }

            // 传送
            pawn.transform.localPosition = new Vector3(
                targetPos.x + offset, targetPos.y + offset, 0f);

            // 放大
            elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                pawn.transform.localScale = Vector3.Lerp(Vector3.zero, startScale, t);
                yield return null;
            }
            pawn.transform.localScale = startScale;
        }

        // ================================================================
        // 工具
        // ================================================================

        private Color GetTileColor(TileType type, int colorId)
        {
            Color baseColor = type switch
            {
                TileType.Start     => new Color(1f, 0.9f, 0.3f),
                TileType.Territory => new Color(0.7f, 0.85f, 0.7f),
                TileType.Shop      => new Color(0.6f, 0.75f, 1f),
                TileType.Reward    => new Color(1f, 0.8f, 0.5f),
                TileType.Event     => new Color(0.9f, 0.5f, 0.5f),
                TileType.Supply    => new Color(0.5f, 0.9f, 0.7f),
                _                  => Color.white
            };

            // 有归属颜色的格子（colorId > 0）：混合玩家颜色
            if (colorId > 0 &&
                (type == TileType.Territory || type == TileType.Shop || type == TileType.Supply))
            {
                var ownerColor = BoardTile.ColorIdToPlayerColor(colorId);
                Color pc = ownerColor switch
                {
                    PlayerColor.Red    => Color.red,
                    PlayerColor.Blue   => Color.blue,
                    PlayerColor.Green  => Color.green,
                    PlayerColor.Yellow => Color.yellow,
                    _                  => Color.white
                };
                baseColor = Color.Lerp(pc, Color.white, 0.6f);
            }
            return baseColor;
        }
    }
}
