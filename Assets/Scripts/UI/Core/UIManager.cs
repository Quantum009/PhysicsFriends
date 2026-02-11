// ============================================================
// UIManager.cs — UI管理器：实现IUIProvider接口
// 管理所有UI面板的显示/隐藏，处理用户输入并回调游戏逻辑
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.Systems;
using PhysicsFriends.UI.Panels;

namespace PhysicsFriends.UI
{
    /// <summary>
    /// UI管理器 MonoBehaviour。
    /// 在场景中作为顶级UI控制器，持有所有面板引用。
    /// 实现 IUIProvider 接口供 TurnManager 调用。
    /// </summary>
    public class UIManager : MonoBehaviour, IUIProvider
    {
        // === 单例 ===
        public static UIManager Instance { get; private set; }

        // === 面板引用（在Inspector中绑定）===
        [Header("面板引用")]
        [SerializeField] private HUDPanel hudPanel;
        [SerializeField] private HandPanel handPanel;
        [SerializeField] private DicePanel dicePanel;
        [SerializeField] private ChoiceDialog choiceDialog;
        [SerializeField] private CardSelectDialog cardSelectDialog;
        [SerializeField] private TileSelectPanel tileSelectPanel;
        [SerializeField] private EventRewardPanel eventRewardPanel;
        [SerializeField] private ShopPanel shopPanel;
        [SerializeField] private TradePanel tradePanel;
        [SerializeField] private SynthesisPanel synthesisPanel;
        [SerializeField] private ModificationPanel modificationPanel;
        [SerializeField] private FreeActionPanel freeActionPanel;
        [SerializeField] private GameOverPanel gameOverPanel;
        [SerializeField] private SetupPanel setupPanel;
        [SerializeField] private NotificationPanel notificationPanel;

        [Header("Canvas引用")]
        [SerializeField] private Canvas mainCanvas;

        // === 内部状态 ===
        private List<PlayerState> _cachedPlayers;

        // =============================================================
        // Unity生命周期
        // =============================================================

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // 启动时隐藏所有弹窗面板
            HideAllDialogs();
        }

        /// <summary>隐藏所有弹窗面板（保留HUD）</summary>
        public void HideAllDialogs()
        {
            SetPanelActive(dicePanel, false);
            SetPanelActive(choiceDialog, false);
            SetPanelActive(cardSelectDialog, false);
            SetPanelActive(tileSelectPanel, false);
            SetPanelActive(eventRewardPanel, false);
            SetPanelActive(shopPanel, false);
            SetPanelActive(tradePanel, false);
            SetPanelActive(synthesisPanel, false);
            SetPanelActive(modificationPanel, false);
            SetPanelActive(freeActionPanel, false);
            SetPanelActive(gameOverPanel, false);
            SetPanelActive(setupPanel, false);
        }

        private void SetPanelActive(MonoBehaviour panel, bool active)
        {
            if (panel != null)
                panel.gameObject.SetActive(active);
        }

        // =============================================================
        // IUIProvider 实现 — 骰子
        // =============================================================

        public UICallback<bool> ShowDiceRoll(DiceRollRequest request)
        {
            var cb = new UICallback<bool>();
            if (dicePanel != null)
            {
                dicePanel.gameObject.SetActive(true);
                dicePanel.ShowRoll(request, () =>
                {
                    dicePanel.gameObject.SetActive(false);
                    cb.Complete(true);
                });
            }
            else
            {
                Debug.Log($"[UI] 骰子:{request.result} ({request.context})");
                cb.Complete(true);
            }
            return cb;
        }

        public UICallback<DiceRerollResponse> AskReroll(DiceRollRequest request)
        {
            var cb = new UICallback<DiceRerollResponse>();
            if (dicePanel != null)
            {
                dicePanel.gameObject.SetActive(true);
                dicePanel.AskReroll(request, (wantsReroll) =>
                {
                    dicePanel.gameObject.SetActive(false);
                    cb.Complete(new DiceRerollResponse { wantsReroll = wantsReroll });
                });
            }
            else
            {
                // 无UI时默认：点数<3重投
                cb.Complete(new DiceRerollResponse { wantsReroll = request.result < 3 });
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 修正链
        // =============================================================

        public UICallback<ForceModResponse> AskForceModification(ForceModRequest request)
        {
            var cb = new UICallback<ForceModResponse>();
            if (modificationPanel != null)
            {
                modificationPanel.gameObject.SetActive(true);
                modificationPanel.AskForce(request, (response) =>
                {
                    modificationPanel.gameObject.SetActive(false);
                    cb.Complete(response);
                });
            }
            else
            {
                // 默认：非掷骰者-1，掷骰者+1
                bool isSelf = request.sourcePlayer == request.dicePlayer;
                int mod = isSelf ? 1 : -1;
                if (request.hasPrincipia) mod *= 2;
                cb.Complete(new ForceModResponse { useForce = true, direction = mod });
            }
            return cb;
        }

        public UICallback<AccelModResponse> AskAccelModification(AccelModRequest request)
        {
            var cb = new UICallback<AccelModResponse>();
            if (modificationPanel != null)
            {
                modificationPanel.gameObject.SetActive(true);
                modificationPanel.AskAccel(request, (response) =>
                {
                    modificationPanel.gameObject.SetActive(false);
                    cb.Complete(response);
                });
            }
            else
            {
                cb.Complete(new AccelModResponse { useAccel = true, direction = 1 });
            }
            return cb;
        }

        public UICallback<bool> AskPressureNullify(PressureNullifyRequest request)
        {
            var cb = new UICallback<bool>();
            if (modificationPanel != null)
            {
                modificationPanel.gameObject.SetActive(true);
                modificationPanel.AskPressure(request, (use) =>
                {
                    modificationPanel.gameObject.SetActive(false);
                    cb.Complete(use);
                });
            }
            else
            {
                cb.Complete(false); // 默认不使用
            }
            return cb;
        }

        public void NotifySpringClamp(SpringClampNotice notice)
        {
            SendNotification(new GameNotification(
                NotificationType.Info,
                $"弹性系数钳制：{notice.beforeClamp}→{notice.afterClamp}",
                notice.player));
        }

        // =============================================================
        // IUIProvider 实现 — 通用选择
        // =============================================================

        public UICallback<string> ShowChoice(ChoiceRequest request)
        {
            var cb = new UICallback<string>();
            if (choiceDialog != null)
            {
                choiceDialog.gameObject.SetActive(true);
                choiceDialog.Show(request, (chosenId) =>
                {
                    choiceDialog.gameObject.SetActive(false);
                    cb.Complete(chosenId);
                });
            }
            else
            {
                // 默认选第一个
                cb.Complete(request.options.Count > 0 ? request.options[0].id : null);
            }
            return cb;
        }

        public UICallback<bool> ShowConfirm(string title, string message, PlayerState player = null)
        {
            var cb = new UICallback<bool>();
            if (choiceDialog != null)
            {
                var request = new ChoiceRequest
                {
                    title = title,
                    message = message,
                    player = player,
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption("yes", "确定"),
                        new ChoiceOption("no", "取消")
                    },
                    allowCancel = false
                };
                choiceDialog.gameObject.SetActive(true);
                choiceDialog.Show(request, (id) =>
                {
                    choiceDialog.gameObject.SetActive(false);
                    cb.Complete(id == "yes");
                });
            }
            else
            {
                cb.Complete(true);
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 卡牌选择
        // =============================================================

        public UICallback<CardSelectResponse> SelectCards(CardSelectRequest request)
        {
            var cb = new UICallback<CardSelectResponse>();
            if (cardSelectDialog != null)
            {
                cardSelectDialog.gameObject.SetActive(true);
                cardSelectDialog.Show(request, (response) =>
                {
                    cardSelectDialog.gameObject.SetActive(false);
                    cb.Complete(response);
                });
            }
            else
            {
                // 默认选第一张可用的
                var available = request.player.handCards
                    .Where(c => request.filter == null || request.filter(c))
                    .Take(request.minSelect)
                    .ToList();
                cb.Complete(new CardSelectResponse { selectedCards = available, cancelled = false });
            }
            return cb;
        }

        public UICallback<BasicCardChoiceResponse> SelectBasicCards(BasicCardChoiceRequest request)
        {
            var cb = new UICallback<BasicCardChoiceResponse>();
            // 用通用选择面板来选基本物理量类型
            var options = new List<ChoiceOption>
            {
                new ChoiceOption("Time", "时间 [s]"),
                new ChoiceOption("Length", "长度 [m]"),
                new ChoiceOption("Mass", "质量 [kg]"),
                new ChoiceOption("Current", "电流 [A]"),
                new ChoiceOption("Temperature", "温度 [K]"),
                new ChoiceOption("LuminousIntensity", "光照强度 [cd]")
            };

            var choiceReq = new ChoiceRequest
            {
                title = request.title,
                message = $"选择{request.count}种基本物理量",
                options = options,
                player = request.player,
                allowCancel = false
            };

            if (choiceDialog != null)
            {
                choiceDialog.gameObject.SetActive(true);
                // 简化：一次选一种（多次调用处理多选）
                choiceDialog.Show(choiceReq, (id) =>
                {
                    choiceDialog.gameObject.SetActive(false);
                    var chosen = ParseBasicCardId(id);
                    cb.Complete(new BasicCardChoiceResponse
                    {
                        chosenCards = new List<PhysicsCardId> { chosen }
                    });
                });
            }
            else
            {
                cb.Complete(new BasicCardChoiceResponse
                {
                    chosenCards = new List<PhysicsCardId> { PhysicsCardId.Time }
                });
            }
            return cb;
        }

        private PhysicsCardId ParseBasicCardId(string id)
        {
            switch (id)
            {
                case "Time": return PhysicsCardId.Time;
                case "Length": return PhysicsCardId.Length;
                case "Mass": return PhysicsCardId.Mass;
                case "Current": return PhysicsCardId.Current;
                case "Temperature": return PhysicsCardId.Temperature;
                case "LuminousIntensity": return PhysicsCardId.LuminousIntensity;
                default: return PhysicsCardId.Time;
            }
        }

        // =============================================================
        // IUIProvider 实现 — 格子选择
        // =============================================================

        public UICallback<TileSelectResponse> SelectTile(TileSelectRequest request)
        {
            var cb = new UICallback<TileSelectResponse>();
            if (tileSelectPanel != null)
            {
                tileSelectPanel.gameObject.SetActive(true);
                tileSelectPanel.Show(request, (tileIdx) =>
                {
                    tileSelectPanel.gameObject.SetActive(false);
                    cb.Complete(new TileSelectResponse { selectedTileIndex = tileIdx });
                });
            }
            else
            {
                cb.Complete(new TileSelectResponse { selectedTileIndex = 0 });
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 目标玩家选择
        // =============================================================

        public UICallback<PlayerTargetResponse> SelectTargetPlayer(PlayerTargetRequest request)
        {
            var cb = new UICallback<PlayerTargetResponse>();
            if (_cachedPlayers == null)
            {
                cb.Complete(new PlayerTargetResponse { targetPlayer = null });
                return cb;
            }

            var validTargets = _cachedPlayers
                .Where(p => p != request.sourcePlayer && (request.filter == null || request.filter(p)))
                .ToList();

            if (validTargets.Count == 0)
            {
                cb.Complete(new PlayerTargetResponse { targetPlayer = null });
                return cb;
            }

            var options = validTargets.Select(p =>
                new ChoiceOption(p.playerIndex.ToString(), p.playerName)).ToList();

            var req = new ChoiceRequest
            {
                title = request.title,
                message = "选择一名玩家",
                options = options,
                player = request.sourcePlayer,
                allowCancel = true
            };

            var choiceCb = ShowChoice(req);
            // 在协程框架中，这里需要通过中间回调桥接
            // 实际使用时由协程等待 choiceCb
            StartCoroutine(WaitAndBridge(choiceCb, (id) =>
            {
                if (string.IsNullOrEmpty(id))
                {
                    cb.Complete(new PlayerTargetResponse { targetPlayer = null });
                    return;
                }
                int idx = int.Parse(id);
                var target = _cachedPlayers.FirstOrDefault(p => p.playerIndex == idx);
                cb.Complete(new PlayerTargetResponse { targetPlayer = target });
            }));

            return cb;
        }

        private System.Collections.IEnumerator WaitAndBridge<T>(UICallback<T> source, Action<T> onComplete)
        {
            while (!source.IsReady)
                yield return null;
            onComplete?.Invoke(source.Result);
        }

        // =============================================================
        // IUIProvider 实现 — 事件/奖励展示
        // =============================================================

        public UICallback<bool> ShowEventCard(EventCardShowRequest request)
        {
            var cb = new UICallback<bool>();
            if (eventRewardPanel != null)
            {
                eventRewardPanel.gameObject.SetActive(true);
                eventRewardPanel.ShowEvent(request, () =>
                {
                    eventRewardPanel.gameObject.SetActive(false);
                    cb.Complete(true);
                });
            }
            else
            {
                Debug.Log($"[UI] 事件牌：{EventCardDatabase.Get(request.eventId)?.nameZH}");
                cb.Complete(true);
            }
            return cb;
        }

        public UICallback<bool> ShowRewardCard(RewardCardShowRequest request)
        {
            var cb = new UICallback<bool>();
            if (eventRewardPanel != null)
            {
                eventRewardPanel.gameObject.SetActive(true);
                eventRewardPanel.ShowReward(request, () =>
                {
                    eventRewardPanel.gameObject.SetActive(false);
                    cb.Complete(true);
                });
            }
            else
            {
                Debug.Log($"[UI] 奖励牌：{RewardCardDatabase.Get(request.rewardId)?.nameZH}");
                cb.Complete(true);
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 特殊事件交互
        // =============================================================

        public UICallback<PhaseChoiceResponse> AskPhaseChoice(PhaseChoiceRequest request)
        {
            var cb = new UICallback<PhaseChoiceResponse>();
            var options = new List<ChoiceOption>
            {
                new ChoiceOption("Solid", "固态", "跳过下一回合，获得8mol"),
                new ChoiceOption("Liquid", "液态", "调转前进方向"),
                new ChoiceOption("Gas", "气态", "始终轻盈、不触发地块效果，直到经过起点")
            };
            var req = new ChoiceRequest
            {
                title = "相变",
                message = "选择一种物态",
                options = options,
                player = request.player,
                allowCancel = false
            };

            var choiceCb = ShowChoice(req);
            StartCoroutine(WaitAndBridge(choiceCb, (id) =>
            {
                PhaseState phase = PhaseState.Solid;
                switch (id)
                {
                    case "Solid": phase = PhaseState.Solid; break;
                    case "Liquid": phase = PhaseState.Liquid; break;
                    case "Gas": phase = PhaseState.Gas; break;
                }
                cb.Complete(new PhaseChoiceResponse { chosenPhase = phase });
            }));
            return cb;
        }

        public UICallback<FeynmanBetResponse> AskFeynmanBet(FeynmanBetRequest request)
        {
            var cb = new UICallback<FeynmanBetResponse>();
            var options = new List<ChoiceOption>
            {
                new ChoiceOption("odd", "奇数", "猜骰子点数为奇数"),
                new ChoiceOption("even", "偶数", "猜骰子点数为偶数")
            };
            var req = new ChoiceRequest
            {
                title = "费曼的赌注",
                message = "猜一下骰子的奇偶！",
                options = options,
                player = request.player,
                allowCancel = false
            };

            var choiceCb = ShowChoice(req);
            StartCoroutine(WaitAndBridge(choiceCb, (id) =>
            {
                cb.Complete(new FeynmanBetResponse { guessOdd = id == "odd" });
            }));
            return cb;
        }

        public UICallback<NuclearContinueResponse> AskNuclearContinue(NuclearContinueRequest request)
        {
            var cb = new UICallback<NuclearContinueResponse>();
            var confirmCb = ShowConfirm(
                "核反应堆",
                $"当前累计奖励：{request.currentReward}mol\n上次投掷：{request.lastRoll}\n继续掷骰？（5-6将失去所有mol！）",
                request.player);

            StartCoroutine(WaitAndBridge(confirmCb, (yes) =>
            {
                cb.Complete(new NuclearContinueResponse { continueRolling = yes });
            }));
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 商店/交易
        // =============================================================

        public UICallback<ShopPurchaseResponse> ShowShop(ShopPurchaseRequest request)
        {
            var cb = new UICallback<ShopPurchaseResponse>();
            if (shopPanel != null)
            {
                shopPanel.gameObject.SetActive(true);
                shopPanel.Show(request, (response) =>
                {
                    shopPanel.gameObject.SetActive(false);
                    cb.Complete(response);
                });
            }
            else
            {
                // 默认不买
                cb.Complete(new ShopPurchaseResponse { purchaseType = ShopPurchaseType.None });
            }
            return cb;
        }

        public UICallback<TradeResponse> ShowTrade(TradeRequest request)
        {
            var cb = new UICallback<TradeResponse>();
            if (tradePanel != null)
            {
                tradePanel.gameObject.SetActive(true);
                tradePanel.Show(request, (response) =>
                {
                    tradePanel.gameObject.SetActive(false);
                    cb.Complete(response);
                });
            }
            else
            {
                cb.Complete(new TradeResponse { tradeOccurred = false });
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 合成
        // =============================================================

        public UICallback<SynthesisResponse> ShowSynthesis(SynthesisRequest request)
        {
            var cb = new UICallback<SynthesisResponse>();
            if (synthesisPanel != null)
            {
                synthesisPanel.gameObject.SetActive(true);
                synthesisPanel.Show(request, (response) =>
                {
                    synthesisPanel.gameObject.SetActive(false);
                    cb.Complete(response);
                });
            }
            else
            {
                cb.Complete(new SynthesisResponse { doSynthesize = false });
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 自由行动
        // =============================================================

        public UICallback<FreeActionResponse> ShowFreeActionMenu(FreeActionRequest request)
        {
            var cb = new UICallback<FreeActionResponse>();
            if (freeActionPanel != null)
            {
                freeActionPanel.gameObject.SetActive(true);
                freeActionPanel.Show(request, (response) =>
                {
                    freeActionPanel.gameObject.SetActive(false);
                    cb.Complete(response);
                });
            }
            else
            {
                cb.Complete(new FreeActionResponse { action = FreeActionType.EndTurn });
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 游戏设置
        // =============================================================

        public UICallback<GameSetupResponse> ShowGameSetup(GameSetupRequest request)
        {
            var cb = new UICallback<GameSetupResponse>();
            if (setupPanel != null)
            {
                setupPanel.gameObject.SetActive(true);
                setupPanel.Show(request, (response) =>
                {
                    setupPanel.gameObject.SetActive(false);
                    cb.Complete(response);
                });
            }
            else
            {
                // 默认2人标准模式
                cb.Complete(new GameSetupResponse
                {
                    gameMode = GameMode.Standard,
                    playerCount = 2,
                    characters = new[] { Character.Newton, Character.Maxwell },
                    colors = new[] { PlayerColor.Red, PlayerColor.Blue },
                    playerNames = new[] { "玩家1", "玩家2" }
                });
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — HUD更新
        // =============================================================

        public void UpdateHUD(List<PlayerState> players, int currentPlayerIndex,
            int roundNumber, Era era)
        {
            _cachedPlayers = players;
            if (hudPanel != null)
                hudPanel.Refresh(players, currentPlayerIndex, roundNumber, era);
        }

        public void UpdateHandDisplay(PlayerState player)
        {
            if (handPanel != null)
                handPanel.Refresh(player);
        }

        public void HighlightTile(int tileIndex, bool highlight)
        {
            // 通知棋盘视觉层高亮格子
            var board = FindObjectOfType<BoardVisual>();
            if (board != null) board.HighlightTile(tileIndex, highlight);
        }

        public void ClearAllHighlights()
        {
            var board = FindObjectOfType<BoardVisual>();
            if (board != null) board.ClearAllHighlights();
        }

        // =============================================================
        // IUIProvider 实现 — 通知
        // =============================================================

        public void SendNotification(GameNotification notification)
        {
            if (notificationPanel != null)
                notificationPanel.ShowNotification(notification);
            else
                Debug.Log($"[通知][{notification.type}] {notification.message}");
        }

        public UICallback<bool> SendNotificationAndWait(GameNotification notification)
        {
            var cb = new UICallback<bool>();
            if (notificationPanel != null)
            {
                notificationPanel.ShowNotificationWithCallback(notification, () =>
                {
                    cb.Complete(true);
                });
            }
            else
            {
                Debug.Log($"[通知][{notification.type}] {notification.message}");
                cb.Complete(true);
            }
            return cb;
        }

        // =============================================================
        // IUIProvider 实现 — 动画
        // =============================================================

        public UICallback<bool> AnimateMovement(PlayerState player, int fromTile,
            int toTile, bool passedStart)
        {
            var cb = new UICallback<bool>();
            // 基础实现：直接完成（后续可接入动画系统）
            cb.Complete(true);
            return cb;
        }

        public UICallback<bool> AnimateTeleport(PlayerState player, int fromTile, int toTile)
        {
            var cb = new UICallback<bool>();
            cb.Complete(true);
            return cb;
        }
    }

    /// <summary>
    /// 棋盘视觉层（占位，后续与棋盘Prefab绑定）
    /// </summary>
    public class BoardVisual : MonoBehaviour
    {
        public void HighlightTile(int index, bool highlight) { }
        public void ClearAllHighlights() { }
    }
}
