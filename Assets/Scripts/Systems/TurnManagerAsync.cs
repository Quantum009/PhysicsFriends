// ============================================================
// TurnManagerAsync.cs — 异步回合管理器
// 协程版本的回合执行流程，在每个需要UI交互的点 yield 等待。
// 替代原同步 TurnManager.ExecuteTurn()，由 GameManager 的协程调用。
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.Board;
using PhysicsFriends.UI;

namespace PhysicsFriends.Systems
{
    /// <summary>
    /// 异步回合管理器：所有需要玩家交互的操作通过 IUIProvider 请求，
    /// 在协程中 yield return 等待 UICallback.IsReady。
    /// </summary>
    public class TurnManagerAsync
    {
        // 系统引用（与原TurnManager相同）
        private DiceSystem _dice;
        private DeckManager _deck;
        private BoardManager _board;
        private PassiveEffectManager _passive;
        private EventEffectProcessor _eventProcessor;
        private RewardEffectProcessor _rewardProcessor;
        private CardEffectProcessor _cardProcessor;
        private AchievementChecker _achievementChecker;
        private SynthesisSystem _synthesis;

        private List<PlayerState> _allPlayers;
        private GameConfig _config;
        private Era _currentEra;
        private GameRecorder _recorder;
        private IUIProvider _ui;
        private GameSaveManager _saveManager; // 每回合存档管理器

        // ---- 新增系统引用 ----
        private BuildingManager _buildingManager;
        private CharacterAbilitySystem _characterSystem;
        private MagneticFluxManager _magneticFlux;
        private VictoryChecker _victoryChecker;

        // 回合状态
        private GameSnapshot _currentSnapshot;
        private int _turnCounter;
        private int _eraRoundCount;
        private TurnResult _currentResult;

        public TurnManagerAsync(
            DiceSystem dice, DeckManager deck, BoardManager board,
            PassiveEffectManager passive, EventEffectProcessor eventProc,
            RewardEffectProcessor rewardProc, CardEffectProcessor cardProc,
            AchievementChecker achChecker, SynthesisSystem synthesis,
            List<PlayerState> players, GameConfig config, GameRecorder recorder,
            IUIProvider uiProvider)
        {
            _dice = dice;
            _deck = deck;
            _board = board;
            _passive = passive;
            _eventProcessor = eventProc;
            _rewardProcessor = rewardProc;
            _cardProcessor = cardProc;
            _achievementChecker = achChecker;
            _synthesis = synthesis;
            _allPlayers = players;
            _config = config;
            _recorder = recorder;
            _ui = uiProvider;
            _saveManager = new GameSaveManager(); // 每回合存档，无上限
            _currentEra = Era.NaturalPhilosophy;

            foreach (var p in _allPlayers)
                p.OnVictoryCheck = OnPlayerValueChanged;
        }

        /// <summary>注入新增的子系统（在 GameManager 初始化后调用）</summary>
        public void InjectNewSystems(
            BuildingManager buildingMgr,
            CharacterAbilitySystem charSystem,
            MagneticFluxManager magneticFlux,
            VictoryChecker victoryChecker)
        {
            _buildingManager = buildingMgr;
            _characterSystem = charSystem;
            _magneticFlux = magneticFlux;
            _victoryChecker = victoryChecker;
        }

        public void SetEra(Era era)
        {
            _currentEra = era;
            _achievementChecker.SetEra(era);
        }

        public int EraRoundCount => _eraRoundCount;
        public GameSaveManager SaveManager => _saveManager;

        // =============================================================
        // 辅助：等待UICallback
        // =============================================================

        /// <summary>等待UICallback完成的协程辅助器</summary>
        private static IEnumerator WaitFor<T>(UICallback<T> cb)
        {
            while (!cb.IsReady)
                yield return null;
        }

        // =============================================================
        // 主回合协程
        // =============================================================

        /// <summary>
        /// 异步执行一个完整回合。完成后通过 onComplete 回调返回结果。
        /// 由 GameManager 的协程调用：yield return StartCoroutine(ExecuteTurnAsync(...))
        /// </summary>
        public IEnumerator ExecuteTurnAsync(PlayerState player, Action<TurnResult> onComplete)
        {
            var result = new TurnResult();
            _currentResult = result;
            _turnCounter++;

            Debug.Log($"\n{'='}{new string('=', 50)}");
            Debug.Log($"[回合{_turnCounter}] {player.playerName} 的回合开始");

            // 通知UI：回合开始
            _ui.SendNotification(new GameNotification(NotificationType.TurnStart,
                $"{player.playerName} 的回合", player));
            _ui.UpdateHUD(_allPlayers, _allPlayers.IndexOf(player), _eraRoundCount, _currentEra);
            _ui.UpdateHandDisplay(player);

            // 记录
            var rec = _recorder.BeginTurn(_turnCounter, _eraRoundCount, player.playerIndex,
                player.playerName, _currentEra,
                player.position, player.mol, player.handCards.Count, player.achievementPoints);

            // ---- 阶段0: 保存快照（用于时间机器回滚 + 每回合存档）----
            _currentSnapshot = GameSnapshot.Create(_allPlayers, _board, _currentEra);
            _saveManager.SaveTurn(_turnCounter, _allPlayers, _board, _currentEra,
                _allPlayers.IndexOf(player));

            // ---- 阶段1: 跳过检查 ----
            if (_passive.ShouldSkipTurn(player))
            {
                result.turnSkipped = true;
                _ui.SendNotification(new GameNotification(NotificationType.SkipTurn,
                    $"{player.playerName} 跳过本回合", player));
                player.recentMoveSteps.Add(0);
                if (player.recentMoveSteps.Count > 10) player.recentMoveSteps.RemoveAt(0);
                _passive.TickBuffs(player);
                _recorder.EndTurn(player.position, player.mol, player.handCards.Count, player.achievementPoints);
                _currentResult = null;
                onComplete?.Invoke(result);
                yield break;
            }

            // ---- 阶段1.5: 回合开始被动效果 ----
            int molBefore = player.mol;
            _passive.OnTurnStart(player, _allPlayers);
            if (player.mol != molBefore)
                _ui.SendNotification(new GameNotification(NotificationType.MolChange,
                    $"被动效果：mol {molBefore}→{player.mol}", player));
            if (result.gameOver) { Finish(player, result, rec); onComplete?.Invoke(result); yield break; }

            // ---- 阶段2: 创举掷骰检查 ----
            yield return Phase2_AchievementRollCheckAsync(player, result);
            if (result.gameOver) { Finish(player, result, rec); onComplete?.Invoke(result); yield break; }

            // ---- 阶段3: 掷骰 ----
            int rawRoll = 0;
            yield return Phase3_RollDiceAsync(player, (r) => rawRoll = r);
            result.diceRoll = rawRoll;
            rec.rawDiceRoll = rawRoll;

            int actualSteps;

            if (player.michelsonMorleyTurns > 0)
            {
                actualSteps = 6;
                result.actualSteps = 6;
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "迈克尔逊-莫雷：步数固定为6", player));
            }
            else
            {
                // ---- 阶段4: 修正链 ----
                int modifiedRoll = 0;
                yield return Phase4_ModificationChainAsync(player, rawRoll, (r) => modifiedRoll = r);
                rec.modifiedDiceRoll = modifiedRoll;

                // ---- 阶段5: 沉重/轻盈 ----
                actualSteps = _passive.CalculateActualSteps(player, modifiedRoll);
                result.actualSteps = actualSteps;
                rec.actualSteps = actualSteps;

                if (actualSteps != modifiedRoll)
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        $"沉重/轻盈：{modifiedRoll}→{actualSteps}步", player));
            }

            // 记录步数
            player.recentMoveSteps.Add(actualSteps);
            if (player.recentMoveSteps.Count > 10) player.recentMoveSteps.RemoveAt(0);

            // ---- 阶段6: 移动 ----
            int startPos = player.position;
            bool passedStart = MovePlayer(player, actualSteps);

            // 移动动画
            var moveCb = _ui.AnimateMovement(player, startPos, player.position, passedStart);
            yield return WaitFor(moveCb);

            _ui.SendNotification(new GameNotification(NotificationType.Movement,
                $"移动：格{startPos}→格{player.position}（{actualSteps}步）", player));

            if (passedStart)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "经过起点！", player));
                molBefore = player.mol;
                _passive.OnPassStart(player);
                if (player.mol != molBefore)
                    _ui.SendNotification(new GameNotification(NotificationType.MolChange,
                        $"经过起点：mol {molBefore}→{player.mol}", player));

                // ▸ 新增：麦克斯韦角色能力 — 经过起点额外获得电流
                if (_characterSystem != null)
                    _characterSystem.OnPassStartBonus(player);

                if (player.phaseState == PhaseState.Gas)
                {
                    player.phaseState = PhaseState.None;
                    _ui.SendNotification(new GameNotification(NotificationType.BuffExpired,
                        "气态在经过起点后解除", player));
                }
            }

            _ui.UpdateHUD(_allPlayers, _allPlayers.IndexOf(player), _eraRoundCount, _currentEra);
            if (result.gameOver) { Finish(player, result, rec); onComplete?.Invoke(result); yield break; }

            // ▸ 新增：处理经过建筑格的收费
            yield return ProcessPassingTollsAsync(player);

            if (player.stoppedByRoadblock)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "被路障/电磁铁阻挡！", player));
                _passive.TickBuffs(player);
                _recorder.EndTurn(player.position, player.mol, player.handCards.Count, player.achievementPoints);
                _currentResult = null;
                onComplete?.Invoke(result);
                yield break;
            }

            // ---- 阶段7: 落点效果 ----
            bool rollback = false;
            yield return Phase7_LandingEffectAsync(player, result, (rb) => rollback = rb);

            if (rollback)
            {
                result.timeRollback = true;
                _ui.SendNotification(new GameNotification(NotificationType.Warning,
                    "时间机器回滚！本回合无效", player));
                _recorder.EndTurn(player.position, player.mol, player.handCards.Count, player.achievementPoints);
                _currentResult = null;
                onComplete?.Invoke(result);
                yield break;
            }
            if (result.gameOver) { Finish(player, result, rec); onComplete?.Invoke(result); yield break; }

            // ---- 阶段7.5: 处理容器卡存入（面积/体积获取后询问存入mol）----
            yield return ProcessPendingContainerDepositsAsync(player);
            _ui.UpdateHUD(_allPlayers, _allPlayers.IndexOf(player), _eraRoundCount, _currentEra);

            // ---- 阶段8: 自由行动 ----
            yield return Phase8_FreeActionAsync(player);
            if (result.gameOver) { Finish(player, result, rec); onComplete?.Invoke(result); yield break; }

            // ---- 阶段9: 回合结束 ----
            Phase9_TurnEnd(player, result);

            foreach (var achId in result.newAchievements)
            {
                var achDef = AchievementDatabase.Get(achId);
                _ui.SendNotification(new GameNotification(NotificationType.Achievement,
                    $"达成创举：{achDef?.nameZH}（+{achDef?.points}分）", player, 2f));
                yield return new WaitForSeconds(0.5f);
            }

            _passive.TickBuffs(player);
            _ui.UpdateHUD(_allPlayers, _allPlayers.IndexOf(player), _eraRoundCount, _currentEra);
            _ui.UpdateHandDisplay(player);

            Finish(player, result, rec);
            _currentResult = null;
            onComplete?.Invoke(result);
        }

        // =============================================================
        // 阶段2: 创举掷骰（异步）
        // =============================================================

        private IEnumerator Phase2_AchievementRollCheckAsync(PlayerState player, TurnResult result)
        {
            foreach (var achId in _achievementChecker.ActiveAchievements)
            {
                if (_achievementChecker.GlobalCompleted.Contains(achId)) continue;
                if (player.completedAchievements.Contains(achId)) continue;

                bool conditionMet = false;
                int targetRoll = -1;
                string achName = AchievementDatabase.Get(achId)?.nameZH ?? achId.ToString();

                switch (achId)
                {
                    case AchievementId.Atomism:
                        conditionMet = player.mol >= _config.AdjustValue(15);
                        targetRoll = 6;
                        break;
                    case AchievementId.PinholeImaging:
                        conditionMet = player.CountCards(PhysicsCardId.LuminousIntensity) >= 2;
                        targetRoll = 1;
                        break;
                    case AchievementId.StandardModel:
                        conditionMet = player.mol >= _config.AdjustValue(40);
                        targetRoll = 6;
                        break;
                }

                if (conditionMet && targetRoll > 0)
                {
                    int roll = _dice.RollUnmodified();

                    // 展示骰子结果
                    var diceCb = _ui.ShowDiceRoll(new DiceRollRequest
                    {
                        player = player,
                        result = roll,
                        context = $"创举检查：{achName}（需要{targetRoll}）",
                        allowReroll = false
                    });
                    yield return WaitFor(diceCb);

                    if (roll == targetRoll)
                    {
                        player.achievementDiceCheckPassed.Add(achId);
                        _ui.SendNotification(new GameNotification(NotificationType.Achievement,
                            $"创举{achName}掷骰通过！", player));
                    }
                }
            }
        }

        // =============================================================
        // 阶段3: 掷骰（异步，含薛定谔重投）
        // =============================================================

        private IEnumerator Phase3_RollDiceAsync(PlayerState player, Action<int> onResult)
        {
            int roll = _dice.RollUnmodified();

            // 展示投骰结果
            var showCb = _ui.ShowDiceRoll(new DiceRollRequest
            {
                player = player,
                result = roll,
                context = "投骰",
                allowReroll = player.character == Character.Schrodinger
            });
            yield return WaitFor(showCb);

            // 薛定谔重投
            if (player.character == Character.Schrodinger)
            {
                var rerollCb = _ui.AskReroll(new DiceRollRequest
                {
                    player = player,
                    result = roll,
                    context = "薛定谔技能：是否重投？",
                    allowReroll = true
                });
                yield return WaitFor(rerollCb);

                if (rerollCb.Result.wantsReroll)
                {
                    int newRoll = _dice.RollUnmodified();
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        $"薛定谔重投：{roll}→{newRoll}", player));

                    var showCb2 = _ui.ShowDiceRoll(new DiceRollRequest
                    {
                        player = player,
                        result = newRoll,
                        context = "薛定谔重投结果"
                    });
                    yield return WaitFor(showCb2);
                    roll = newRoll;
                }
            }

            onResult(roll);
        }

        // =============================================================
        // 阶段4: 修正链（异步，每个修正玩家都要交互）
        // =============================================================

        private IEnumerator Phase4_ModificationChainAsync(PlayerState player, int rawRoll,
            Action<int> onResult)
        {
            int currentValue = rawRoll;
            int playerIdx = _allPlayers.IndexOf(player);

            // 按修正顺序遍历
            for (int offset = 1; offset <= _allPlayers.Count; offset++)
            {
                int idx = (playerIdx + offset) % _allPlayers.Count;
                var modPlayer = _allPlayers[idx];

                // 力修正
                var forceCards = modPlayer.handCards.Where(c =>
                    c.cardId == PhysicsCardId.Force && !c.isUsed && !c.forceUsedThisRound).ToList();

                foreach (var fc in forceCards)
                {
                    var forceCb = _ui.AskForceModification(new ForceModRequest
                    {
                        sourcePlayer = modPlayer,
                        dicePlayer = player,
                        currentDiceValue = currentValue,
                        forceCard = fc,
                        hasPrincipia = modPlayer.hasPrincipia
                    });
                    yield return WaitFor(forceCb);

                    if (forceCb.Result.useForce)
                    {
                        // 检查压强无效化
                        bool nullified = false;
                        foreach (var p in _allPlayers)
                        {
                            if (p.HasCard(PhysicsCardId.Pressure))
                            {
                                var pressureCb = _ui.AskPressureNullify(new PressureNullifyRequest
                                {
                                    player = p,
                                    currentDiceValue = currentValue,
                                    modDescription = $"{modPlayer.playerName}的力（{forceCb.Result.direction:+#;-#}）"
                                });
                                yield return WaitFor(pressureCb);
                                if (pressureCb.Result)
                                {
                                    nullified = true;
                                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                                        $"{p.playerName}使用压强无效化了力修正", p));
                                    break;
                                }
                            }
                        }

                        if (!nullified)
                        {
                            currentValue += forceCb.Result.direction;
                            _ui.SendNotification(new GameNotification(NotificationType.Info,
                                $"{modPlayer.playerName}的力修正：{forceCb.Result.direction:+#;-#}→{currentValue}",
                                modPlayer));
                        }
                        fc.forceUsedThisRound = true;
                    }
                }

                // 掷骰者自己的加速度修正
                if (idx == playerIdx)
                {
                    var accelCards = modPlayer.handCards.Where(c =>
                        c.cardId == PhysicsCardId.Acceleration && !c.isUsed).ToList();

                    foreach (var ac in accelCards)
                    {
                        var accelCb = _ui.AskAccelModification(new AccelModRequest
                        {
                            player = modPlayer,
                            currentDiceValue = currentValue,
                            accelCard = ac
                        });
                        yield return WaitFor(accelCb);

                        if (accelCb.Result.useAccel)
                        {
                            currentValue += accelCb.Result.direction;
                            _ui.SendNotification(new GameNotification(NotificationType.Info,
                                $"加速度修正：{accelCb.Result.direction:+#;-#}→{currentValue}", player));
                        }
                    }
                }
            }

            // 弹性系数钳制
            foreach (var p in _allPlayers)
            {
                if (p.HasCard(PhysicsCardId.SpringConstant))
                {
                    int before = currentValue;
                    currentValue = Mathf.Clamp(currentValue, 3, 4);
                    if (before != currentValue)
                        _ui.NotifySpringClamp(new SpringClampNotice
                            { player = p, beforeClamp = before, afterClamp = currentValue });
                    break;
                }
            }

            currentValue = Mathf.Max(1, currentValue);
            onResult(currentValue);
        }

        // =============================================================
        // 阶段6: 移动
        // =============================================================

        private bool MovePlayer(PlayerState player, int steps)
        {
            bool passedStart = false;
            int totalTiles = _board.TotalTiles;
            int dir = player.moveDirection == MoveDirection.Clockwise ? 1 : -1;

            // 清除上一次的待收费记录
            player.pendingTollTiles?.Clear();

            for (int step = 0; step < steps; step++)
            {
                int prevPos = player.position;
                int nextPos = (player.position + dir + totalTiles) % totalTiles;
                if (nextPos == 0 && player.position != 0)
                    passedStart = true;

                player.position = nextPos;

                // 磁通量面检测
                if (_magneticFlux != null)
                {
                    var rewards = _magneticFlux.OnPlayerMoved(prevPos, nextPos);
                    foreach (var (ownerIdx, reward) in rewards)
                    {
                        _allPlayers[ownerIdx].mol += reward;
                        _ui.SendNotification(new GameNotification(NotificationType.MolChange,
                            $"磁通量面完成！{_allPlayers[ownerIdx].playerName}获得{reward}mol",
                            _allPlayers[ownerIdx]));
                    }
                }

                if (step < steps - 1)
                {
                    _passive.OnPassTile(player, nextPos, _board, _allPlayers);
                    if (player.stoppedByRoadblock) break;

                    // 经过建筑格：记录待收费
                    if (_buildingManager != null)
                    {
                        var building = _buildingManager.GetBuildingAt(nextPos);
                        if (building != null && building.ownerPlayerIndex != player.playerIndex)
                        {
                            player.pendingTollTiles ??= new List<int>();
                            player.pendingTollTiles.Add(nextPos);
                        }
                    }
                }
            }
            return passedStart;
        }

        /// <summary>处理玩家经过建筑格时的异步收费</summary>
        private IEnumerator ProcessPassingTollsAsync(PlayerState player)
        {
            if (_buildingManager == null) yield break;
            if (player.pendingTollTiles == null || player.pendingTollTiles.Count == 0) yield break;

            foreach (int tileIdx in player.pendingTollTiles)
            {
                yield return _buildingManager.ProcessBuildingToll(player, tileIdx, _allPlayers);
            }
            player.pendingTollTiles.Clear();
            _ui.UpdateHUD(_allPlayers, _allPlayers.IndexOf(player), _eraRoundCount, _currentEra);
        }

        // =============================================================
        // 阶段7: 落点效果（异步）
        // =============================================================

        private IEnumerator Phase7_LandingEffectAsync(PlayerState player, TurnResult result,
            Action<bool> onRollback)
        {
            if (player.phaseState == PhaseState.Gas)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "气态：跳过落点效果", player));
                onRollback(false);
                yield break;
            }

            var tile = _board.GetTile(player.position);
            if (tile == null) { onRollback(false); yield break; }

            // ▸ 新增：落点建筑收费（落在他人有建筑的格子上）
            if (_buildingManager != null)
            {
                yield return _buildingManager.ProcessBuildingToll(player, player.position, _allPlayers);
                _ui.UpdateHUD(_allPlayers, _allPlayers.IndexOf(player), _eraRoundCount, _currentEra);
                if (_currentResult != null && _currentResult.gameOver)
                { onRollback(false); yield break; }
            }

            switch (tile.tileType)
            {
                case TileType.Territory:
                    yield return ProcessTerritoryAsync(player, tile);
                    break;

                case TileType.Shop:
                    yield return ProcessShopAsync(player, tile);
                    break;

                case TileType.Reward:
                    yield return ProcessRewardAsync(player);
                    break;

                case TileType.Event:
                    bool rollback = false;
                    yield return ProcessEventAsync(player, (rb) => rollback = rb);
                    if (rollback) { onRollback(true); yield break; }
                    break;

                case TileType.Supply:
                    yield return ProcessSupplyAsync(player, tile);
                    break;
            }

            _ui.UpdateHandDisplay(player);
            onRollback(false);
        }

        // --- 领地格 ---
        private IEnumerator ProcessTerritoryAsync(PlayerState player, BoardTile tile)
        {
            if (tile.ownerColor == player.color)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "自己的领地：可以合成卡牌", player));
                yield return SynthesisLoopAsync(player, unlimitedAttempts: true);
            }
        }

        // --- 商店格 ---
        private IEnumerator ProcessShopAsync(PlayerState player, BoardTile tile)
        {
            // 购买
            int randomPrice = _config.GetRandomCardPrice(_currentEra);
            int chosenPrice = _config.GetChosenCardPrice(_currentEra);

            var shopCb = _ui.ShowShop(new ShopPurchaseRequest
            {
                player = player,
                randomPrice = randomPrice,
                chosenPrice = chosenPrice,
                currentEra = _currentEra
            });
            yield return WaitFor(shopCb);

            var shopResult = shopCb.Result;
            if (shopResult.purchaseType == ShopPurchaseType.Random && player.mol >= randomPrice)
            {
                var cardId = _deck.DrawRandomBasic(UnityEngine.Random.Range(1, 7));
                player.mol -= randomPrice;
                player.GiveCard(cardId);
                _ui.SendNotification(new GameNotification(NotificationType.CardGained,
                    $"购买了{CardDatabase.Get(cardId)?.nameZH}（{randomPrice}mol）", player));
            }
            else if (shopResult.purchaseType == ShopPurchaseType.Chosen && player.mol >= chosenPrice)
            {
                player.mol -= chosenPrice;
                player.GiveCard(shopResult.chosenCardId);
                _ui.SendNotification(new GameNotification(NotificationType.CardGained,
                    $"购买了{CardDatabase.Get(shopResult.chosenCardId)?.nameZH}（{chosenPrice}mol）", player));
            }

            // 合成（自己商店不限次数，他人商店也可以合成一次）
            bool isOwnShop = tile.ownerColor == player.color;
            if (isOwnShop)
                yield return SynthesisLoopAsync(player, unlimitedAttempts: true);

            // 他人商店：交易
            if (!isOwnShop)
            {
                var shopOwner = _allPlayers.FirstOrDefault(p => p.color == tile.ownerColor);
                if (shopOwner != null)
                {
                    var tradeCb = _ui.ShowTrade(new TradeRequest
                    {
                        buyer = player,
                        seller = shopOwner
                    });
                    yield return WaitFor(tradeCb);
                    // 交易处理由TradePanel内部完成
                }
            }
        }

        // --- 补给格 ---
        private IEnumerator ProcessSupplyAsync(PlayerState player, BoardTile tile)
        {
            if (tile.ownerColor == player.color)
            {
                if (!player.IsHandFull())
                {
                    var basicCb = _ui.SelectBasicCards(new BasicCardChoiceRequest
                    {
                        player = player,
                        title = "补给：任选一张基本物理量",
                        count = 1
                    });
                    yield return WaitFor(basicCb);
                    if (basicCb.Result.chosenCards.Count > 0)
                        player.GiveCard(basicCb.Result.chosenCards[0]);
                }
                yield return SynthesisLoopAsync(player, unlimitedAttempts: true);
            }
            else
            {
                if (!player.IsHandFull())
                {
                    int roll = _dice.RollUnmodified();
                    var cardId = _deck.DrawRandomBasic(roll);
                    player.GiveCard(cardId);
                    _ui.SendNotification(new GameNotification(NotificationType.CardGained,
                        $"他人补给格：随机获得{CardDatabase.Get(cardId)?.nameZH}", player));
                }
            }
        }

        // --- 奖励格 ---
        private IEnumerator ProcessRewardAsync(PlayerState player)
        {
            var rewardId = _deck.DrawReward();
            var def = RewardCardDatabase.Get(rewardId);

            var showCb = _ui.ShowRewardCard(new RewardCardShowRequest
            {
                rewardId = rewardId,
                player = player,
                effectDescription = def?.descriptionZH
            });
            yield return WaitFor(showCb);

            // 需要UI交互的奖励牌特殊处理
            switch (rewardId)
            {
                case RewardCardId.TopPaper:
                    if (!player.IsHandFull())
                    {
                        var basicCb = _ui.SelectBasicCards(new BasicCardChoiceRequest
                        {
                            player = player,
                            title = "顶级论文：选择一种基本物理量",
                            count = 1
                        });
                        yield return WaitFor(basicCb);
                        if (basicCb.Result.chosenCards.Count > 0)
                            player.GiveCard(basicCb.Result.chosenCards[0]);
                    }
                    break;

                case RewardCardId.Square:
                    if (player.handCards.Count > 0 && !player.IsHandFull())
                    {
                        var selectCb = _ui.SelectCards(new CardSelectRequest
                        {
                            player = player,
                            title = "平方：选择一张卡牌复制",
                            message = "选择一张手牌，获得它的副本",
                            minSelect = 1,
                            maxSelect = 1,
                            filter = c => !c.isUsed
                        });
                        yield return WaitFor(selectCb);
                        if (!selectCb.Result.cancelled && selectCb.Result.selectedCards.Count > 0)
                            player.GiveCard(selectCb.Result.selectedCards[0].cardId);
                    }
                    break;

                case RewardCardId.Laboratory:
                case RewardCardId.ResearchInstitute:
                case RewardCardId.LargeCollider:
                    // 建筑放置需要选格子
                    BuildingType bType = rewardId == RewardCardId.Laboratory ? BuildingType.Laboratory
                        : rewardId == RewardCardId.ResearchInstitute ? BuildingType.ResearchInstitute
                        : BuildingType.LargeCollider;

                    var ownedTiles = _board.tiles.Where(t =>
                        (t.tileType == TileType.Territory || t.tileType == TileType.Shop) &&
                        t.ownerColor == player.color && t.buildings.Count == 0).ToList();

                    if (ownedTiles.Count > 0)
                    {
                        var tileCb = _ui.SelectTile(new TileSelectRequest
                        {
                            player = player,
                            title = $"建造{bType}：选择格子",
                            filter = idx => ownedTiles.Any(t => t.index == idx)
                        });
                        yield return WaitFor(tileCb);

                        int selIdx = tileCb.Result.selectedTileIndex;
                        if (selIdx >= 0)
                        {
                            _board.PlaceBuilding(bType, selIdx, player.playerIndex);
                            player.buildings.Add(new BuildingInstance(bType, selIdx, player.playerIndex));
                        }
                    }
                    break;

                case RewardCardId.Electromagnet: // #3 电磁铁：消耗电流牌放置
                {
                    var currentCard = player.handCards.FirstOrDefault(
                        c => c.cardId == PhysicsCardId.Current && !c.isUsed);
                    if (currentCard != null)
                    {
                        var confirmCb = _ui.ShowConfirm(
                            "电磁铁", "消耗1张电流牌激活电磁铁（3回合）？", player);
                        yield return WaitFor(confirmCb);
                        if (confirmCb.Result)
                        {
                            player.RemoveCard(currentCard);
                            var tileCb = _ui.SelectTile(new TileSelectRequest
                            {
                                player = player,
                                title = "电磁铁：选择放置位置",
                                filter = null
                            });
                            yield return WaitFor(tileCb);
                            if (tileCb.Result.selectedTileIndex >= 0)
                            {
                                player.electromagnetTurns = 3;
                                player.electromagnetPosition = tileCb.Result.selectedTileIndex;
                                _ui.SendNotification(new GameNotification(NotificationType.Info,
                                    $"电磁铁已激活在格子{tileCb.Result.selectedTileIndex}，持续3回合", player));
                            }
                        }
                    }
                    else
                    {
                        _ui.SendNotification(new GameNotification(NotificationType.Info,
                            "电磁铁：没有电流牌，无法激活", player));
                    }
                    break;
                }

                case RewardCardId.Generator: // #17 发电机：消耗力学量获得2张电流
                {
                    var mechCards = player.handCards.Where(c =>
                    {
                        var d = CardDatabase.Get(c.cardId);
                        return d != null && d.branch == PhysicsBranch.Mechanics && !c.isUsed;
                    }).ToList();

                    if (mechCards.Count > 0)
                    {
                        var selectCb = _ui.SelectCards(new CardSelectRequest
                        {
                            player = player,
                            title = "发电机：选择消耗一张力学量牌",
                            message = "消耗后获得2张电流",
                            minSelect = 1,
                            maxSelect = 1,
                            filter = c =>
                            {
                                var d = CardDatabase.Get(c.cardId);
                                return d != null && d.branch == PhysicsBranch.Mechanics && !c.isUsed;
                            }
                        });
                        yield return WaitFor(selectCb);
                        if (!selectCb.Result.cancelled && selectCb.Result.selectedCards.Count > 0)
                        {
                            player.RemoveCard(selectCb.Result.selectedCards[0]);
                            player.GiveCard(PhysicsCardId.Current);
                            player.GiveCard(PhysicsCardId.Current);
                            _ui.SendNotification(new GameNotification(NotificationType.CardGained,
                                "发电机：获得2张电流", player));
                        }
                    }
                    else
                    {
                        _ui.SendNotification(new GameNotification(NotificationType.Info,
                            "发电机：没有力学量牌", player));
                    }
                    break;
                }

                default:
                    // 其他奖励牌使用同步处理
                    _rewardProcessor.ProcessReward(rewardId, player, _allPlayers, _board);
                    break;
            }
        }

        // --- 事件格 ---
        private IEnumerator ProcessEventAsync(PlayerState player, Action<bool> onRollback)
        {
            var eventId = _deck.DrawEvent();
            yield return ProcessEventByIdAsync(player, eventId, onRollback);
        }

        /// <summary>处理指定事件牌（供熵等预抽事件使用）</summary>
        private IEnumerator ProcessEventByIdAsync(PlayerState player, EventCardId eventId, Action<bool> onRollback)
        {
            var eventDef = EventCardDatabase.Get(eventId);

            var showCb = _ui.ShowEventCard(new EventCardShowRequest
            {
                eventId = eventId,
                player = player,
                effectDescription = eventDef?.descriptionZH
            });
            yield return WaitFor(showCb);

            // 金皇冠免于不幸
            if (eventDef.isNegative && player.hasGoldenCrown)
            {
                player.hasGoldenCrown = false;
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    $"金皇冠免于不幸事件：{eventDef.nameZH}", player));
                onRollback(false);
                yield break;
            }

            // 需要UI交互的事件特殊处理
            switch (eventId)
            {
                case EventCardId.PhaseTransition:
                    var phaseCb = _ui.AskPhaseChoice(new PhaseChoiceRequest { player = player });
                    yield return WaitFor(phaseCb);
                    _eventProcessor.ApplyPhaseTransition(player, phaseCb.Result.chosenPhase);
                    break;

                case EventCardId.FeynmanBet:
                    var betCb = _ui.AskFeynmanBet(new FeynmanBetRequest { player = player });
                    yield return WaitFor(betCb);
                    int betRoll = _dice.RollUnmodified();
                    bool correct = (betCb.Result.guessOdd == (betRoll % 2 != 0));

                    var betDiceCb = _ui.ShowDiceRoll(new DiceRollRequest
                    {
                        player = player, result = betRoll,
                        context = correct ? "猜对了！+10mol" : "猜错了！-10mol"
                    });
                    yield return WaitFor(betDiceCb);

                    if (correct) player.mol += 10;
                    else player.mol = Math.Max(0, player.mol - 10);
                    break;

                case EventCardId.QuantumTunneling:
                    var tunelCb = _ui.SelectTile(new TileSelectRequest
                    {
                        player = player,
                        title = "量子隧穿：选择传送目标",
                        filter = null // 任何格子都可以
                    });
                    yield return WaitFor(tunelCb);
                    int targetTile = tunelCb.Result.selectedTileIndex;
                    if (targetTile >= 0)
                    {
                        var tpCb = _ui.AnimateTeleport(player, player.position, targetTile);
                        yield return WaitFor(tpCb);
                        player.position = targetTile;
                    }
                    player.quantumTunnelingUsed = true;
                    break;

                case EventCardId.NuclearReactor:
                    yield return ProcessNuclearReactorAsync(player);
                    break;

                case EventCardId.FranckHertz:
                    yield return ProcessFranckHertzAsync(player);
                    break;

                default:
                    // 其他事件使用同步处理
                    bool rb = _eventProcessor.ProcessEvent(eventId, player, _allPlayers, _currentSnapshot);
                    if (rb) { onRollback(true); yield break; }
                    break;
            }

            // 事件后创举标记检查
            if (player.singularityLostAll)
            {
                _achievementChecker.CheckBigBang(player);
                player.singularityLostAll = false;
            }
            if (player.quantumTunnelingUsed)
            {
                _achievementChecker.CheckQuantumMechanics(player);
                player.quantumTunnelingUsed = false;
            }
            if (player.superconductorTriggered)
            {
                _achievementChecker.CheckSuperconductivity(player);
                player.superconductorTriggered = false;
            }

            onRollback(false);
        }

        // --- 核反应堆（异步循环）---
        private IEnumerator ProcessNuclearReactorAsync(PlayerState player)
        {
            int reward = 1;
            bool continueRolling = true;

            while (continueRolling)
            {
                var contCb = _ui.AskNuclearContinue(new NuclearContinueRequest
                {
                    player = player, currentReward = reward, lastRoll = 0
                });
                yield return WaitFor(contCb);

                if (!contCb.Result.continueRolling)
                {
                    player.mol += reward;
                    _ui.SendNotification(new GameNotification(NotificationType.MolChange,
                        $"核反应堆：获得{reward}mol", player));
                    break;
                }

                int roll = _dice.RollUnmodified();
                var diceCb = _ui.ShowDiceRoll(new DiceRollRequest
                {
                    player = player, result = roll, context = "核反应堆"
                });
                yield return WaitFor(diceCb);

                if (roll >= 1 && roll <= 4)
                {
                    reward *= 2;
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        $"核反应堆掷骰{roll}：奖励翻倍至{reward}mol", player));
                }
                else
                {
                    player.mol = 0;
                    _ui.SendNotification(new GameNotification(NotificationType.Warning,
                        $"核反应堆掷骰{roll}：失败！失去所有mol", player));
                    continueRolling = false;
                }
            }
        }

        // --- 弗兰克-赫兹（异步展示3次骰子）---
        private IEnumerator ProcessFranckHertzAsync(PlayerState player)
        {
            int[] rolls = new int[3];
            for (int i = 0; i < 3; i++)
            {
                rolls[i] = _dice.RollUnmodified();
                var diceCb = _ui.ShowDiceRoll(new DiceRollRequest
                {
                    player = player, result = rolls[i],
                    context = $"弗兰克-赫兹实验 第{i + 1}/3次"
                });
                yield return WaitFor(diceCb);
            }

            bool inc = rolls[0] < rolls[1] && rolls[1] < rolls[2];
            bool dec = rolls[0] > rolls[1] && rolls[1] > rolls[2];

            if (inc || dec)
            {
                player.mol += 20;
                _ui.SendNotification(new GameNotification(NotificationType.MolChange,
                    $"弗兰克-赫兹成功！+20mol", player));
            }
            else
            {
                player.mol = Math.Max(0, player.mol - 5);
                _ui.SendNotification(new GameNotification(NotificationType.Warning,
                    $"弗兰克-赫兹失败！-5mol", player));
            }
        }

        // =============================================================
        // 阶段8: 自由行动（异步循环）
        // =============================================================

        private IEnumerator Phase8_FreeActionAsync(PlayerState player)
        {
            bool mustDiscard = player.handCards.Count > PlayerState.MaxHandCards;
            bool turnEnded = false;

            while (!turnEnded)
            {
                mustDiscard = player.handCards.Count > PlayerState.MaxHandCards;

                // 检查可用行动
                var available = new List<FreeActionType> { FreeActionType.EndTurn };

                // 主动卡
                bool hasActive = player.handCards.Any(c =>
                    CardDatabase.Get(c.cardId)?.effectType == CardEffectType.Active && !c.isUsed);
                if (hasActive)
                    available.Add(FreeActionType.UseActiveCard);

                // 合成
                var handCardIds = player.handCards.Where(c => !c.isUsed).Select(c => c.cardId).ToList();
                var possibleSynths = _synthesis.FindPossibleSyntheses(handCardIds);
                if (possibleSynths.Count > 0)
                    available.Add(FreeActionType.Synthesize);

                // 创新项目
                var tripleGroups = player.handCards.Where(c => !c.isUsed)
                    .GroupBy(c => c.cardId).Where(g => g.Count() >= 3);
                if (tripleGroups.Any())
                    available.Add(FreeActionType.InnovationProject);

                if (mustDiscard)
                    available.Add(FreeActionType.DiscardCard);

                var actionCb = _ui.ShowFreeActionMenu(new FreeActionRequest
                {
                    player = player,
                    canSynthesize = possibleSynths.Count > 0,
                    canUseActive = hasActive,
                    mustDiscard = mustDiscard,
                    availableActions = available
                });
                yield return WaitFor(actionCb);

                var action = actionCb.Result.action;

                switch (action)
                {
                    case FreeActionType.EndTurn:
                        if (!mustDiscard)
                            turnEnded = true;
                        break;

                    case FreeActionType.Synthesize:
                        yield return SynthesisLoopAsync(player, unlimitedAttempts: false);
                        break;

                    case FreeActionType.UseActiveCard:
                        yield return ProcessActiveCardAsync(player);
                        break;

                    case FreeActionType.DiscardCard:
                        var discardCb = _ui.SelectCards(new CardSelectRequest
                        {
                            player = player,
                            title = "弃牌",
                            message = $"手牌超出上限，需要弃到{PlayerState.MaxHandCards}张",
                            minSelect = 1,
                            maxSelect = player.handCards.Count - PlayerState.MaxHandCards,
                            filter = null
                        });
                        yield return WaitFor(discardCb);
                        if (!discardCb.Result.cancelled)
                        {
                            foreach (var card in discardCb.Result.selectedCards)
                                player.RemoveCard(card);
                        }
                        break;

                    case FreeActionType.InnovationProject:
                        yield return ProcessInnovationProjectAsync(player);
                        break;
                }

                _ui.UpdateHandDisplay(player);
                _ui.UpdateHUD(_allPlayers, _allPlayers.IndexOf(player), _eraRoundCount, _currentEra);
            }
        }

        // =============================================================
        // 容器存入处理（面积/体积卡获取后询问存入mol）
        // =============================================================
        // 主动/抉择卡牌使用
        // =============================================================

        /// <summary>主动卡牌使用流程</summary>
        private IEnumerator ProcessActiveCardAsync(PlayerState player)
        {
            // 1. 选择要使用的主动卡
            var cardCb = _ui.SelectCards(new CardSelectRequest
            {
                player = player,
                title = "选择要使用的主动/抉择卡牌",
                message = "",
                minSelect = 1,
                maxSelect = 1,
                filter = c =>
                {
                    var def = CardDatabase.Get(c.cardId);
                    return def != null
                        && (def.effectType == CardEffectType.Active || def.effectType == CardEffectType.Choice)
                        && !c.isUsed;
                }
            });
            yield return WaitFor(cardCb);
            if (cardCb.Result.cancelled || cardCb.Result.selectedCards.Count == 0)
                yield break;

            var card = cardCb.Result.selectedCards[0];
            var cardDef = CardDatabase.Get(card.cardId);

            // 牛顿技能：主动效果可释放两次
            int useCount = 1;
            if (player.characterTaskCompleted && player.character == Character.Newton
                && cardDef.effectType == CardEffectType.Active)
                useCount = 2;

            for (int use = 0; use < useCount; use++)
            {
                switch (card.cardId)
                {
                    // ---- 主动卡 ----

                    case PhysicsCardId.Velocity: // 速度：额外回合
                        player.potentialExtraTurns++;
                        _ui.SendNotification(new GameNotification(NotificationType.Info,
                            "速度：获得额外行动回合", player));
                        break;

                    case PhysicsCardId.Momentum: // 动量：撞晕同格玩家3回合
                        var samePos = _allPlayers.Where(p => p != player && p.position == player.position).ToList();
                        if (samePos.Count > 0)
                        {
                            var targetCb = _ui.SelectTargetPlayer(new PlayerTargetRequest
                            {
                                player = player,
                                title = "动量：选择撞晕目标",
                                candidates = samePos
                            });
                            yield return WaitFor(targetCb);
                            if (targetCb.Result.selectedPlayer != null)
                            {
                                targetCb.Result.selectedPlayer.stunTurns = 3;
                                _ui.SendNotification(new GameNotification(NotificationType.Info,
                                    $"动量：撞晕了{targetCb.Result.selectedPlayer.playerName}（3回合）", player));
                            }
                        }
                        else
                        {
                            _ui.SendNotification(new GameNotification(NotificationType.Info,
                                "动量：同格没有其他玩家", player));
                        }
                        break;

                    case PhysicsCardId.Torque: // 力矩：旋转传牌
                        yield return ProcessTorqueAsync(player);
                        break;

                    case PhysicsCardId.MomentOfInertia: // 转动惯量：调转自己方向
                        player.moveDirection = player.moveDirection == MoveDirection.Clockwise
                            ? MoveDirection.CounterClockwise : MoveDirection.Clockwise;
                        _ui.SendNotification(new GameNotification(NotificationType.Info,
                            $"转动惯量：方向变为{(player.moveDirection == MoveDirection.Clockwise ? "顺时针" : "逆时针")}", player));
                        break;

                    case PhysicsCardId.AngularMomentum: // 角动量：调转他人方向
                        var amTargetCb = _ui.SelectTargetPlayer(new PlayerTargetRequest
                        {
                            player = player,
                            title = "角动量：选择调转方向的目标",
                            candidates = _allPlayers.Where(p => p != player).ToList()
                        });
                        yield return WaitFor(amTargetCb);
                        if (amTargetCb.Result.selectedPlayer != null)
                        {
                            var t = amTargetCb.Result.selectedPlayer;
                            t.moveDirection = t.moveDirection == MoveDirection.Clockwise
                                ? MoveDirection.CounterClockwise : MoveDirection.Clockwise;
                            _ui.SendNotification(new GameNotification(NotificationType.Info,
                                $"角动量：{t.playerName}方向变为{(t.moveDirection == MoveDirection.Clockwise ? "顺时针" : "逆时针")}", player));
                        }
                        break;

                    case PhysicsCardId.Entropy: // 熵：抽5张事件牌，弃1张，执行其余
                        yield return ProcessEntropyAsync(player);
                        break;

                    case PhysicsCardId.CalorificValue: // 热值：燃烧所有手牌，每张2mol
                    {
                        int burnCount = player.handCards.Count; // 包括热值自身
                        int molGain = burnCount * 2;
                        player.mol += molGain;
                        player.handCards.Clear();
                        _ui.SendNotification(new GameNotification(NotificationType.CardGained,
                            $"热值：燃烧{burnCount}张牌，获得{molGain}mol", player));
                        _ui.UpdateHandDisplay(player);
                        yield break; // 手牌全清，直接退出
                    }

                    case PhysicsCardId.Resistance: // 电阻：释放路障
                    {
                        var tileCb = _ui.SelectTile(new TileSelectRequest
                        {
                            player = player,
                            title = "电阻：选择放置路障的位置",
                            filter = null
                        });
                        yield return WaitFor(tileCb);
                        if (tileCb.Result.selectedTileIndex >= 0)
                        {
                            int lightBonus = player.CalculateLightBonus();
                            int count = 1 + lightBonus;
                            if (player.characterTaskCompleted && player.character == Character.Newton)
                                count *= 2;
                            int baseIdx = tileCb.Result.selectedTileIndex;
                            for (int i = 0; i < count; i++)
                                _board.PlaceRoadblock(baseIdx + i);
                            _ui.SendNotification(new GameNotification(NotificationType.Info,
                                $"电阻：放置了{count}个路障", player));
                        }
                        break;
                    }

                    case PhysicsCardId.Charge: // 电荷：激活，下次经过起点+2电流
                        card.chargeActivated = true;
                        _ui.SendNotification(new GameNotification(NotificationType.Info,
                            "电荷已激活：下次经过起点+2电流", player));
                        break;

                    // ---- 抉择卡 ----

                    case PhysicsCardId.Energy:    // 能量
                    case PhysicsCardId.Work:      // 功
                    case PhysicsCardId.Heat:      // 热量
                        yield return ProcessEnergyChoiceAsync(player, card);
                        break;

                    case PhysicsCardId.Density: // 密度：选沉重或轻盈
                        yield return ProcessDensityChoiceAsync(player, card);
                        break;

                    case PhysicsCardId.SpecificHeat: // 比热容：掠夺或给予
                        yield return ProcessSpecificHeatChoiceAsync(player, card);
                        break;

                    default:
                        _ui.SendNotification(new GameNotification(NotificationType.Info,
                            $"使用了{cardDef?.nameZH ?? card.cardId.ToString()}", player));
                        break;
                }
            }

            // 标记卡牌已使用
            card.isUsed = true;
        }

        /// <summary>力矩：所有玩家按方向传一张手牌给下一位</summary>
        private IEnumerator ProcessTorqueAsync(PlayerState user)
        {
            // 选择方向
            var dirCb = _ui.ShowChoice(new ChoiceRequest
            {
                title = "力矩：选择传牌方向",
                message = "所有玩家将一张手牌传给该方向的下一位玩家",
                options = new List<ChoiceOption>
                {
                    new ChoiceOption("cw", "顺时针", ""),
                    new ChoiceOption("ccw", "逆时针", "")
                },
                allowCancel = false,
                player = user
            });
            yield return WaitFor(dirCb);
            bool clockwise = dirCb.Result == "cw";

            // 每位玩家选择要传出的卡牌
            var cardsToPass = new CardInstance[_allPlayers.Count];
            for (int i = 0; i < _allPlayers.Count; i++)
            {
                var p = _allPlayers[i];
                if (p.handCards.Count == 0) continue;

                // 使用者可以选自己想要留下的（实际是选出要传出的）
                var passCb = _ui.SelectCards(new CardSelectRequest
                {
                    player = p,
                    title = $"力矩传牌：选择一张手牌传出",
                    message = clockwise ? "传给顺时针方向的下一位" : "传给逆时针方向的下一位",
                    minSelect = 1,
                    maxSelect = 1,
                    filter = c => !c.isUsed
                });
                yield return WaitFor(passCb);

                if (!passCb.Result.cancelled && passCb.Result.selectedCards.Count > 0)
                    cardsToPass[i] = passCb.Result.selectedCards[0];
            }

            // 执行传递
            int count = _allPlayers.Count;
            for (int i = 0; i < count; i++)
            {
                if (cardsToPass[i] == null) continue;
                int nextIdx = clockwise ? (i + 1) % count : (i - 1 + count) % count;
                _allPlayers[i].RemoveCard(cardsToPass[i]);
                _allPlayers[nextIdx].handCards.Add(cardsToPass[i]);
            }
            _ui.SendNotification(new GameNotification(NotificationType.Info,
                $"力矩：{(clockwise ? "顺时针" : "逆时针")}传牌完成", user));
        }

        /// <summary>熵：抽5张事件牌，弃1张，执行其余</summary>
        private IEnumerator ProcessEntropyAsync(PlayerState player)
        {
            player.entropyUseCount++;

            // 抽5张
            var drawnEvents = new List<EventCardId>();
            for (int i = 0; i < 5; i++)
                drawnEvents.Add(_deck.DrawEvent());

            // 构建选项让玩家选弃哪一张
            var options = new List<ChoiceOption>();
            for (int i = 0; i < drawnEvents.Count; i++)
            {
                var def = EventCardDatabase.Get(drawnEvents[i]);
                options.Add(new ChoiceOption(
                    i.ToString(),
                    def?.nameZH ?? drawnEvents[i].ToString(),
                    def?.descriptionZH ?? ""
                ));
            }
            options.Add(new ChoiceOption("-1", "不弃牌", "执行全部5张事件"));

            var discardCb = _ui.ShowChoice(new ChoiceRequest
            {
                title = "熵：选择弃掉一张事件牌",
                message = "抽到了5张事件牌。你可以弃掉其中1张不执行。",
                options = options,
                allowCancel = false,
                player = player
            });
            yield return WaitFor(discardCb);

            int discardIdx = -1;
            int.TryParse(discardCb.Result, out discardIdx);

            // 执行未弃掉的事件（使用已抽取的eventId，不再从牌堆抽）
            for (int i = 0; i < drawnEvents.Count; i++)
            {
                if (i == discardIdx) continue;
                bool rollback = false;
                yield return ProcessEventByIdAsync(player, drawnEvents[i], rb => rollback = rb);
                if (rollback) yield break;
            }
        }

        /// <summary>能量/功/热量 抉择：6mol 或 1张基本物理量</summary>
        private IEnumerator ProcessEnergyChoiceAsync(PlayerState player, CardInstance card)
        {
            int lightBonus = player.CalculateLightBonus();
            int molAmount = 6 + lightBonus;
            int multiplier = (player.characterTaskCompleted && player.character == Character.Einstein) ? 2 : 1;

            var cardName = CardDatabase.Get(card.cardId)?.nameZH ?? "能量";
            var choiceCb = _ui.ShowChoice(new ChoiceRequest
            {
                title = $"{cardName}：选择效果",
                message = "",
                options = new List<ChoiceOption>
                {
                    new ChoiceOption("mol", $"兑换 {molAmount * multiplier} mol",
                        multiplier > 1 ? "（爱因斯坦翻倍）" : ""),
                    new ChoiceOption("card", $"任选 {1 * multiplier} 张基本物理量",
                        multiplier > 1 ? "（爱因斯坦翻倍）" : "")
                },
                allowCancel = false,
                player = player
            });
            yield return WaitFor(choiceCb);

            if (choiceCb.Result == "mol")
            {
                player.mol += molAmount * multiplier;
                _ui.SendNotification(new GameNotification(NotificationType.MolChange,
                    $"{cardName}：+{molAmount * multiplier}mol", player));
            }
            else
            {
                for (int i = 0; i < 1 * multiplier; i++)
                {
                    var basicCb = _ui.SelectBasicCards(new BasicCardChoiceRequest
                    {
                        player = player,
                        title = $"{cardName}：选择基本物理量（{i + 1}/{1 * multiplier}）",
                        count = 1
                    });
                    yield return WaitFor(basicCb);
                    if (basicCb.Result.chosenCards.Count > 0)
                        player.GiveCard(basicCb.Result.chosenCards[0]);
                }
            }
        }

        /// <summary>密度 抉择：给目标施加沉重或轻盈</summary>
        private IEnumerator ProcessDensityChoiceAsync(PlayerState player, CardInstance card)
        {
            bool bothChoices = player.characterTaskCompleted && player.character == Character.Maxwell;

            // 选择目标
            var targetCb = _ui.SelectTargetPlayer(new PlayerTargetRequest
            {
                player = player,
                title = "密度：选择目标玩家",
                candidates = _allPlayers.Where(p => p != player).ToList()
            });
            yield return WaitFor(targetCb);
            if (targetCb.Result.selectedPlayer == null) yield break;
            var target = targetCb.Result.selectedPlayer;

            if (bothChoices)
            {
                // 麦克斯韦：两项都生效
                target.densityHeavyTurns = 3;
                target.densityLightTurns = 3;
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    $"密度（双重）：{target.playerName}同时获得沉重+轻盈3回合", player));
            }
            else
            {
                var choiceCb = _ui.ShowChoice(new ChoiceRequest
                {
                    title = "密度：选择效果",
                    message = $"目标：{target.playerName}",
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption("heavy", "沉重（3回合移动减半）", ""),
                        new ChoiceOption("light", "轻盈（3回合移动翻倍）", "")
                    },
                    allowCancel = false,
                    player = player
                });
                yield return WaitFor(choiceCb);

                if (choiceCb.Result == "heavy")
                {
                    target.densityHeavyTurns = 3;
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        $"密度：{target.playerName}获得3回合沉重", player));
                }
                else
                {
                    target.densityLightTurns = 3;
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        $"密度：{target.playerName}获得3回合轻盈", player));
                }
            }
        }

        /// <summary>比热容 抉择：掠夺富人 或 接济穷人</summary>
        private IEnumerator ProcessSpecificHeatChoiceAsync(PlayerState player, CardInstance card)
        {
            bool bothChoices = player.characterTaskCompleted && player.character == Character.Maxwell;

            if (bothChoices)
            {
                // 麦克斯韦：两项都执行
                // 掠夺
                var richerPlayers = _allPlayers.Where(p => p != player && p.mol > player.mol).ToList();
                if (richerPlayers.Count > 0)
                {
                    var robCb = _ui.SelectTargetPlayer(new PlayerTargetRequest
                    {
                        player = player, title = "比热容-掠夺：选择mol更多的目标",
                        candidates = richerPlayers
                    });
                    yield return WaitFor(robCb);
                    if (robCb.Result.selectedPlayer != null)
                    {
                        int rob = Math.Min(4, robCb.Result.selectedPlayer.mol);
                        robCb.Result.selectedPlayer.mol -= rob;
                        player.mol += rob;
                    }
                }
                // 给予
                var poorerPlayers = _allPlayers.Where(p => p != player && p.mol < player.mol).ToList();
                if (poorerPlayers.Count > 0)
                {
                    var giveCb = _ui.SelectTargetPlayer(new PlayerTargetRequest
                    {
                        player = player, title = "比热容-给予：选择mol更少的目标",
                        candidates = poorerPlayers
                    });
                    yield return WaitFor(giveCb);
                    if (giveCb.Result.selectedPlayer != null)
                    {
                        int give = Math.Min(4, player.mol);
                        player.mol -= give;
                        giveCb.Result.selectedPlayer.mol += give;
                        yield return SelectTwoBasicCardsAsync(player, "比热容：选择2张基本物理量");
                    }
                }
            }
            else
            {
                var choiceCb = _ui.ShowChoice(new ChoiceRequest
                {
                    title = "比热容：选择效果",
                    message = "",
                    options = new List<ChoiceOption>
                    {
                        new ChoiceOption("rob", "掠夺：从mol更多的玩家处夺取4mol", ""),
                        new ChoiceOption("give", "给予：给mol更少的玩家4mol，自己获得2张基本物理量", "")
                    },
                    allowCancel = false,
                    player = player
                });
                yield return WaitFor(choiceCb);

                if (choiceCb.Result == "rob")
                {
                    var targets = _allPlayers.Where(p => p != player && p.mol > player.mol).ToList();
                    if (targets.Count > 0)
                    {
                        var tCb = _ui.SelectTargetPlayer(new PlayerTargetRequest
                        {
                            player = player, title = "比热容：选择掠夺目标",
                            candidates = targets
                        });
                        yield return WaitFor(tCb);
                        if (tCb.Result.selectedPlayer != null)
                        {
                            int rob = Math.Min(4, tCb.Result.selectedPlayer.mol);
                            tCb.Result.selectedPlayer.mol -= rob;
                            player.mol += rob;
                            _ui.SendNotification(new GameNotification(NotificationType.MolChange,
                                $"比热容：掠夺{rob}mol", player));
                        }
                    }
                }
                else
                {
                    var targets = _allPlayers.Where(p => p != player && p.mol < player.mol).ToList();
                    if (targets.Count > 0)
                    {
                        var tCb = _ui.SelectTargetPlayer(new PlayerTargetRequest
                        {
                            player = player, title = "比热容：选择给予目标",
                            candidates = targets
                        });
                        yield return WaitFor(tCb);
                        if (tCb.Result.selectedPlayer != null)
                        {
                            int give = Math.Min(4, player.mol);
                            player.mol -= give;
                            tCb.Result.selectedPlayer.mol += give;
                            yield return SelectTwoBasicCardsAsync(player, "比热容：选择2张基本物理量");
                        }
                    }
                }
            }
        }

        /// <summary>工具：让玩家选两张基本物理量</summary>
        private IEnumerator SelectTwoBasicCardsAsync(PlayerState player, string title)
        {
            for (int i = 0; i < 2; i++)
            {
                var cb = _ui.SelectBasicCards(new BasicCardChoiceRequest
                {
                    player = player,
                    title = $"{title}（{i + 1}/2）",
                    count = 1
                });
                yield return WaitFor(cb);
                if (cb.Result.chosenCards.Count > 0)
                    player.GiveCard(cb.Result.chosenCards[0]);
            }
        }

        /// <summary>创新项目：消耗3张同名卡牌，获得等值mol</summary>
        private IEnumerator ProcessInnovationProjectAsync(PlayerState player)
        {
            // 找出所有≥3张的同名牌组
            var tripleGroups = player.handCards.Where(c => !c.isUsed)
                .GroupBy(c => c.cardId).Where(g => g.Count() >= 3).ToList();

            if (tripleGroups.Count == 0)
            {
                _ui.SendNotification(new GameNotification(NotificationType.Info,
                    "没有3张以上同名牌", player));
                yield break;
            }

            // 让玩家选择消耗哪组
            var options = new List<ChoiceOption>();
            foreach (var g in tripleGroups)
            {
                var def = CardDatabase.Get(g.Key);
                string name = def?.nameZH ?? g.Key.ToString();
                int count = g.Count();
                options.Add(new ChoiceOption(
                    g.Key.ToString(),
                    $"{name} ×{count}",
                    $"消耗3张，获得mol奖励"
                ));
            }

            var choiceCb = _ui.ShowChoice(new ChoiceRequest
            {
                title = "创新项目：选择消耗的卡牌",
                message = "消耗3张同名卡牌执行创新项目",
                options = options,
                allowCancel = true,
                player = player
            });
            yield return WaitFor(choiceCb);

            if (string.IsNullOrEmpty(choiceCb.Result)) yield break;

            if (Enum.TryParse(choiceCb.Result, out PhysicsCardId chosenId))
            {
                // 移除3张
                int removed = 0;
                for (int i = player.handCards.Count - 1; i >= 0 && removed < 3; i--)
                {
                    if (player.handCards[i].cardId == chosenId && !player.handCards[i].isUsed)
                    {
                        player.RemoveCard(player.handCards[i]);
                        removed++;
                    }
                }

                // 奖励mol（基本物理量3mol，复合物理量6mol）
                int reward = CardDatabase.IsBasicQuantity(chosenId) ? 3 : 6;
                player.mol += reward;

                var def = CardDatabase.Get(chosenId);
                _ui.SendNotification(new GameNotification(NotificationType.MolChange,
                    $"创新项目：消耗3张{def?.nameZH}，获得{reward}mol", player));
            }
        }

        // =============================================================
        private IEnumerator ProcessPendingContainerDepositsAsync(PlayerState player)
        {
            var pendingCards = player.handCards.Where(c => c.pendingDeposit && !c.isUsed).ToList();
            foreach (var card in pendingCards)
            {
                int maxCapacity = PassiveEffectManager.GetContainerCapacity(card.cardId);
                if (maxCapacity <= 0) { card.pendingDeposit = false; continue; }

                int maxDeposit = Math.Min(maxCapacity, player.mol); // 不能存超过拥有的
                var cardName = CardDatabase.Get(card.cardId)?.nameZH ?? card.cardId.ToString();

                // 构建选项：0 ~ maxDeposit
                var options = new List<ChoiceOption>();
                for (int i = 0; i <= maxDeposit; i++)
                {
                    options.Add(new ChoiceOption(
                        i.ToString(),
                        $"存入 {i} mol",
                        i == 0 ? "不存入（容器翻倍将无效果）" : $"从你的{player.mol}mol中扣除{i}mol",
                        true
                    ));
                }

                var choiceCb = _ui.ShowChoice(new ChoiceRequest
                {
                    title = $"{cardName}容器",
                    message = $"你获得了{cardName}（容器上限{maxCapacity}mol）。\n要存入多少mol？每经过起点，容器中mol翻倍。",
                    options = options,
                    allowCancel = false,
                    player = player
                });
                yield return WaitFor(choiceCb);

                int deposit = 0;
                if (int.TryParse(choiceCb.Result, out deposit) && deposit > 0)
                {
                    deposit = Math.Min(deposit, Math.Min(maxCapacity, player.mol));
                    card.containerMol = deposit;
                    player.mol -= deposit;
                    Debug.Log($"[容器] {player.playerName} 向{cardName}存入{deposit}mol（剩余{player.mol}mol）");
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        $"向{cardName}存入{deposit}mol", player));
                }
                else
                {
                    Debug.Log($"[容器] {player.playerName} 选择不向{cardName}存入mol");
                }

                card.pendingDeposit = false; // 标记已处理
            }
        }

        // =============================================================
        // 合成循环
        // =============================================================

        private IEnumerator SynthesisLoopAsync(PlayerState player, bool unlimitedAttempts)
        {
            bool continueLoop = true;
            while (continueLoop)
            {
                // 检查是否有足够材料
                var unusedCards = player.handCards.Where(c => !c.isUsed).ToList();
                if (unusedCards.Count < 2)
                {
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        "手牌不足，无法合成", player));
                    break;
                }

                // ---- 第1步：选择材料卡 ----
                var matCb = _ui.SelectCards(new CardSelectRequest
                {
                    player = player,
                    title = "合成 — 选择材料",
                    message = "选择2张或更多卡牌作为合成材料",
                    minSelect = 2,
                    maxSelect = unusedCards.Count,
                    filter = c => !c.isUsed
                });
                yield return WaitFor(matCb);

                if (matCb.Result.cancelled || matCb.Result.selectedCards == null
                    || matCb.Result.selectedCards.Count < 2)
                {
                    continueLoop = false;
                    break;
                }

                var selectedMaterials = matCb.Result.selectedCards;

                // ---- 第2步：计算可能的产物 ----
                var possibleOutputs = _synthesis.GetPossibleOutputs(selectedMaterials);

                if (possibleOutputs.Count == 0)
                {
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        "这些材料无法合成任何物理量", player));
                    // 不退出循环，让玩家重新选择
                    continue;
                }

                PhysicsCardId chosenOutput;

                if (possibleOutputs.Count == 1)
                {
                    // 唯一产物，自动选择
                    chosenOutput = possibleOutputs[0];
                    var outName = CardDatabase.Get(chosenOutput)?.nameZH ?? chosenOutput.ToString();
                    _ui.SendNotification(new GameNotification(NotificationType.Info,
                        $"合成目标：{outName}", player));
                }
                else
                {
                    // ---- 第3步：多种产物，让玩家选择 ----
                    var options = new List<ChoiceOption>();
                    foreach (var outId in possibleOutputs)
                    {
                        var def = CardDatabase.Get(outId);
                        string name = def?.nameZH ?? outId.ToString();
                        string desc = def?.effectDescription ?? "";
                        options.Add(new ChoiceOption(outId.ToString(), name, desc));
                    }

                    var choiceCb = _ui.ShowChoice(new ChoiceRequest
                    {
                        title = "选择合成产物",
                        message = "这组材料可以合成多种物理量，请选择：",
                        options = options,
                        allowCancel = true,
                        player = player
                    });
                    yield return WaitFor(choiceCb);

                    // 取消选择 → 回到材料选择
                    if (string.IsNullOrEmpty(choiceCb.Result))
                        continue;

                    if (!Enum.TryParse(choiceCb.Result, out chosenOutput))
                        continue;
                }

                // ---- 第4步：执行合成 ----
                var output = _synthesis.ExecuteSynthesis(player, selectedMaterials, chosenOutput);

                _ui.SendNotification(new GameNotification(NotificationType.CardGained,
                    $"合成了{CardDatabase.Get(chosenOutput)?.nameZH}", player));

                // 如果合成产物是容器卡，询问存入
                yield return ProcessPendingContainerDepositsAsync(player);

                _ui.UpdateHandDisplay(player);

                if (!unlimitedAttempts) continueLoop = false;
            }
        }

        // =============================================================
        // 阶段9: 回合结束
        // =============================================================

        private void Phase9_TurnEnd(PlayerState player, TurnResult result)
        {
            var newAch = _achievementChecker.CheckAll(player, _allPlayers, _dice, result.diceRoll);
            result.newAchievements = newAch;

            foreach (var achId in newAch)
                _achievementChecker.GrantAchievementReward(achId, player);

            // ▸ 新增：角色任务检查
            if (_characterSystem != null)
                _characterSystem.CheckCharacterTask(player);

            // ▸ 新增：独立胜利检查
            if (_victoryChecker != null && !result.gameOver)
            {
                if (_victoryChecker.CheckVictory(player))
                {
                    result.gameOver = true;
                    result.winner = player;
                    result.victoryType = _victoryChecker.WinType;
                }
            }

            // ▸ 新增：爱因斯坦高速回合计数
            if (result.actualSteps >= 6)
                player.consecutiveHighSpeedTurns++;
            else
                player.consecutiveHighSpeedTurns = 0;

            // ▸ 新增：牛顿匀速运动检测
            if (player.recentMoveSteps.Count >= 3)
            {
                int last = player.recentMoveSteps.Count;
                if (player.recentMoveSteps[last - 1] == player.recentMoveSteps[last - 2] &&
                    player.recentMoveSteps[last - 2] == player.recentMoveSteps[last - 3] &&
                    player.recentMoveSteps[last - 1] > 0)
                {
                    player.newtonStage1Done = true;
                }
            }

            CheckEraAdvance();
            player.achievementDiceCheckPassed.Clear();
        }

        // =============================================================
        // 胜利/纪元检查（与原TurnManager相同）
        // =============================================================

        private void OnPlayerValueChanged(PlayerState player)
        {
            if (_currentResult == null || _currentResult.gameOver) return;

            if (player.mol >= _config.wealthVictoryMol)
            {
                _currentResult.gameOver = true;
                _currentResult.winner = player;
                _currentResult.victoryType = VictoryType.Wealth;
            }
            else if (player.achievementPoints >= _config.achievementVictoryPts)
            {
                _currentResult.gameOver = true;
                _currentResult.winner = player;
                _currentResult.victoryType = VictoryType.Achievement;
            }
        }

        private void CheckEraAdvance()
        {
            bool allDone = true;
            foreach (var a in _achievementChecker.ActiveAchievements)
                if (!_achievementChecker.GlobalCompleted.Contains(a)) { allDone = false; break; }

            bool eraEnds = allDone || _eraRoundCount >= _config.maxRoundsPerEra;
            if (!eraEnds) return;

            if (_currentEra == Era.NaturalPhilosophy)
            {
                SetEra(Era.ClassicalPhysics);
                _board.ExpandBoard(_allPlayers);
                _eraRoundCount = 0;
                DrawEraAchievements(Era.ClassicalPhysics);
            }
            else if (_currentEra == Era.ClassicalPhysics)
            {
                SetEra(Era.ModernPhysics);
                _eraRoundCount = 0;
                DrawEraAchievements(Era.ModernPhysics);
            }
        }

        private void DrawEraAchievements(Era era)
        {
            var all = AchievementDatabase.GetByEra(era);
            int count = _config.achievementsPerEra;
            var drawn = all.OrderBy(x => UnityEngine.Random.value)
                .Take(Math.Min(count, all.Count)).ToList();
            _achievementChecker.SetActiveAchievements(drawn);
        }

        public void OnRoundEnd()
        {
            foreach (var p in _allPlayers)
            {
                p.forceUsedThisRound = false;
                foreach (var c in p.handCards) c.forceUsedThisRound = false;
            }
            _eraRoundCount++;
        }

        public void InitializeFirstEra()
        {
            _eraRoundCount = 0;
            DrawEraAchievements(Era.NaturalPhilosophy);
        }

        private void Finish(PlayerState player, TurnResult result, TurnRecord rec)
        {
            _recorder.EndTurn(player.position, player.mol, player.handCards.Count,
                player.achievementPoints);
        }
    }
}
