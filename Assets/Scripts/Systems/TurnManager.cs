// ============================================================
// TurnManager.cs — 回合管理器：执行9阶段回合流程
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.Board;

namespace PhysicsFriends.Systems
{
    /// <summary>
    /// 回合结果数据
    /// </summary>
    [Serializable]
    public class TurnResult
    {
        public bool turnSkipped;           // 回合被跳过
        public bool timeRollback;          // 时间机器回滚
        public bool gameOver;              // 游戏结束
        public PlayerState winner;         // 胜利者
        public VictoryType victoryType;    // 胜利类型
        public int diceRoll;               // 骰子点数
        public int actualSteps;            // 实际步数
        public List<AchievementId> newAchievements = new List<AchievementId>();
    }

    /// <summary>
    /// 回合管理器：控制每个玩家回合的9个阶段
    /// 阶段0: 保存快照
    /// 阶段1: 跳过检查
    /// 阶段2: 创举掷骰检查
    /// 阶段3: 掷骰
    /// 阶段4: 修正链
    /// 阶段5: 沉重/轻盈计算
    /// 阶段6: 移动
    /// 阶段7: 落点效果
    /// 阶段8: 自由行动（主动卡）
    /// 阶段9: 回合结束（胜利/创举检查）
    /// </summary>
    public class TurnManager
    {
        // 系统引用
        private DiceSystem _dice;
        private DeckManager _deck;
        private BoardManager _board;
        private PassiveEffectManager _passive;
        private EventEffectProcessor _eventProcessor;
        private RewardEffectProcessor _rewardProcessor;
        private CardEffectProcessor _cardProcessor;
        private AchievementChecker _achievementChecker;
        private SynthesisSystem _synthesis;

        // 游戏状态
        private List<PlayerState> _allPlayers;
        private GameConfig _config;
        private Era _currentEra;

        // 回合状态
        private GameSnapshot _currentSnapshot;
        private int _turnCounter;
        private int _eraRoundCount; // 当前时代的轮次计数
        private TurnResult _currentResult; // 当前回合结果（供回调写入）
        private PlayerState _currentPlayer; // 当前回合玩家

        private GameRecorder _recorder; // 回合记录器

        public TurnManager(
            DiceSystem dice, DeckManager deck, BoardManager board,
            PassiveEffectManager passive, EventEffectProcessor eventProc,
            RewardEffectProcessor rewardProc, CardEffectProcessor cardProc,
            AchievementChecker achChecker, SynthesisSystem synthesis,
            List<PlayerState> players, GameConfig config, GameRecorder recorder)
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
            _currentEra = Era.NaturalPhilosophy;

            // 为每个玩家注册胜利条件回调：mol或创举分变化时自动检查
            foreach (var p in _allPlayers)
            {
                p.OnVictoryCheck = OnPlayerValueChanged;
            }
        }

        public void SetEra(Era era)
        {
            _currentEra = era;
            _achievementChecker.SetEra(era);
        }

        // =============================================================
        // 主回合执行
        // =============================================================

        /// <summary>执行一个完整回合</summary>
        public TurnResult ExecuteTurn(PlayerState player)
        {
            var result = new TurnResult();
            _currentResult = result;
            _currentPlayer = player;
            _turnCounter++;

            Debug.Log($"\n{'='}{new string('=', 50)}");
            Debug.Log($"[回合{_turnCounter}] {player.playerName} 的回合开始");

            // 开始记录本回合
            var rec = _recorder.BeginTurn(_turnCounter, _eraRoundCount, player.playerIndex,
                player.playerName, _currentEra,
                player.position, player.mol, player.handCards.Count, player.achievementPoints);
            rec.Log(TurnActionType.TurnStart, $"{player.playerName} 的回合开始", player.playerIndex);

            // ---- 阶段0: 保存快照 ----
            Phase0_SaveSnapshot();

            // ---- 阶段1: 跳过检查 ----
            if (Phase1_SkipCheck(player))
            {
                result.turnSkipped = true;
                rec.wasSkipped = true;
                rec.Log(TurnActionType.TurnSkipped, $"{player.playerName} 跳过本回合", player.playerIndex);
                player.recentMoveSteps.Add(0);
                if (player.recentMoveSteps.Count > 10)
                    player.recentMoveSteps.RemoveAt(0);
                _passive.TickBuffs(player);
                Debug.Log($"[回合{_turnCounter}] {player.playerName} 跳过本回合");
                rec.Log(TurnActionType.TurnEnd, "回合结束(跳过)");
                _recorder.EndTurn(player.position, player.mol, player.handCards.Count, player.achievementPoints);
                _currentResult = null;
                return result;
            }

            // ---- 阶段1.5: 被动效果（回合开始触发）----
            int molBefore = player.mol;
            _passive.OnTurnStart(player, _allPlayers);
            if (player.mol != molBefore)
                rec.Log(TurnActionType.PassiveMagnetic, $"磁感应强度方向效果：mol {molBefore}→{player.mol}", player.playerIndex, value: player.mol - molBefore);
            if (result.gameOver) { FinishRecording(player, result, rec); return result; }

            // ---- 阶段2: 创举掷骰检查 ----
            Phase2_AchievementRollCheck(player, result);
            if (result.gameOver) { FinishRecording(player, result, rec); return result; }

            // ---- 阶段3: 掷骰 ----
            int rawRoll = Phase3_RollDice(player);
            result.diceRoll = rawRoll;
            rec.rawDiceRoll = rawRoll;
            rec.Log(TurnActionType.DiceRoll, $"掷骰原始点数：{rawRoll}", player.playerIndex, value: rawRoll);

            int actualSteps;

            // 迈克尔逊-莫雷：步数固定为6，不受任何修正影响
            if (player.michelsonMorleyTurns > 0)
            {
                actualSteps = 6;
                result.actualSteps = 6;
                rec.modifiedDiceRoll = rawRoll;
                rec.actualSteps = 6;
                rec.Log(TurnActionType.MichelsonFixed, "迈克尔逊-莫雷：步数固定为6", player.playerIndex, value: 6);
                Debug.Log("[迈克尔逊-莫雷] 步数固定为6（不受任何修正影响）");
            }
            else
            {
                // ---- 阶段4: 修正链 ----
                int modifiedRoll = Phase4_ModificationChain(player, rawRoll);
                rec.modifiedDiceRoll = modifiedRoll;
                if (modifiedRoll != rawRoll)
                    rec.Log(TurnActionType.DiceFinalValue, $"修正后点数：{rawRoll}→{modifiedRoll}", player.playerIndex, value: modifiedRoll);

                // ---- 阶段5: 沉重/轻盈 ----
                actualSteps = Phase5_HeavyLightCalc(player, modifiedRoll);
                result.actualSteps = actualSteps;
                rec.actualSteps = actualSteps;
                if (actualSteps != modifiedRoll)
                    rec.Log(TurnActionType.StepCalculation, $"沉重/轻盈：{modifiedRoll}→{actualSteps}步", player.playerIndex, value: actualSteps);
            }

            // 记录步数
            player.recentMoveSteps.Add(actualSteps);
            if (player.recentMoveSteps.Count > 10)
                player.recentMoveSteps.RemoveAt(0);

            // ---- 阶段6: 移动 ----
            int startPos = player.position;
            bool passedStart = Phase6_Move(player, actualSteps);
            rec.Log(TurnActionType.MoveStep, $"移动：格{startPos}→格{player.position}（{actualSteps}步，{player.moveDirection}）",
                player.playerIndex, value: actualSteps);

            if (passedStart)
            {
                rec.Log(TurnActionType.PassedStart, "经过起点", player.playerIndex);
                molBefore = player.mol;
                int cardsBefore = player.handCards.Count;
                _passive.OnPassStart(player);
                if (player.mol != molBefore)
                    rec.Log(TurnActionType.MolChange, $"经过起点被动效果：mol {molBefore}→{player.mol}", player.playerIndex, value: player.mol - molBefore);
                if (player.handCards.Count != cardsBefore)
                    rec.Log(TurnActionType.CardGained, $"经过起点被动效果：手牌 {cardsBefore}→{player.handCards.Count}张", player.playerIndex, value: player.handCards.Count - cardsBefore);

                if (player.phaseState == PhaseState.Gas)
                {
                    player.phaseState = PhaseState.None;
                    rec.Log(TurnActionType.BuffExpired, "气态在经过起点后解除", player.playerIndex);
                    Debug.Log("[相变] 气态在经过起点后解除");
                }
            }
            if (result.gameOver) { FinishRecording(player, result, rec); return result; }

            // 检查路障/电磁铁停留
            if (player.stoppedByRoadblock)
            {
                rec.Log(TurnActionType.StoppedByRoadblock, $"被路障/电磁铁阻挡在格{player.position}", player.playerIndex);
                Debug.Log($"[移动] {player.playerName} 被阻挡，回合提前结束");
                _passive.TickBuffs(player);
                rec.Log(TurnActionType.TurnEnd, "回合结束(被阻挡)");
                _recorder.EndTurn(player.position, player.mol, player.handCards.Count, player.achievementPoints);
                _currentResult = null;
                return result;
            }

            // ---- 阶段7: 落点效果 ----
            var tile = _board.GetTile(player.position);
            rec.Log(TurnActionType.LandTerritory + 0, $"落点：格{player.position}（{tile?.tileType}）", player.playerIndex, extra: tile?.tileType.ToString() ?? "");
            molBefore = player.mol;
            bool rollback = Phase7_LandingEffect(player, result);
            if (player.mol != molBefore)
                rec.Log(TurnActionType.MolChange, $"落点效果：mol {molBefore}→{player.mol}", player.playerIndex, value: player.mol - molBefore);

            if (rollback)
            {
                result.timeRollback = true;
                rec.wasRollback = true;
                rec.Log(TurnActionType.TimeMachineRollback, "时间机器回滚，本回合无效");
                _recorder.EndTurn(player.position, player.mol, player.handCards.Count, player.achievementPoints);
                _currentResult = null;
                Debug.Log("[时间机器] 回滚到上一回合状态！");
                return result;
            }
            if (result.gameOver) { FinishRecording(player, result, rec); return result; }

            // ---- 阶段8: 自由行动 ----
            Phase8_FreeAction(player);
            if (result.gameOver) { FinishRecording(player, result, rec); return result; }

            // ---- 阶段9: 回合结束 ----
            Phase9_TurnEnd(player, result);

            // 记录创举达成
            foreach (var achId in result.newAchievements)
            {
                var achDef = AchievementDatabase.Get(achId);
                rec.Log(TurnActionType.AchievementCompleted, $"达成创举：{achDef?.nameZH}（+{achDef?.points}分）",
                    player.playerIndex, extra: achId.ToString());
            }

            // Buff倒计时
            _passive.TickBuffs(player);

            // 结束记录
            FinishRecording(player, result, rec);
            _currentResult = null;
            return result;
        }

        /// <summary>结束回合记录（抽取的公共方法）</summary>
        private void FinishRecording(PlayerState player, TurnResult result, TurnRecord rec)
        {
            if (result.gameOver)
            {
                rec.triggeredVictory = true;
                rec.Log(result.victoryType == VictoryType.Wealth ? TurnActionType.VictoryWealth : TurnActionType.VictoryAchievement,
                    $"{result.winner?.playerName} 达成{result.victoryType}胜利！");
            }
            rec.Log(TurnActionType.TurnEnd, "回合结束");
            _passive.TickBuffs(player);
            _recorder.EndTurn(player.position, player.mol, player.handCards.Count, player.achievementPoints);
            _currentResult = null;
        }

        // =============================================================
        // 各阶段实现
        // =============================================================

        /// <summary>阶段0: 保存游戏快照（时间机器用）</summary>
        private void Phase0_SaveSnapshot()
        {
            _currentSnapshot = GameSnapshot.Create(_allPlayers, _board, _currentEra);
            Debug.Log("[阶段0] 快照已保存");
        }

        /// <summary>阶段1: 检查是否需要跳过回合</summary>
        private bool Phase1_SkipCheck(PlayerState player)
        {
            return _passive.ShouldSkipTurn(player);
        }

        /// <summary>阶段2: 回合开始时的创举掷骰检查（原子论/小孔成像/标准模型）</summary>
        private void Phase2_AchievementRollCheck(PlayerState player, TurnResult result)
        {
            // 规则书：这些创举需要在"回合开始时"检查条件，然后进行独立的不受修正掷骰
            // 原子论(#3)：回合开始时至少拥有15mol → 掷骰=6则完成
            // 小孔成像(#5)：回合开始时至少两张光照强度 → 掷骰=1则完成
            // 标准模型(#12)：回合开始时至少拥有40mol → 掷骰=6则完成

            foreach (var achId in _achievementChecker.ActiveAchievements)
            {
                if (_achievementChecker.GlobalCompleted.Contains(achId)) continue;
                if (player.completedAchievements.Contains(achId)) continue;

                bool conditionMet = false;
                int targetRoll = -1;

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
                    int roll = _dice.RollUnmodified(); // 独立掷骰，不受任何修正影响
                    Debug.Log($"[阶段2] 创举{achId}掷骰检查：需要{targetRoll}，投出{roll}");
                    if (roll == targetRoll)
                    {
                        // 标记为通过掷骰检查（在Phase9统一发放奖励）
                        player.achievementDiceCheckPassed.Add(achId);
                        Debug.Log($"[阶段2] 创举{achId}掷骰通过！");
                    }
                }
            }
        }

        /// <summary>阶段3: 掷骰</summary>
        private int Phase3_RollDice(PlayerState player)
        {
            int roll = _dice.RollUnmodified();
            Debug.Log($"[阶段3] {player.playerName} 掷骰：{roll}");

            // 薛定谔技能：可以选择重掷
            if (player.character == Character.Schrodinger)
            {
                // TODO: 由UI询问是否重掷
                // 简化：如果点数<3则自动重掷
                if (roll < 3)
                {
                    int reroll = _dice.RollUnmodified();
                    Debug.Log($"[薛定谔] 重掷：{roll}→{reroll}");
                    roll = reroll;
                }
            }

            return roll;
        }

        /// <summary>阶段4: 骰子修正链</summary>
        private int Phase4_ModificationChain(PlayerState player, int rawRoll)
        {
            // 收集所有修正
            var modifications = new List<TurnDiceModification>();
            int currentPlayerIdx = _allPlayers.IndexOf(player);

            // 修正顺序：从下一位玩家开始，到掷骰者结束
            for (int offset = 1; offset <= _allPlayers.Count; offset++)
            {
                int idx = (currentPlayerIdx + offset) % _allPlayers.Count;
                var modPlayer = _allPlayers[idx];

                // 收集该玩家的力卡修正
                var forceCards = modPlayer.handCards.Where(c =>
                    c.cardId == PhysicsCardId.Force && !c.isUsed &&
                    !c.forceUsedThisRound).ToList();

                foreach (var fc in forceCards)
                {
                    bool hasPrincipia = modPlayer.hasPrincipia;
                    // TODO: 由UI获取修正方向
                    // 简化：非掷骰者默认-1，掷骰者默认+1
                    int mod = (idx == currentPlayerIdx) ? 1 : -1;
                    if (hasPrincipia)
                        mod = (idx == currentPlayerIdx) ? 2 : -2;

                    modifications.Add(new TurnDiceModification
                    {
                        sourcePlayer = modPlayer,
                        card = fc,
                        value = mod,
                        type = ModificationType.Force
                    });
                    fc.forceUsedThisRound = true; // 力每轮只能用一次
                }

                // 掷骰者自己的加速度修正
                if (idx == currentPlayerIdx)
                {
                    var accelCards = modPlayer.handCards.Where(c =>
                        c.cardId == PhysicsCardId.Acceleration && !c.isUsed).ToList();
                    foreach (var ac in accelCards)
                    {
                        modifications.Add(new TurnDiceModification
                        {
                            sourcePlayer = modPlayer,
                            card = ac,
                            value = 1, // TODO: 由UI选择+1/-1
                            type = ModificationType.Acceleration
                        });
                    }
                }
            }

            // 应用修正（检查压强无效化和弹性系数钳制）
            int result = rawRoll;
            bool hasPressureNullify = false;
            bool hasSpringClamp = false;

            // 先检查是否有压强或弹性系数
            foreach (var mod in modifications)
            {
                if (mod.type == ModificationType.Pressure)
                    hasPressureNullify = true;
                if (mod.type == ModificationType.SpringConstant)
                    hasSpringClamp = true;
            }

            // 检查所有玩家手中的压强和弹性系数
            foreach (var p in _allPlayers)
            {
                if (p.HasCard(PhysicsCardId.Pressure))
                {
                    // TODO: 由UI询问是否使用压强无效化
                    // 压强可以无效化任意一次修正
                }
                if (p.HasCard(PhysicsCardId.SpringConstant))
                {
                    // 弹性系数：钳制结果到[3,4]
                    hasSpringClamp = true;
                }
            }

            // 应用力和加速度修正
            foreach (var mod in modifications)
            {
                if (mod.type == ModificationType.Force ||
                    mod.type == ModificationType.Acceleration)
                {
                    result += mod.value;
                    Debug.Log($"[修正] {mod.sourcePlayer.playerName} 的{mod.type}：{(mod.value > 0 ? "+" : "")}{mod.value}");
                }
            }

            // 弹性系数钳制
            if (hasSpringClamp)
            {
                result = Mathf.Clamp(result, 3, 4);
                Debug.Log($"[修正] 弹性系数钳制：结果={result}");
            }

            // 最终结果不低于1
            result = Mathf.Max(1, result);
            Debug.Log($"[阶段4] 修正后点数：{rawRoll}→{result}");
            return result;
        }

        /// <summary>阶段5: 沉重/轻盈层计算</summary>
        private int Phase5_HeavyLightCalc(PlayerState player, int modifiedRoll)
        {
            return _passive.CalculateActualSteps(player, modifiedRoll);
        }

        /// <summary>阶段6: 移动，返回是否经过起点</summary>
        private bool Phase6_Move(PlayerState player, int steps)
        {
            bool passedStart = false;
            int totalTiles = _board.TotalTiles;
            int direction = player.moveDirection == MoveDirection.Clockwise ? 1 : -1;

            // 液态反转方向
            // （已经在相变处理中修改了moveDirection）

            Debug.Log($"[阶段6] {player.playerName} 从格子{player.position}移动{steps}步({player.moveDirection})");

            for (int step = 0; step < steps; step++)
            {
                int nextPos = (player.position + direction + totalTiles) % totalTiles;

                // 检查是否经过起点（不是从起点出发）
                if (nextPos == 0 && player.position != 0)
                {
                    passedStart = true;
                    Debug.Log($"[移动] {player.playerName} 经过起点！");
                }

                player.position = nextPos;

                // 检查中途路障/电磁铁（最后一步在阶段7处理）
                if (step < steps - 1)
                {
                    _passive.OnPassTile(player, nextPos, _board, _allPlayers);
                    if (player.stoppedByRoadblock)
                    {
                        Debug.Log($"[移动] 在第{step + 1}步被阻挡");
                        break;
                    }
                }
            }

            Debug.Log($"[移动] {player.playerName} 到达格子{player.position}");
            return passedStart;
        }

        /// <summary>阶段7: 落点效果</summary>
        private bool Phase7_LandingEffect(PlayerState player, TurnResult result)
        {
            // 气态：不触发地块效果
            if (player.phaseState == PhaseState.Gas)
            {
                Debug.Log("[阶段7] 气态：跳过落点效果");
                return false;
            }

            var tile = _board.GetTile(player.position);
            if (tile == null) return false;

            Debug.Log($"[阶段7] 落点类型：{tile.tileType}");

            switch (tile.tileType)
            {
                case TileType.Start:
                    // 起点格：无特殊效果（经过起点的效果在移动阶段已处理）
                    break;

                case TileType.Territory:
                    ProcessTerritoryTile(player, tile);
                    break;

                case TileType.Shop:
                    ProcessShopTile(player, tile);
                    break;

                case TileType.Reward:
                    ProcessRewardTile(player);
                    break;

                case TileType.Event:
                    return ProcessEventTile(player);

                case TileType.Supply:
                    ProcessSupplyTile(player, tile);
                    break;
            }

            return false;
        }

        /// <summary>处理领地格</summary>
        private void ProcessTerritoryTile(PlayerState player, BoardTile tile)
        {
            if (tile.ownerColor == player.color)
            {
                // 自己颜色的领地：可以执行不限次数的合成卡牌操作
                Debug.Log($"[领地] {player.playerName} 踩到自己的领地，可以合成卡牌");
                TryAutoSynthesize(player);
            }
            else
            {
                // 其他玩家颜色的领地：无事发生
                Debug.Log($"[领地] {player.playerName} 踩到他人领地，无事发生");
            }
        }

        /// <summary>处理商店格</summary>
        private void ProcessShopTile(PlayerState player, BoardTile tile)
        {
            if (tile.ownerColor == player.color)
            {
                // 自己颜色的商店：可以购买卡牌与不限次数的合成卡牌操作
                Debug.Log($"[商店] {player.playerName} 进入自己的商店，可以购买+合成");
                ProcessShopPurchase(player);
                TryAutoSynthesize(player);
            }
            else
            {
                // 其他玩家颜色的商店：可以购买卡牌，还可以与相应颜色的玩家交易
                Debug.Log($"[商店] {player.playerName} 进入他人商店，可以购买+交易");
                ProcessShopPurchase(player);
                // TODO: 由UI处理与相应颜色玩家的交易议价
            }
        }

        /// <summary>商店购买逻辑（按时代定价）</summary>
        private void ProcessShopPurchase(PlayerState player)
        {
            if (player.IsHandFull())
            {
                Debug.Log("[商店] 手牌已满，无法购买");
                return;
            }
            // 价格根据时代变化
            int randomPrice = _config.GetRandomCardPrice(_currentEra);
            int chosenPrice = _config.GetChosenCardPrice(_currentEra);

            // TODO: 由UI让玩家选择购买类型
            // 简化：如果买得起随机卡就自动买一张
            if (player.mol >= randomPrice)
            {
                var cardId = _deck.DrawRandomBasic(UnityEngine.Random.Range(1, 7));
                player.mol -= randomPrice;
                player.GiveCard(cardId);
                Debug.Log($"[商店] 购买了随机{CardDatabase.Get(cardId)?.nameZH}（花费{randomPrice}mol）");
            }
        }

        /// <summary>处理奖励格</summary>
        private void ProcessRewardTile(PlayerState player)
        {
            var rewardId = _deck.DrawReward();
            Debug.Log($"[奖励] 抽到奖励牌");
            _rewardProcessor.ProcessReward(rewardId, player, _allPlayers, _board);
        }

        /// <summary>处理补给格（仅内圈有，展开后被事件格取代）</summary>
        private void ProcessSupplyTile(PlayerState player, BoardTile tile)
        {
            if (tile.ownerColor == player.color)
            {
                // 自己颜色的补给：任选一张基本物理量牌+不限次数合成
                if (!player.IsHandFull())
                {
                    // TODO: 由UI让玩家选择基本物理量
                    // 简化：随机获取一张
                    var cardId = _deck.DrawRandomBasic(UnityEngine.Random.Range(1, 7));
                    player.GiveCard(cardId);
                    Debug.Log($"[补给] {player.playerName} 的补给格：任选获得{CardDatabase.Get(cardId)?.nameZH}");
                }
                TryAutoSynthesize(player);
            }
            else
            {
                // 其他玩家颜色的补给：随机获得一张基本物理量牌
                if (!player.IsHandFull())
                {
                    int roll = _dice.RollUnmodified();
                    var cardId = _deck.DrawRandomBasic(roll);
                    player.GiveCard(cardId);
                    Debug.Log($"[补给] {player.playerName} 在他人补给格：随机获得{CardDatabase.Get(cardId)?.nameZH}");
                }
            }
        }

        /// <summary>处理事件格，返回是否回滚</summary>
        private bool ProcessEventTile(PlayerState player)
        {
            var eventId = _deck.DrawEvent();
            bool rollback = _eventProcessor.ProcessEvent(eventId, player, _allPlayers, _currentSnapshot);

            // 事件触发后检查相关创举
            if (!rollback)
            {
                // 创举11：宇宙大爆炸（奇点事件中失去一切）
                if (player.singularityLostAll)
                {
                    _achievementChecker.CheckBigBang(player);
                    player.singularityLostAll = false; // 重置标记
                }
                // 创举14：量子力学（触发量子隧穿事件）
                if (player.quantumTunnelingUsed)
                {
                    _achievementChecker.CheckQuantumMechanics(player);
                    player.quantumTunnelingUsed = false; // 重置标记
                }
                // 创举15：超导现象（触发超导事件）
                if (player.superconductorTriggered)
                {
                    _achievementChecker.CheckSuperconductivity(player);
                    player.superconductorTriggered = false;
                }
            }

            return rollback;
        }

        /// <summary>阶段8: 自由行动</summary>
        private void Phase8_FreeAction(PlayerState player)
        {
            Debug.Log("[阶段8] 自由行动阶段");

            // 检查创新项目（消耗3张同名手牌）
            // TODO: 由UI触发，这里自动检查
            var cardGroups = player.handCards
                .Where(c => !c.isUsed)
                .GroupBy(c => c.cardId)
                .Where(g => g.Count() >= 3);
            foreach (var group in cardGroups)
            {
                // 简化：不自动执行，仅提示
                Debug.Log($"[创新项目] {player.playerName} 可以消耗3张{CardDatabase.Get(group.Key)?.nameZH}执行创新项目");
            }

            // 合成（在领地/商店/补给格已处理，这里是通用提示）
            TryAutoSynthesize(player);

            // 手牌上限检查：如果在允许合成的回合手牌>10，可以通过合成减少
            if (player.handCards.Count > PlayerState.MaxHandCards)
            {
                Debug.Log($"[手牌] {player.playerName} 手牌数量{player.handCards.Count}超过上限{PlayerState.MaxHandCards}，需要合成或弃牌");
                // TODO: 由UI让玩家选择合成或弃牌
            }
        }

        /// <summary>尝试自动合成（简化版）</summary>
        private void TryAutoSynthesize(PlayerState player)
        {
            // 检查手牌是否可以合成任何目标
            var handCardIds = player.handCards
                .Where(c => !c.isUsed)
                .Select(c => c.cardId)
                .ToList();

            if (handCardIds.Count < 2) return;

            // 获取所有可能的合成结果
            var possibleResults = _synthesis.FindPossibleSyntheses(handCardIds);
            if (possibleResults.Count > 0)
            {
                Debug.Log($"[合成] 发现{possibleResults.Count}种可能的合成");
                // TODO: 由UI让玩家选择合成
            }
        }

        /// <summary>阶段9: 回合结束</summary>
        private void Phase9_TurnEnd(PlayerState player, TurnResult result)
        {
            Debug.Log("[阶段9] 回合结束检查");

            // 检查创举
            var newAchievements = _achievementChecker.CheckAll(
                player, _allPlayers, _dice, result.diceRoll);
            result.newAchievements = newAchievements;

            // 发放创举奖励（mol/创举分变化会通过属性setter自动触发胜利检查）
            foreach (var achId in newAchievements)
            {
                _achievementChecker.GrantAchievementReward(achId, player);
            }
            if (result.gameOver) return;

            // 检查纪元推进
            CheckEraAdvance();

            // 清除本回合的创举掷骰结果
            player.achievementDiceCheckPassed.Clear();

            // 力卡每轮使用标记重置（在全局轮结束时重置，这里标记）
            // 注意：力每轮只能用一次，轮 = 所有玩家各走一回合
        }

        /// <summary>
        /// 回调：玩家mol或创举分变化时自动触发
        /// </summary>
        private void OnPlayerValueChanged(PlayerState player)
        {
            if (_currentResult == null || _currentResult.gameOver) return;
            CheckVictory(player, _currentResult);
        }

        /// <summary>
        /// 检查胜利条件
        /// </summary>
        public void CheckVictory(PlayerState player, TurnResult result)
        {
            if (result.gameOver) return; // 已经有人赢了

            // 财富胜利
            if (player.mol >= _config.wealthVictoryMol)
            {
                result.gameOver = true;
                result.winner = player;
                result.victoryType = VictoryType.Wealth;
                Debug.Log($"[胜利] {player.playerName} 达成财富胜利！（{player.mol}mol ≥ {_config.wealthVictoryMol}）");
                return;
            }

            // 创举胜利
            if (player.achievementPoints >= _config.achievementVictoryPts)
            {
                result.gameOver = true;
                result.winner = player;
                result.victoryType = VictoryType.Achievement;
                Debug.Log($"[胜利] {player.playerName} 达成创举胜利！（{player.achievementPoints}分 ≥ {_config.achievementVictoryPts}）");
            }
        }

        /// <summary>检查是否需要推进纪元</summary>
        private void CheckEraAdvance()
        {
            // 检查当前时代的活跃创举是否全部完成
            bool allActiveCompleted = true;
            foreach (var achId in _achievementChecker.ActiveAchievements)
            {
                if (!_achievementChecker.GlobalCompleted.Contains(achId))
                {
                    allActiveCompleted = false;
                    break;
                }
            }

            // 时代结束条件：1.所有活跃创举完成 或 2.达到轮次上限(15轮)
            bool eraEnds = allActiveCompleted || _eraRoundCount >= _config.maxRoundsPerEra;

            if (!eraEnds) return;

            if (_currentEra == Era.NaturalPhilosophy)
            {
                SetEra(Era.ClassicalPhysics);
                // 展开棋盘：24→72格，玩家位置不变
                _board.ExpandBoard(_allPlayers);
                _eraRoundCount = 0;
                // 抽取新时代的创举
                DrawEraAchievements(Era.ClassicalPhysics);
                Debug.Log("[纪元] 进入经典物理学时期！棋盘已扩展至72格");
            }
            else if (_currentEra == Era.ClassicalPhysics)
            {
                SetEra(Era.ModernPhysics);
                _eraRoundCount = 0;
                DrawEraAchievements(Era.ModernPhysics);
                Debug.Log("[纪元] 进入现代物理学时期！");
            }
            // 现代物理学时期没有轮次上限（游戏直到有人胜利）
        }

        /// <summary>从该时代的5张创举中随机抽取（标准2张，慢速3张）</summary>
        private void DrawEraAchievements(Era era)
        {
            var allEraAch = AchievementDatabase.GetByEra(era);
            int drawCount = _config.achievementsPerEra;

            // 随机抽取
            var shuffled = allEraAch.OrderBy(x => UnityEngine.Random.value).ToList();
            var drawn = shuffled.Take(Math.Min(drawCount, shuffled.Count)).ToList();
            _achievementChecker.SetActiveAchievements(drawn);

            string names = string.Join(", ", drawn.Select(a => AchievementDatabase.Get(a)?.nameZH));
            Debug.Log($"[创举] {era}时代创举：{names}");
        }

        /// <summary>在每个完整轮结束时调用</summary>
        public void OnRoundEnd()
        {
            // 重置所有玩家的力卡使用标记
            foreach (var p in _allPlayers)
            {
                p.forceUsedThisRound = false;
                foreach (var card in p.handCards)
                {
                    card.forceUsedThisRound = false;
                }
            }
            _eraRoundCount++;
            Debug.Log($"[轮结束] 力卡标记已重置，当前时代第{_eraRoundCount}轮");
        }

        /// <summary>初始化第一个时代的创举</summary>
        public void InitializeFirstEra()
        {
            _eraRoundCount = 0;
            DrawEraAchievements(Era.NaturalPhilosophy);
        }

        /// <summary>
        /// 创新项目：消耗三张同名手牌，抽取随机牌
        /// 在每个玩家回合的任意时刻可以执行
        /// </summary>
        public bool TryInnovationProject(PlayerState player, PhysicsCardId cardId)
        {
            int count = player.CountCards(cardId);
            if (count < 3) return false;

            // 消耗3张同名手牌
            for (int i = 0; i < 3; i++)
            {
                var card = player.handCards.FirstOrDefault(c => c.cardId == cardId && !c.isUsed);
                if (card != null) player.RemoveCard(card);
            }

            // 创新项目：从基本物理量中抽3张、非基本力学量/电磁学量/热学量各抽1张
            // 编号1~6，掷骰决定获取
            var pool = new List<PhysicsCardId>();

            // 抽3张基本物理量（随机）
            for (int i = 0; i < 3; i++)
            {
                int roll = _dice.RollUnmodified();
                pool.Add(_deck.DrawRandomBasic(roll));
            }
            // 各抽1张非基本量
            pool.Add(GetRandomFromBranch(PhysicsBranch.Mechanics));
            pool.Add(GetRandomFromBranch(PhysicsBranch.Electromagnetics));
            pool.Add(GetRandomFromBranch(PhysicsBranch.Thermodynamics));

            // 掷骰决定获取哪张
            int result = _dice.RollUnmodified();
            int index = Math.Min(result - 1, pool.Count - 1);
            if (index >= 0 && index < pool.Count)
            {
                player.GiveCard(pool[index]);
                Debug.Log($"[创新项目] {player.playerName} 消耗3张{CardDatabase.Get(cardId)?.nameZH}，" +
                          $"获得{CardDatabase.Get(pool[index])?.nameZH}");
            }
            return true;
        }

        /// <summary>从指定学科分支随机获取一张非基本物理量</summary>
        private PhysicsCardId GetRandomFromBranch(PhysicsBranch branch)
        {
            var candidates = new List<PhysicsCardId>();
            foreach (var kvp in CardDatabase.GetAll())
            {
                if (kvp.Value.branch == branch && !CardDatabase.IsBasicQuantity(kvp.Key))
                    candidates.Add(kvp.Key);
            }
            if (candidates.Count == 0) return PhysicsCardId.Time;
            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }
    }

    /// <summary>骰子修正数据</summary>
    public class TurnDiceModification
    {
        public PlayerState sourcePlayer;
        public CardInstance card;
        public int value;
        public ModificationType type;
    }

    public enum ModificationType
    {
        Force,           // 力：+1/-1（原理版+2/-2）
        Acceleration,    // 加速度：+1/-1（仅掷骰者）
        Pressure,        // 压强：无效化一次修正
        SpringConstant   // 弹性系数：钳制到[3,4]
    }
}
