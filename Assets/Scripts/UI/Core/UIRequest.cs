// ============================================================
// UIRequest.cs — UI交互请求/响应数据类型
// 所有UI交互通过这些数据类型传递信息
// ============================================================
using System;
using System.Collections.Generic;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.Systems;

namespace PhysicsFriends.UI
{
    // =============================================================
    // 通用异步回调容器：UI操作完成时写入结果
    // =============================================================

    /// <summary>
    /// 异步UI操作的结果容器。
    /// UI面板在用户操作完成后调用 Complete(value) 写入结果。
    /// TurnManager通过协程 yield return 等待 IsReady 变为 true。
    /// </summary>
    public class UICallback<T>
    {
        public T Result { get; private set; }
        public bool IsReady { get; private set; }

        public void Complete(T value)
        {
            Result = value;
            IsReady = true;
        }

        public void Reset()
        {
            Result = default;
            IsReady = false;
        }
    }

    // =============================================================
    // 骰子相关
    // =============================================================

    /// <summary>骰子投掷展示请求</summary>
    public class DiceRollRequest
    {
        public PlayerState player;
        public int result;                 // 骰子结果
        public string context;             // 上下文说明（"普通投骰"/"创举检查"等）
        public bool allowReroll;           // 是否允许重投（薛定谔技能）
    }

    /// <summary>骰子重投响应</summary>
    public class DiceRerollResponse
    {
        public bool wantsReroll;           // 是否选择重投
    }

    // =============================================================
    // 修正链相关
    // =============================================================

    /// <summary>力修正请求：询问某玩家是否使用力牌修正</summary>
    public class ForceModRequest
    {
        public PlayerState sourcePlayer;   // 使用力的玩家
        public PlayerState dicePlayer;     // 掷骰的玩家
        public int currentDiceValue;       // 当前骰子值
        public CardInstance forceCard;     // 力牌实例
        public bool hasPrincipia;          // 是否有原理（+2/-2）
    }

    /// <summary>力修正响应</summary>
    public class ForceModResponse
    {
        public bool useForce;              // 是否使用
        public int direction;              // +1 或 -1（原理版+2/-2）
    }

    /// <summary>加速度修正请求</summary>
    public class AccelModRequest
    {
        public PlayerState player;
        public int currentDiceValue;
        public CardInstance accelCard;
    }

    /// <summary>加速度修正响应</summary>
    public class AccelModResponse
    {
        public bool useAccel;
        public int direction;              // +1 或 -1
    }

    /// <summary>压强无效化请求</summary>
    public class PressureNullifyRequest
    {
        public PlayerState player;
        public int currentDiceValue;
        public string modDescription;      // 要无效化的修正描述
    }

    /// <summary>弹性系数钳制通知</summary>
    public class SpringClampNotice
    {
        public PlayerState player;
        public int beforeClamp;
        public int afterClamp;
    }

    // =============================================================
    // 选择相关（通用）
    // =============================================================

    /// <summary>通用选项</summary>
    public class ChoiceOption
    {
        public string id;                  // 选项标识
        public string label;               // 显示文本
        public string description;         // 描述
        public bool enabled;               // 是否可选
        public object data;                // 附加数据

        public ChoiceOption(string id, string label, string desc = "", bool enabled = true, object data = null)
        {
            this.id = id;
            this.label = label;
            this.description = desc;
            this.enabled = enabled;
            this.data = data;
        }
    }

    /// <summary>通用选择请求</summary>
    public class ChoiceRequest
    {
        public string title;               // 标题
        public string message;             // 说明文字
        public List<ChoiceOption> options;  // 可选项
        public bool allowCancel;           // 是否允许取消
        public PlayerState player;         // 做选择的玩家
    }

    // =============================================================
    // 卡牌选择
    // =============================================================

    /// <summary>手牌选择请求</summary>
    public class CardSelectRequest
    {
        public PlayerState player;
        public string title;
        public string message;
        public int minSelect;              // 最少选几张
        public int maxSelect;              // 最多选几张
        public Func<CardInstance, bool> filter; // 可选卡牌过滤器（null=全部可选）
    }

    /// <summary>手牌选择响应</summary>
    public class CardSelectResponse
    {
        public List<CardInstance> selectedCards;
        public bool cancelled;
    }

    /// <summary>基本物理量选择请求（从固定列表中选）</summary>
    public class BasicCardChoiceRequest
    {
        public PlayerState player;
        public string title;
        public int count;                  // 需要选几张
    }

    /// <summary>基本物理量选择响应</summary>
    public class BasicCardChoiceResponse
    {
        public List<PhysicsCardId> chosenCards;
    }

    // =============================================================
    // 棋盘格子选择
    // =============================================================

    /// <summary>格子选择请求（量子隧穿/建筑放置）</summary>
    public class TileSelectRequest
    {
        public PlayerState player;
        public string title;
        public Func<int, bool> filter;     // 可选格子过滤器
    }

    /// <summary>格子选择响应</summary>
    public class TileSelectResponse
    {
        public int selectedTileIndex = -1;
        public bool cancelled;
    }

    // =============================================================
    // 玩家目标选择
    // =============================================================

    /// <summary>目标玩家选择请求</summary>
    public class PlayerTargetRequest
    {
        public PlayerState sourcePlayer;
        public string title;
        public Func<PlayerState, bool> filter; // 可选目标过滤器

        /// <summary>便捷别名</summary>
        public PlayerState player { get => sourcePlayer; set => sourcePlayer = value; }

        /// <summary>便捷设置：传入候选列表自动生成filter</summary>
        public List<PlayerState> candidates
        {
            set => filter = value != null ? (p => value.Contains(p)) : null;
        }
    }

    /// <summary>目标玩家选择响应</summary>
    public class PlayerTargetResponse
    {
        public PlayerState targetPlayer;

        /// <summary>便捷别名</summary>
        public PlayerState selectedPlayer { get => targetPlayer; set => targetPlayer = value; }
    }

    // =============================================================
    // 事件/奖励展示
    // =============================================================

    /// <summary>事件牌展示请求</summary>
    public class EventCardShowRequest
    {
        public EventCardId eventId;
        public PlayerState player;
        public string effectDescription;
    }

    /// <summary>奖励牌展示请求</summary>
    public class RewardCardShowRequest
    {
        public RewardCardId rewardId;
        public PlayerState player;
        public string effectDescription;
    }

    // =============================================================
    // 相变选择
    // =============================================================

    /// <summary>相变选择请求</summary>
    public class PhaseChoiceRequest
    {
        public PlayerState player;
    }

    /// <summary>相变选择响应</summary>
    public class PhaseChoiceResponse
    {
        public PhaseState chosenPhase;
    }

    // =============================================================
    // 费曼赌注
    // =============================================================

    /// <summary>费曼赌注请求</summary>
    public class FeynmanBetRequest
    {
        public PlayerState player;
    }

    /// <summary>费曼赌注响应</summary>
    public class FeynmanBetResponse
    {
        public bool guessOdd;              // true=猜奇数, false=猜偶数
    }

    // =============================================================
    // 核反应堆
    // =============================================================

    /// <summary>核反应堆继续掷骰请求</summary>
    public class NuclearContinueRequest
    {
        public PlayerState player;
        public int currentReward;          // 当前累计奖励
        public int lastRoll;               // 上一次投掷结果
    }

    /// <summary>核反应堆继续响应</summary>
    public class NuclearContinueResponse
    {
        public bool continueRolling;
    }

    // =============================================================
    // 商店/交易
    // =============================================================

    /// <summary>商店购买请求</summary>
    public class ShopPurchaseRequest
    {
        public PlayerState player;
        public int randomPrice;            // 随机牌价格
        public int chosenPrice;            // 指定牌价格
        public Era currentEra;
    }

    /// <summary>商店购买响应</summary>
    public enum ShopPurchaseType { None, Random, Chosen }
    public class ShopPurchaseResponse
    {
        public ShopPurchaseType purchaseType;
        public PhysicsCardId chosenCardId; // 仅当purchaseType==Chosen时有效
    }

    /// <summary>交易请求</summary>
    public class TradeRequest
    {
        public PlayerState buyer;
        public PlayerState seller;         // 商店所属颜色的玩家
    }

    /// <summary>交易响应</summary>
    public class TradeResponse
    {
        public bool tradeOccurred;
        public bool cancelled;                  // 用户取消了交易
        public List<CardInstance> buyerGave;
        public List<CardInstance> sellerGave;
        public int molExchange;                 // 正=买家给卖家mol，负=卖家给买家

        // TradeManager 流程使用的字段
        public List<CardInstance> offeredCards = new();     // 发起方提供的卡牌
        public int offeredMol;                              // 发起方提供的mol
        public List<PhysicsCardId> requestedCardIds = new();// 发起方索要的卡牌ID
        public int requestedMol;                            // 发起方索要的mol
    }

    // =============================================================
    // 合成
    // =============================================================

    /// <summary>合成选择请求</summary>
    public class SynthesisRequest
    {
        public PlayerState player;
        public List<SynthesisResult> possibleSyntheses;
        public bool unlimitedAttempts;     // 领地格不限次数
    }

    /// <summary>合成选择响应</summary>
    public class SynthesisResponse
    {
        public bool doSynthesize;
        public PhysicsCardId targetId;
        public List<CardInstance> materialsUsed;
    }

    // =============================================================
    // 自由行动阶段
    // =============================================================

    /// <summary>自由行动菜单选项</summary>
    public enum FreeActionType
    {
        EndTurn,           // 结束回合
        UseActiveCard,     // 使用主动卡
        Synthesize,        // 合成
        InnovationProject, // 创新项目
        DiscardCard        // 弃牌（手牌超限时）
    }

    /// <summary>自由行动请求</summary>
    public class FreeActionRequest
    {
        public PlayerState player;
        public bool canSynthesize;
        public bool canUseActive;
        public bool mustDiscard;           // 手牌超限必须弃牌
        public List<FreeActionType> availableActions;
    }

    /// <summary>自由行动响应</summary>
    public class FreeActionResponse
    {
        public FreeActionType action;
        public int cardIndex;              // 选中的手牌索引（使用/弃牌）
        public int targetPlayerIndex;      // 目标玩家（动量等需要）
    }

    // =============================================================
    // 游戏设置
    // =============================================================

    /// <summary>游戏设置请求</summary>
    public class GameSetupRequest
    {
        public int maxPlayers;
    }

    /// <summary>游戏设置响应</summary>
    public class GameSetupResponse
    {
        public GameMode gameMode;
        public int playerCount;
        public Character[] characters;
        public PlayerColor[] colors;
        public string[] playerNames;
    }

    // =============================================================
    // 通知/日志（不需要响应）
    // =============================================================

    /// <summary>游戏通知类型</summary>
    public enum NotificationType
    {
        Info,           // 一般信息
        Warning,        // 警告
        Achievement,    // 创举达成
        EraChange,      // 纪元变化
        Victory,        // 胜利
        TurnStart,      // 回合开始
        TurnEnd,        // 回合结束
        Movement,       // 移动
        SkipTurn,       // 跳过回合
        BuffApplied,    // Buff应用
        BuffExpired,    // Buff过期
        MolChange,      // mol变化
        CardGained,     // 获得卡牌
        CardLost        // 失去卡牌
    }

    /// <summary>游戏通知</summary>
    public class GameNotification
    {
        public NotificationType type;
        public string message;
        public PlayerState player;         // 相关玩家（可选）
        public float duration;             // 显示时长（秒），0=使用默认值

        public GameNotification(NotificationType type, string message,
            PlayerState player = null, float duration = 0f)
        {
            this.type = type;
            this.message = message;
            this.player = player;
            this.duration = duration;
        }
    }
}
