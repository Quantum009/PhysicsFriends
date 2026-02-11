// ============================================================
// BoardSystem.cs — 棋盘系统：管理格子布局、内/外圈切换
// 使用棋盘数据表中的实际布局数据
// 内圈24格（自然哲学时期），外圈72格（经典物理学及之后）
// type: 1=起点, 2=领地, 3=事件, 4=商店, 5=奖励, 6=补给
// color: 0=无颜色, 1=红色, 2=黄色, 3=蓝色, 4=绿色
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using PhysicsFriends.Core;
using PhysicsFriends.Player;

namespace PhysicsFriends.Board
{
    /// <summary>棋盘上的一个格子</summary>
    [Serializable]
    public class BoardTile
    {
        public int index;                   // 格子在棋盘中的顺序索引
        public int dataId;                  // 棋盘数据表中的原始ID
        public TileType tileType;           // 格子类型
        public PlayerColor ownerColor;      // 领地/商店/补给的所属颜色
        public int colorId;                 // 原始颜色ID (0=无, 1=红, 2=黄, 3=蓝, 4=绿)
        public int ownerIndex = -1;         // 领地所有者玩家索引（-1=无主）
        public float x;                     // 格子的x坐标
        public float y;                     // 格子的y坐标
        public List<int> roadblocks;        // 此格上的路障列表（路障ID）
        public List<BuildingInstance> buildings; // 此格上的建筑列表
        public int fluxSurfaceOwner;        // 磁通量面的所有者玩家索引（-1=无）
        public MoveDirection fluxDirection; // 磁通量面的方向

        /// <summary>是否有路障</summary>
        public bool hasRoadblock => roadblocks.Count > 0;

        public BoardTile(int index, int dataId, TileType type, int colorId, float x, float y)
        {
            this.index = index;
            this.dataId = dataId;
            this.tileType = type;
            this.colorId = colorId;
            this.ownerColor = ColorIdToPlayerColor(colorId);
            this.x = x;
            this.y = y;
            this.roadblocks = new List<int>();
            this.buildings = new List<BuildingInstance>();
            this.fluxSurfaceOwner = -1;
        }

        /// <summary>将颜色ID映射到PlayerColor枚举</summary>
        public static PlayerColor ColorIdToPlayerColor(int colorId)
        {
            switch (colorId)
            {
                case 1: return PlayerColor.Red;
                case 2: return PlayerColor.Yellow;
                case 3: return PlayerColor.Blue;
                case 4: return PlayerColor.Green;
                default: return PlayerColor.Red; // 无颜色的格子默认红色（不影响逻辑）
            }
        }

        /// <summary>将类型ID映射到TileType枚举</summary>
        public static TileType TypeIdToTileType(int typeId)
        {
            switch (typeId)
            {
                case 1: return TileType.Start;
                case 2: return TileType.Territory;
                case 3: return TileType.Event;
                case 4: return TileType.Shop;
                case 5: return TileType.Reward;
                case 6: return TileType.Supply;
                default: return TileType.Territory;
            }
        }

        /// <summary>深拷贝</summary>
        public BoardTile DeepCopy()
        {
            var copy = new BoardTile(index, dataId, tileType, colorId, x, y);
            copy.ownerIndex = this.ownerIndex;
            copy.roadblocks = new List<int>(this.roadblocks);
            copy.buildings = this.buildings.Select(b => b.DeepCopy()).ToList();
            copy.fluxSurfaceOwner = this.fluxSurfaceOwner;
            copy.fluxDirection = this.fluxDirection;
            return copy;
        }
    }

    /// <summary>
    /// 棋盘管理器：负责棋盘布局的生成与管理
    /// 内圈24格（自然哲学时期），外圈72格（经典物理学及之后）
    /// 棋盘数据来自棋盘数据.xls
    /// </summary>
    [Serializable]
    public class BoardManager
    {
        public List<BoardTile> tiles;       // 当前棋盘的所有格子
        public bool isExpanded;             // 棋盘是否已展开
        public int startTileIndex;          // 起点格的索引（固定为0）

        // 路障相关
        private int _nextRoadblockId = 1;
        public int maxRoadblocks = 12;
        public int currentRoadblockCount;

        // 存储外圈数据（在初始化时就加载，展开时使用）
        private List<BoardTile> _outerTiles;

        /// <summary>构造函数：生成内圈棋盘</summary>
        public BoardManager(int playerCount)
        {
            isExpanded = false;
            startTileIndex = 0;
            currentRoadblockCount = 0;
            GenerateInnerBoard();
            GenerateOuterBoardData();
        }

        /// <summary>无参构造函数</summary>
        public BoardManager()
        {
            isExpanded = false;
            startTileIndex = 0;
            currentRoadblockCount = 0;
            tiles = new List<BoardTile>();
        }

        // =============================================================
        // 棋盘数据定义（来自棋盘数据.xls）
        // 内圈: id以100为步长 (0,100,200,...,2300), 共24格
        // 外圈: id为1~71 + 起点0, 共72格
        // type: 1=起点, 2=领地, 3=事件, 4=商店, 5=奖励, 6=补给
        // color: 0=无颜色, 1=红色, 2=黄色, 3=蓝色, 4=绿色
        // =============================================================

        /// <summary>
        /// 生成内圈棋盘（24格）
        /// 数据来自Excel内圈sheet的前24行（id=0,100,200,...,2300）
        /// </summary>
        private void GenerateInnerBoard()
        {
            tiles = new List<BoardTile>();

            // 内圈数据: (dataId, typeId, colorId, x, y)
            // 来自棋盘数据.xls内圈sheet
            var innerData = new (int id, int type, int color, float x, float y)[]
            {
                (0,    1, 0,  0,  0),   // 起点
                (100,  2, 1,  0,  1),   // 领地-红
                (200,  4, 1,  0,  2),   // 商店-红
                (300,  6, 1,  0,  3),   // 补给-红
                (400,  4, 1,  0,  4),   // 商店-红
                (500,  2, 1,  0,  5),   // 领地-红
                (600,  5, 0,  0,  6),   // 奖励
                (700,  2, 2, -1,  6),   // 领地-黄
                (800,  4, 2, -2,  6),   // 商店-黄
                (900,  6, 2, -3,  6),   // 补给-黄
                (1000, 4, 2, -4,  6),   // 商店-黄
                (1100, 2, 2, -5,  6),   // 领地-黄
                (1200, 5, 0, -6,  6),   // 奖励
                (1300, 1, 2, -6,  5),   // 起点(应为领地?)→按数据type=1
                (1400, 4, 2, -6,  4),   // 商店-黄
                (1500, 6, 2, -6,  3),   // 补给-黄
                (1600, 4, 2, -6,  2),   // 商店-黄
                (1700, 2, 2, -6,  1),   // 领地-黄
                (1800, 5, 0, -6,  0),   // 奖励
                (1900, 1, 2, -5,  0),   // 起点(应为领地?)→按数据type=1
                (2000, 4, 2, -4,  0),   // 商店-黄
                (2100, 6, 2, -3,  0),   // 补给-黄
                (2200, 4, 2, -2,  0),   // 商店-黄
                (2300, 2, 2, -1,  0),   // 领地-黄
            };

            for (int i = 0; i < innerData.Length; i++)
            {
                var d = innerData[i];
                var tileType = BoardTile.TypeIdToTileType(d.type);
                tiles.Add(new BoardTile(i, d.id, tileType, d.color, d.x, d.y));
            }
        }

        /// <summary>
        /// 预加载外圈棋盘数据（72格）
        /// 数据来自Excel内圈sheet的后72行（id=0~71）
        /// </summary>
        private void GenerateOuterBoardData()
        {
            _outerTiles = new List<BoardTile>();

            // 外圈数据: (dataId, typeId, colorId, x, y)
            var outerData = new (int id, int type, int color, float x, float y)[]
            {
                // 第0格：起点（与内圈起点一致）
                (0,  1, 0,  0,  0),   // 起点
                (1,  2, 4,  1,  0),
                (2,  2, 3,  2,  0),   // 领地-蓝
                (3,  3, 0,  3,  0),   // 事件
                (4,  2, 2,  4,  0),   // 领地-黄
                (5,  4, 1,  5,  0),   // 商店-红
                (6,  5, 0,  6,  0),   // 奖励
                (7,  4, 4,  6,  1),   // 商店-绿
                (8,  2, 3,  6,  2),   // 领地-蓝
                (9,  3, 0,  6,  3),   // 事件
                (10, 2, 2,  6,  4),   // 领地-黄
                (11, 2, 1,  6,  5),   // 领地-红
                (12, 5, 0,  6,  6),   // 奖励
                (13, 2, 4,  5,  6),   // 领地-绿
                (14, 4, 3,  4,  6),   // 商店-蓝
                (15, 3, 0,  3,  6),   // 事件
                (16, 2, 2,  2,  6),   // 领地-黄
                (17, 2, 1,  1,  6),   // 领地-红
                (18, 5, 0,  0,  6),   // 奖励
                (19, 2, 4,  0,  7),   // 领地-绿
                (20, 2, 3,  0,  8),   // 领地-蓝
                (21, 3, 0,  0,  9),   // 事件
                (22, 4, 2,  0, 10),   // 商店-黄
                (23, 2, 1,  0, 11),   // 领地-红
                (24, 5, 0,  0, 12),   // 奖励
                (25, 2, 4, -1, 12),   // 领地-绿
                (26, 2, 3, -2, 12),   // 领地-蓝
                (27, 3, 0, -3, 12),   // 事件
                (28, 2, 2, -4, 12),   // 领地-黄
                (29, 4, 1, -5, 12),   // 商店-红
                (30, 5, 0, -6, 12),   // 奖励
                (31, 4, 4, -6, 11),   // 商店-绿
                (32, 2, 3, -6, 10),   // 领地-蓝
                (33, 3, 0, -6,  9),   // 事件
                (34, 2, 2, -6,  8),   // 领地-黄
                (35, 2, 1, -6,  7),   // 领地-红
                (36, 5, 0, -6,  6),   // 奖励
                (37, 2, 4, -7,  6),   // 领地-绿
                (38, 4, 3, -8,  6),   // 商店-蓝
                (39, 3, 0, -9,  6),   // 事件
                (40, 2, 2,-10,  6),   // 领地-黄
                (41, 2, 1,-11,  6),   // 领地-红
                (42, 5, 0,-12,  6),   // 奖励
                (43, 2, 4,-12,  5),   // 领地-绿
                (44, 2, 3,-12,  4),   // 领地-蓝
                (45, 3, 0,-12,  3),   // 事件
                (46, 4, 2,-12,  2),   // 商店-黄
                (47, 2, 1,-12,  1),   // 领地-红
                (48, 5, 0,-12,  0),   // 奖励
                (49, 2, 4,-11,  0),   // 领地-绿
                (50, 2, 3,-10,  0),   // 领地-蓝
                (51, 3, 0, -9,  0),   // 事件
                (52, 2, 2, -8,  0),   // 领地-黄
                (53, 4, 1, -7,  0),   // 商店-红
                (54, 5, 0, -6,  0),   // 奖励
                (55, 4, 4, -6, -1),   // 商店-绿
                (56, 2, 3, -6, -2),   // 领地-蓝
                (57, 3, 0, -6, -3),   // 事件
                (58, 2, 2, -6, -4),   // 领地-黄
                (59, 2, 1, -6, -5),   // 领地-红
                (60, 5, 0, -6, -6),   // 奖励
                (61, 2, 4, -5, -6),   // 领地-绿
                (62, 4, 3, -4, -6),   // 商店-蓝
                (63, 3, 0, -3, -6),   // 事件
                (64, 2, 2, -2, -6),   // 领地-黄
                (65, 2, 1, -1, -6),   // 领地-红
                (66, 5, 0,  0, -6),   // 奖励
                (67, 2, 4,  0, -5),   // 领地-绿
                (68, 2, 3,  0, -4),   // 领地-蓝
                (69, 3, 0,  0, -3),   // 事件
                (70, 4, 2,  0, -2),   // 商店-黄
                (71, 2, 1,  0, -1),   // 领地-红
            };

            // 直接从outerData构建外圈（72格，索引0~71）
            for (int i = 0; i < outerData.Length; i++)
            {
                var d = outerData[i];
                var tileType = BoardTile.TypeIdToTileType(d.type);
                _outerTiles.Add(new BoardTile(i, d.id, tileType, d.color, d.x, d.y));
            }
        }

        /// <summary>
        /// 展开棋盘：从内圈24格扩展到外圈72格
        /// 在自然哲学→经典物理学时代转换时调用
        /// 展开后没有补给格，以事件格取代之
        /// 清除小圈上的所有东西（路障、建筑、磁通量面），
        /// 所有玩家位置重置到起点（不触发经过起点效果）
        /// </summary>
        public void ExpandBoard(int playerCount, List<PlayerState> players)
        {
            if (isExpanded) return;

            // 直接替换为干净的外圈数据，小圈上的路障/建筑/磁通量面全部丢弃
            tiles = _outerTiles.Select(t => t.DeepCopy()).ToList();
            currentRoadblockCount = 0;

            // 所有玩家拉到起点（不触发经过起点的被动效果）
            foreach (var player in players)
            {
                player.position = 0;
                // 同时清除玩家身上与小圈建筑相关的记录
                player.buildings.Clear();
            }

            isExpanded = true;
        }

        /// <summary>获取棋盘当前的格子总数</summary>
        public int TileCount => tiles.Count;
        public int TotalTiles => tiles.Count;

        /// <summary>重新初始化棋盘（兼容无参构造后调用）</summary>
        public void InitializeBoard(int tileCount)
        {
            isExpanded = false;
            startTileIndex = 0;
            currentRoadblockCount = 0;

            if (tileCount == 24)
            {
                GenerateInnerBoard();
                GenerateOuterBoardData();
            }
            else
            {
                // 后备方案：如果请求的不是24格，生成通用棋盘
                tiles = new List<BoardTile>();
                for (int i = 0; i < tileCount; i++)
                {
                    TileType type;
                    if (i == 0) type = TileType.Start;
                    else if (i % 6 == 1 || i % 6 == 2) type = TileType.Territory;
                    else if (i % 6 == 3) type = TileType.Shop;
                    else type = TileType.Reward;

                    float angle = (360f / tileCount) * i;
                    float rad = angle * (float)(Math.PI / 180.0);
                    float x = (float)Math.Cos(rad) * 4f;
                    float y = (float)Math.Sin(rad) * 4f;

                    tiles.Add(new BoardTile(i, i, type, 0, x, y));
                }
                GenerateOuterBoardData();
            }
        }

        /// <summary>棋盘扩展（无playerCount版本）</summary>
        public void ExpandBoard(List<PlayerState> players)
        {
            ExpandBoard(players.Count, players);
        }

        /// <summary>获取指定索引的格子</summary>
        public BoardTile GetTile(int index)
        {
            int wrappedIndex = ((index % TileCount) + TileCount) % TileCount;
            return tiles[wrappedIndex];
        }

        /// <summary>
        /// 计算从当前位置移动steps步后的新位置
        /// </summary>
        public int CalculateNewPosition(int currentPos, int steps, MoveDirection direction)
        {
            int delta = direction == MoveDirection.Clockwise ? steps : -steps;
            int newPos = (currentPos + delta) % TileCount;
            if (newPos < 0) newPos += TileCount;
            return newPos;
        }

        /// <summary>
        /// 判断从oldPos走到newPos是否经过起点
        /// 规则书：恰好停留在起点也算经过起点。从起点开始运动不算经过起点。
        /// 使用逐步遍历确保正确性，避免绕回误判。
        /// </summary>
        public bool PassedStart(int oldPos, int newPos, MoveDirection direction)
        {
            if (oldPos == 0) return false; // 从起点出发不算经过起点
            if (newPos == 0) return true;  // 恰好停留在起点算经过

            // 使用逐步遍历判断是否经过0
            int step = direction == MoveDirection.Clockwise ? 1 : -1;
            int pos = oldPos;
            // 安全上限：最多遍历整个棋盘
            for (int i = 0; i < TileCount; i++)
            {
                pos = ((pos + step) % TileCount + TileCount) % TileCount;
                if (pos == 0) return true;  // 经过了起点
                if (pos == newPos) return false; // 到达终点，未经过起点
            }
            return false; // 理论上不会到这里
        }

        /// <summary>在指定格子放置路障</summary>
        public int PlaceRoadblock(int tileIndex)
        {
            if (currentRoadblockCount >= maxRoadblocks) return -1;
            int roadblockId = _nextRoadblockId++;
            GetTile(tileIndex).roadblocks.Add(roadblockId);
            currentRoadblockCount++;
            return roadblockId;
        }

        /// <summary>移除所有路障（超导事件使用）</summary>
        public void RemoveAllRoadblocks()
        {
            foreach (var tile in tiles)
                tile.roadblocks.Clear();
            currentRoadblockCount = 0;
        }

        /// <summary>在指定格子建造建筑</summary>
        public bool PlaceBuilding(BuildingType type, int tileIndex, int ownerIndex)
        {
            var tile = GetTile(tileIndex);
            // 规则书：建筑不可以建造在同一个格子中
            if (tile.buildings.Count > 0) return false;

            var building = new BuildingInstance(type, tileIndex, ownerIndex);
            tile.buildings.Add(building);
            return true;
        }

        /// <summary>获取指定格子上的建筑</summary>
        public BuildingInstance GetBuilding(int tileIndex)
        {
            var tile = GetTile(tileIndex);
            return tile.buildings.FirstOrDefault();
        }

        /// <summary>获取指定格子的2D坐标</summary>
        public (float x, float y) GetTilePosition(int index)
        {
            var tile = GetTile(index);
            return (tile.x, tile.y);
        }

        /// <summary>深拷贝整个棋盘</summary>
        public BoardManager DeepCopy(int playerCount)
        {
            var copy = new BoardManager();
            copy.isExpanded = this.isExpanded;
            copy.startTileIndex = this.startTileIndex;
            copy.currentRoadblockCount = this.currentRoadblockCount;
            copy._nextRoadblockId = this._nextRoadblockId;
            copy.tiles = this.tiles.Select(t => t.DeepCopy()).ToList();
            if (this._outerTiles != null)
                copy._outerTiles = this._outerTiles.Select(t => t.DeepCopy()).ToList();
            return copy;
        }
    }
}
