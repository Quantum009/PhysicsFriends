// ============================================================
// CardInstance.cs — 卡牌实例：运行时玩家手中的一张具体卡牌
// 每张卡牌实例有唯一ID、状态标记等
// ============================================================
using System;
using PhysicsFriends.Core;
using PhysicsFriends.Data;

namespace PhysicsFriends.Cards
{
    /// <summary>
    /// 运行时的卡牌实例，代表玩家手中的一张具体卡牌
    /// 不同于CardDefinition（静态数据），CardInstance有运行时状态
    /// </summary>
    [Serializable]
    public class CardInstance
    {
        // 全局唯一递增ID，用于区分同类型的不同卡牌实例
        private static int _nextInstanceId = 1;

        public int instanceId;          // 本实例的唯一ID
        public PhysicsCardId cardId;    // 对应的物理量卡牌类型
        public bool isUsed;             // 主动效果是否已使用（主动牌使用后消耗）
        public bool isDisabled;         // 是否被压强/电磁屏蔽等暂时禁用
        public int disabledTurns;       // 禁用剩余回合数（0表示永久禁用直到手动解除）
        public bool forceUsedThisRound; // "力"本轮是否已使用（每轮限一次）

        // 容器相关状态（面积/体积/电容专用）
        public int containerMol;        // 容器中储存的mol数（面积/体积容器）
        public CardInstance containerCard; // 容器中储存的卡牌（电容容器）
        public bool pendingDeposit;     // 容器卡是否等待玩家存入mol（获取时标记）

        // 电荷激活状态
        public bool chargeActivated;    // 电荷是否已激活（激活后经过起点+2电流）

        // 磁通量计数
        public int fluxCount;           // 磁通量面的穿越计数
        public MoveDirection fluxDirection; // 磁通量面选定的方向

        // 磁感应强度/电场强度选定的方向
        public MoveDirection chosenDirection;

        // 电阻率选定的目标玩家索引
        public int resistivityTargetPlayer; // -1表示未选择

        /// <summary>创建一个新的卡牌实例</summary>
        public CardInstance(PhysicsCardId cardId)
        {
            this.instanceId = _nextInstanceId++; // 分配唯一ID并递增
            this.cardId = cardId;               // 设置卡牌类型
            this.isUsed = false;                // 初始未使用
            this.isDisabled = false;            // 初始未禁用
            this.disabledTurns = 0;             // 无禁用倒计时
            this.forceUsedThisRound = false;    // 力本轮未使用
            this.containerMol = 0;              // 容器为空
            this.containerCard = null;          // 电容容器为空
            // 面积/体积卡获取时标记待存入
            this.pendingDeposit = (cardId == PhysicsCardId.Area || cardId == PhysicsCardId.Volume);
            this.chargeActivated = false;       // 电荷未激活
            this.fluxCount = 0;                 // 磁通量计数为0
            this.resistivityTargetPlayer = -1;  // 未选择目标
        }

        /// <summary>获取此卡牌的静态定义数据</summary>
        public CardDefinition Definition => CardDatabase.Get(cardId);

        /// <summary>检查此卡牌是否可以使用（未消耗、未禁用）</summary>
        public bool CanUse()
        {
            if (isUsed) return false;       // 已经使用过了
            if (isDisabled) return false;   // 被禁用了
            return true;                    // 可以使用
        }

        /// <summary>私有构造函数：用于DeepCopy，不递增全局ID</summary>
        private CardInstance() { }

        /// <summary>深拷贝此卡牌实例（用于时间机器快照），不浪费全局ID</summary>
        public CardInstance DeepCopy()
        {
            var copy = new CardInstance();           // 使用私有构造，不递增ID
            copy.instanceId = this.instanceId;       // 保持原始ID
            copy.cardId = this.cardId;               // 复制卡牌类型
            copy.isUsed = this.isUsed;              // 复制使用状态
            copy.isDisabled = this.isDisabled;      // 复制禁用状态
            copy.disabledTurns = this.disabledTurns; // 复制禁用倒计时
            copy.forceUsedThisRound = this.forceUsedThisRound; // 复制力使用标记
            copy.containerMol = this.containerMol;   // 复制容器mol
            copy.pendingDeposit = this.pendingDeposit; // 复制待存入标记
            copy.chargeActivated = this.chargeActivated; // 复制电荷激活状态
            copy.fluxCount = this.fluxCount;         // 复制磁通量计数
            copy.fluxDirection = this.fluxDirection; // 复制磁通量方向
            copy.chosenDirection = this.chosenDirection; // 复制选定方向
            copy.resistivityTargetPlayer = this.resistivityTargetPlayer; // 复制目标

            // 深拷贝电容容器中的卡牌
            if (this.containerCard != null)
                copy.containerCard = this.containerCard.DeepCopy();

            return copy;
        }
    }
}
