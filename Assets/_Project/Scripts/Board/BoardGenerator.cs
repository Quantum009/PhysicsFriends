// ============================================================
// BoardGenerator.cs — 棋盘生成器：创建内圈24格/外圈72格的视觉布局
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;

namespace PhysicsFriends.Board
{
    public class BoardGenerator : MonoBehaviour
    {
        [Header("棋盘尺寸")]
        [SerializeField] private float innerRadius = 4f;    // 内圈半径
        [SerializeField] private float outerRadius = 8f;    // 外圈半径
        [SerializeField] private float tileSize = 0.8f;     // 格子大小

        [Header("预制体")]
        [SerializeField] private GameObject tilePrefab;      // 格子预制体
        [SerializeField] private Transform tilesParent;      // 格子父物体

        [Header("格子颜色")]
        [SerializeField] private Color startColor = new Color(1f, 0.9f, 0.5f);   // 起点：金色
        [SerializeField] private Color shopColor = new Color(0.5f, 0.8f, 1f);    // 商店：蓝色
        [SerializeField] private Color territoryColor = new Color(0.5f, 1f, 0.5f);// 领地：绿色
        [SerializeField] private Color supplyColor = new Color(1f, 0.6f, 0.3f);  // 补给：橙色
        [SerializeField] private Color eventColor = new Color(0.8f, 0.5f, 0.8f); // 事件：紫色
        [SerializeField] private Color rewardColor = new Color(1f, 1f, 0.5f);    // 奖励：黄色

        [Header("玩家颜色")]
        [SerializeField] private Color[] playerColors = new Color[]
        {
            Color.red, Color.blue, Color.green, Color.yellow
        };

        // 内圈24格布局：(TileType, 归属颜色索引, -1=无归属)
        // 按规则书棋盘图排列
        private static readonly (int type, int owner)[] InnerLayout = new (int, int)[]
        {
            (0, -1),  // 0: 起点 (Start)
            (2,  0),  // 1: 商店(红)
            (5, -1),  // 2: 奖励
            (2,  1),  // 3: 商店(蓝)
            (1,  0),  // 4: 领地(红)
            (3,  1),  // 5: 补给(蓝)
            (1,  1),  // 6: 领地(蓝)
            (5, -1),  // 7: 奖励
            (2,  2),  // 8: 商店(绿)
            (1,  2),  // 9: 领地(绿)
            (3,  0),  // 10: 补给(红)
            (5, -1),  // 11: 奖励
            (1,  3),  // 12: 领地(黄)
            (2,  3),  // 13: 商店(黄)
            (3,  2),  // 14: 补给(绿)
            (5, -1),  // 15: 奖励
            (1,  0),  // 16: 领地(红)
            (1,  1),  // 17: 领地(蓝)
            (3,  3),  // 18: 补给(黄)
            (5, -1),  // 19: 奖励
            (1,  2),  // 20: 领地(绿)
            (1,  3),  // 21: 领地(黄)
            (5, -1),  // 22: 奖励
            (2,  0),  // 23: 商店(红)
        };

        // TileType映射: 0=Start, 1=Territory, 2=Shop, 3=Supply, 4=Event, 5=Reward

        /// <summary>生成内圈棋盘视觉对象</summary>
        public List<TileView> GenerateInnerBoardVisuals()
        {
            return GenerateBoardVisuals(InnerLayout, innerRadius, 24);
        }

        /// <summary>生成外圈棋盘视觉对象（补给→事件）</summary>
        public List<TileView> GenerateOuterBoardVisuals()
        {
            // 外圈72格：内圈3倍，补给格替换为事件格
            var outerLayout = new (int type, int owner)[72];
            for (int i = 0; i < 72; i++)
            {
                int innerIdx = i / 3; // 每3格对应内圈1格
                int offset = i % 3;

                if (offset == 0)
                {
                    // 对应内圈格
                    var inner = InnerLayout[innerIdx];
                    int type = inner.type;
                    // 补给格在外圈变为事件格
                    if (type == 3) type = 4; // Supply → Event
                    outerLayout[i] = (type, inner.owner);
                }
                else
                {
                    // 新增的插入格：事件格
                    outerLayout[i] = (4, -1); // Event
                }
            }
            // 修正：确保起点仍在0号位
            outerLayout[0] = (0, -1);

            return GenerateBoardVisuals(outerLayout, outerRadius, 72);
        }

        /// <summary>内圈索引→外圈索引映射表</summary>
        public static int InnerToOuterIndex(int innerIdx)
        {
            return innerIdx * 3;
        }

        private List<TileView> GenerateBoardVisuals((int type, int owner)[] layout,
            float radius, int count)
        {
            var views = new List<TileView>();

            // 清除旧格子
            if (tilesParent != null)
            {
                for (int i = tilesParent.childCount - 1; i >= 0; i--)
                    Destroy(tilesParent.GetChild(i).gameObject);
            }

            for (int i = 0; i < count; i++)
            {
                // 圆形布局：均匀分布在圆上
                float angle = (float)i / count * Mathf.PI * 2f;
                // 起点在正上方（90度）
                angle = Mathf.PI / 2f - angle;
                Vector2 pos = new Vector2(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius
                );

                // 创建格子
                GameObject tileObj;
                if (tilePrefab != null)
                {
                    tileObj = Instantiate(tilePrefab, tilesParent ?? transform);
                }
                else
                {
                    tileObj = CreateDefaultTile();
                    tileObj.transform.SetParent(tilesParent ?? transform);
                }

                tileObj.name = $"Tile_{i}";
                tileObj.transform.localPosition = pos;
                tileObj.transform.localScale = Vector3.one * tileSize;

                // 配置 TileView
                var view = tileObj.GetComponent<TileView>();
                if (view == null) view = tileObj.AddComponent<TileView>();

                var data = layout[i];
                var tileType = DataTypeToTileType(data.type);
                view.Initialize(i, tileType, data.owner, pos);

                // 设置颜色
                var sr = tileObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    Color baseColor = GetTileColor(tileType);
                    if (data.owner >= 0 && data.owner < playerColors.Length)
                    {
                        // 混合玩家颜色
                        baseColor = Color.Lerp(baseColor, playerColors[data.owner], 0.3f);
                    }
                    sr.color = baseColor;
                }

                views.Add(view);
            }

            return views;
        }

        private Color GetTileColor(TileType type)
        {
            return type switch
            {
                TileType.Start => startColor,
                TileType.Shop => shopColor,
                TileType.Territory => territoryColor,
                TileType.Supply => supplyColor,
                TileType.Event => eventColor,
                TileType.Reward => rewardColor,
                _ => Color.white
            };
        }

        /// <summary>将数据中的 int 类型ID 转换为 TileType 枚举</summary>
        /// <remarks>数据映射: 0=Start, 1=Territory, 2=Shop, 3=Supply, 4=Event, 5=Reward</remarks>
        private static TileType DataTypeToTileType(int dataType)
        {
            return dataType switch
            {
                0 => TileType.Start,
                1 => TileType.Territory,
                2 => TileType.Shop,
                3 => TileType.Supply,
                4 => TileType.Event,
                5 => TileType.Reward,
                _ => TileType.Territory
            };
        }

        private GameObject CreateDefaultTile()
        {
            var obj = new GameObject();
            var sr = obj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.sortingOrder = 0;
            obj.AddComponent<BoxCollider2D>();
            return obj;
        }

        private static Sprite _cachedSquare;
        private static Sprite CreateSquareSprite()
        {
            if (_cachedSquare != null) return _cachedSquare;
            var tex = new Texture2D(32, 32);
            var colors = new Color32[32 * 32];
            for (int i = 0; i < colors.Length; i++)
                colors[i] = new Color32(255, 255, 255, 255);
            // 边框
            for (int x = 0; x < 32; x++)
                for (int y = 0; y < 32; y++)
                    if (x < 2 || x >= 30 || y < 2 || y >= 30)
                        colors[y * 32 + x] = new Color32(60, 60, 60, 255);
            tex.SetPixels32(colors);
            tex.Apply();
            _cachedSquare = Sprite.Create(tex, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32);
            return _cachedSquare;
        }
    }
}
