// ============================================================
// GameManager.cs — 游戏管理器：主游戏循环编排（2D项目）
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
    /// 游戏管理器：Unity MonoBehaviour
    /// 负责初始化所有子系统、管理游戏主循环、处理UI回调
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // === Inspector设置 ===
        [Header("游戏配置")]
        [SerializeField] private GameMode gameMode = GameMode.Standard;
        [SerializeField] private int playerCount = 2;

        [Header("2D棋盘引用")]
        [SerializeField] private Transform boardParent;     // 棋盘父物体
        [SerializeField] private GameObject tilePrefab;     // 格子预制体
        [SerializeField] private GameObject playerPrefab;   // 玩家棋子预制体

        // === 子系统 ===
        private GameConfig _config;
        private DiceSystem _dice;
        private DeckManager _deck;
        private BoardManager _board;
        private SynthesisSystem _synthesis;
        private PassiveEffectManager _passive;
        private EventEffectProcessor _eventProcessor;
        private RewardEffectProcessor _rewardProcessor;
        private CardEffectProcessor _cardProcessor;
        private AchievementChecker _achievementChecker;
        private TurnManager _turnManager;
        private TurnManagerAsync _turnManagerAsync;

        // === 新增子系统 ===
        private BuildingManager _buildingManager;
        private CharacterAbilitySystem _characterSystem;
        private MagneticFluxManager _magneticFlux;
        private VictoryChecker _victoryChecker;
        private TradeManager _tradeManager;

        // === UI系统 ===
        private IUIProvider _ui;

        // === 游戏状态 ===
        private List<PlayerState> _players = new List<PlayerState>();
        private int _currentPlayerIndex;
        private int _roundNumber;
        private Era _currentEra;
        private GameState _gameState;
        private bool _isProcessingTurn;

        // === 回合记录 ===
        private GameRecorder _recorder;
        public GameRecorder Recorder => _recorder;

        // === 2D棋子对象 ===
        private List<GameObject> _playerTokens = new List<GameObject>();

        // === 事件 ===
        public event Action<PlayerState> OnTurnStart;
        public event Action<PlayerState, TurnResult> OnTurnEnd;
        public event Action<PlayerState, VictoryType> OnGameOver;
        public event Action<Era> OnEraChange;
        public event Action<PlayerState, AchievementId> OnAchievementUnlocked;

        // === 单例 ===
        public static GameManager Instance { get; private set; }

        // === 属性 ===
        public GameState CurrentState => _gameState;
        public Era CurrentEra => _currentEra;
        public List<PlayerState> Players => _players;
        public PlayerState CurrentPlayer => _players[_currentPlayerIndex];
        public GameConfig Config => _config;
        public BoardManager Board => _board;
        public DeckManager Deck => _deck;
        public SynthesisSystem Synthesis => _synthesis;

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
            InitializeGame();
        }

        // =============================================================
        // 游戏初始化
        // =============================================================

        /// <summary>初始化游戏所有系统</summary>
        public void InitializeGame()
        {
            Debug.Log("=== 《物理之友》游戏初始化 ===");
            _gameState = GameState.Initializing;

            // 0. 初始化静态数据库（必须最先执行）
            CardDatabase.Initialize();
            EventCardDatabase.Initialize();
            RewardCardDatabase.Initialize();
            AchievementDatabase.Initialize();
            Debug.Log("静态数据库初始化完成");

            // 1. 加载配置
            _config = GameConfig.GetConfig(gameMode);
            Debug.Log($"游戏模式：{gameMode}（财富胜利={_config.wealthVictoryMol}mol，创举胜利={_config.achievementVictoryPts}分）");

            // 2. 初始化子系统
            _dice = new DiceSystem();
            _deck = new DeckManager();
            _board = new BoardManager();
            _synthesis = new SynthesisSystem();
            _passive = new PassiveEffectManager();
            _eventProcessor = new EventEffectProcessor(_dice, _deck);
            _rewardProcessor = new RewardEffectProcessor(_dice, _deck);
            _cardProcessor = new CardEffectProcessor();
            _achievementChecker = new AchievementChecker(gameMode);

            // 3. 设置时间机器回滚
            _eventProcessor.SetRollbackAction(OnTimeMachineRollback);

            // 4. 初始化棋盘（24格起始）
            _board.InitializeBoard(24);
            Debug.Log("棋盘初始化完成：24格");

            // 5. 初始化牌堆
            _deck.Initialize(playerCount);
            Debug.Log("牌堆初始化完成");

            // 6. 创建玩家
            CreatePlayers();

            // 7. 初始化回合管理器
            _recorder = new GameRecorder();

            // 获取UI提供者（UIManager单例）
            _ui = UIManager.Instance;
            if (_ui == null)
            {
                Debug.LogWarning("[GameManager] 未找到UIManager，将使用默认UI行为");
            }

            // 补充注入CardEffectProcessor依赖
            _cardProcessor.Init(_ui, _board, _deck);

            // 同步版回合管理器（后备）
            _turnManager = new TurnManager(
                _dice, _deck, _board, _passive,
                _eventProcessor, _rewardProcessor, _cardProcessor,
                _achievementChecker, _synthesis,
                _players, _config, _recorder);

            // 异步版回合管理器（主力，支持UI交互）
            if (_ui != null)
            {
                _turnManagerAsync = new TurnManagerAsync(
                    _dice, _deck, _board, _passive,
                    _eventProcessor, _rewardProcessor, _cardProcessor,
                    _achievementChecker, _synthesis,
                    _players, _config, _recorder, _ui);

                // 初始化新增子系统
                _victoryChecker = new VictoryChecker(_config, gameMode);
                _characterSystem = new CharacterAbilitySystem(_ui);
                _magneticFlux = new MagneticFluxManager(_ui, playerCount);
                _buildingManager = new BuildingManager(_board, _ui);
                _tradeManager = new TradeManager(_ui);

                // 注入到回合管理器
                _turnManagerAsync.InjectNewSystems(
                    _buildingManager, _characterSystem, _magneticFlux, _victoryChecker);
            }

            // 8. 设置初始纪元
            _currentEra = Era.NaturalPhilosophy;
            _currentPlayerIndex = 0;
            _roundNumber = 0;

            // 9. 抽取第一时代的创举牌
            if (_turnManagerAsync != null)
                _turnManagerAsync.InitializeFirstEra();
            else
                _turnManager.InitializeFirstEra();

            // 10. 初始化2D棋盘视觉
            Setup2DBoard();

            _gameState = GameState.Playing;
            Debug.Log("=== 游戏初始化完成，开始游戏 ===");

            // 开始第一个回合
            StartNextTurn();
        }

        /// <summary>创建玩家</summary>
        private void CreatePlayers()
        {
            // 角色分配
            Character[] availableChars = {
                Character.Newton, Character.Maxwell,
                Character.Einstein, Character.Schrodinger
            };

            // 玩家颜色
            PlayerColor[] playerColors = {
                PlayerColor.Red, PlayerColor.Blue, PlayerColor.Green, PlayerColor.Yellow
            };
            string[] colorNames = { "红", "蓝", "绿", "黄" };

            for (int i = 0; i < playerCount; i++)
            {
                var player = new PlayerState(i, playerColors[i], availableChars[i],
                    $"玩家{i + 1}（{colorNames[i]}）");
                player.mol = _config.initialMol;  // 规则书：快速5/标准10/慢速15
                player.position = 0;
                player.moveDirection = MoveDirection.Clockwise;

                // 初始手牌：按CardDefinition.startCount给每种基本物理量牌
                // 规则书附录1：每种基本物理量开局数量=1
                foreach (var kvp in CardDatabase.GetAll())
                {
                    var def = kvp.Value;
                    for (int j = 0; j < def.startCount; j++)
                    {
                        player.GiveCard(def.id);
                    }
                }

                _players.Add(player);
                Debug.Log($"创建玩家：{player.playerName}（角色={player.character}，初始{_config.initialMol}mol）");
            }
        }

        // =============================================================
        // 2D棋盘视觉设置
        // =============================================================

        /// <summary>设置2D棋盘的视觉表现（使用棋盘数据中的坐标）</summary>
        private void Setup2DBoard()
        {
            if (boardParent == null)
            {
                Debug.LogWarning("[2D] 未设置boardParent，跳过视觉初始化");
                return;
            }

            // 使用棋盘数据中的坐标生成格子
            int tileCount = _board.TotalTiles;

            for (int i = 0; i < tileCount; i++)
            {
                var tile = _board.GetTile(i);
                if (tile == null) continue;

                // 使用棋盘数据表中的(x,y)坐标，乘以缩放系数适配2D场景
                float scale = 0.8f; // 缩放系数，根据场景大小调整
                Vector2 pos = new Vector2(tile.x * scale, tile.y * scale);

                if (tilePrefab != null)
                {
                    var tileObj = Instantiate(tilePrefab, boardParent);
                    tileObj.transform.localPosition = pos;
                    tileObj.name = $"Tile_{i}_{tile.tileType}";

                    var sr = tileObj.GetComponent<SpriteRenderer>();
                    if (sr != null)
                    {
                        // 领地/商店/补给格使用所属玩家颜色的浅色版本
                        if (tile.colorId > 0 &&
                            (tile.tileType == TileType.Territory ||
                             tile.tileType == TileType.Shop ||
                             tile.tileType == TileType.Supply))
                        {
                            Color baseColor = PlayerColorToColor(tile.ownerColor);
                            sr.color = Color.Lerp(baseColor, Color.white, 0.6f); // 浅色
                        }
                        else
                        {
                            sr.color = GetTileColor(tile.tileType);
                        }
                    }
                }
            }

            // 生成玩家棋子
            for (int i = 0; i < _players.Count; i++)
            {
                if (playerPrefab != null)
                {
                    var token = Instantiate(playerPrefab, boardParent);
                    token.name = $"PlayerToken_{i}";
                    var sr = token.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.color = PlayerColorToColor(_players[i].color);
                    if (sr != null) sr.sortingOrder = 10 + i;

                    _playerTokens.Add(token);
                    UpdatePlayerTokenPosition(i);
                }
            }

            Debug.Log("[2D] 棋盘视觉初始化完成");
        }

        /// <summary>获取格子颜色</summary>
        private Color GetTileColor(TileType type)
        {
            switch (type)
            {
                case TileType.Start:     return new Color(1f, 0.9f, 0.3f);    // 金色
                case TileType.Territory: return new Color(0.7f, 0.85f, 0.7f); // 浅绿
                case TileType.Shop:      return new Color(0.6f, 0.75f, 1f);   // 浅蓝
                case TileType.Reward:    return new Color(1f, 0.8f, 0.5f);    // 橙色
                case TileType.Event:     return new Color(0.9f, 0.5f, 0.5f);  // 浅红
                case TileType.Supply:    return new Color(0.5f, 0.9f, 0.7f);  // 浅青绿
                default:                 return Color.white;
            }
        }

        /// <summary>将PlayerColor枚举映射为Unity Color</summary>
        private Color PlayerColorToColor(PlayerColor pc)
        {
            switch (pc)
            {
                case PlayerColor.Red:    return Color.red;
                case PlayerColor.Blue:   return Color.blue;
                case PlayerColor.Green:  return Color.green;
                case PlayerColor.Yellow: return Color.yellow;
                default:                 return Color.white;
            }
        }

        /// <summary>更新玩家棋子位置（2D，使用棋盘数据坐标）</summary>
        private void UpdatePlayerTokenPosition(int playerIdx)
        {
            if (playerIdx >= _playerTokens.Count) return;

            var player = _players[playerIdx];
            var tile = _board.GetTile(player.position);
            if (tile == null) return;

            float scale = 0.8f;
            // 多个玩家在同一格时偏移
            float offset = playerIdx * 0.2f;
            Vector2 pos = new Vector2(
                tile.x * scale + offset,
                tile.y * scale + offset
            );

            _playerTokens[playerIdx].transform.localPosition = pos;
        }

        /// <summary>更新所有玩家棋子位置</summary>
        private void UpdateAllTokenPositions()
        {
            for (int i = 0; i < _players.Count; i++)
                UpdatePlayerTokenPosition(i);
        }

        // =============================================================
        // 游戏主循环
        // =============================================================

        /// <summary>开始下一个回合</summary>
        public void StartNextTurn()
        {
            if (_gameState != GameState.Playing) return;
            if (_isProcessingTurn) return;

            StartCoroutine(ProcessTurnCoroutine());
        }

        /// <summary>回合处理协程</summary>
        private IEnumerator ProcessTurnCoroutine()
        {
            _isProcessingTurn = true;
            var player = _players[_currentPlayerIndex];

            // 触发回合开始事件
            OnTurnStart?.Invoke(player);
            _gameState = GameState.PlayerTurn;

            // 等待一帧让UI更新
            yield return null;

            TurnResult result = null;

            if (_turnManagerAsync != null)
            {
                // === 异步版：使用TurnManagerAsync，支持UI交互 ===
                bool turnDone = false;
                yield return _turnManagerAsync.ExecuteTurnAsync(player, (r) =>
                {
                    result = r;
                    turnDone = true;
                });
                // 等待回合完成
                while (!turnDone)
                    yield return null;
            }
            else
            {
                // === 同步版后备：原TurnManager ===
                result = _turnManager.ExecuteTurn(player);
            }

            // 更新棋子位置（2D）
            UpdateAllTokenPositions();

            // 等待动画（可配置）
            yield return new WaitForSeconds(0.5f);

            // 处理回合结果
            if (result.timeRollback)
            {
                Debug.Log("[游戏] 时间机器回滚，当前回合无效");
                _isProcessingTurn = false;
                AdvanceToNextPlayer();
                StartNextTurn();
                yield break;
            }

            // 通知创举达成
            foreach (var achId in result.newAchievements)
            {
                OnAchievementUnlocked?.Invoke(player, achId);
                yield return new WaitForSeconds(0.3f); // 创举展示间隔
            }

            // 触发回合结束事件
            OnTurnEnd?.Invoke(player, result);

            // 检查游戏结束
            if (result.gameOver)
            {
                _gameState = GameState.GameOver;
                OnGameOver?.Invoke(result.winner, result.victoryType);

                // 通过UI显示游戏结束面板
                if (_ui != null)
                {
                    _ui.SendNotification(new GameNotification(NotificationType.Victory,
                        $"{result.winner.playerName} 获胜！（{result.victoryType}）",
                        result.winner, 5f));
                }

                Debug.Log($"\n{'★'}{new string('★', 20)}");
                Debug.Log($"游戏结束！{result.winner.playerName} 获胜！（{result.victoryType}）");
                _isProcessingTurn = false;
                yield break;
            }

            // 检查额外回合（电位连续行动）
            if (_passive.HasExtraTurn(player))
            {
                Debug.Log("[电位] 额外回合！");
                _isProcessingTurn = false;
                StartNextTurn(); // 不推进玩家，再走一次
                yield break;
            }

            // 推进到下一个玩家
            AdvanceToNextPlayer();
            _isProcessingTurn = false;

            // 自动开始下一回合（可改为等待点击）
            yield return new WaitForSeconds(0.2f);
            StartNextTurn();
        }

        /// <summary>推进到下一个玩家</summary>
        private void AdvanceToNextPlayer()
        {
            _currentPlayerIndex++;
            if (_currentPlayerIndex >= _players.Count)
            {
                _currentPlayerIndex = 0;
                _roundNumber++;
                if (_turnManagerAsync != null)
                    _turnManagerAsync.OnRoundEnd();
                else
                    _turnManager.OnRoundEnd();
                Debug.Log($"\n[轮{_roundNumber}结束] 新一轮开始");
            }
        }

        // =============================================================
        // 时间机器回滚
        // =============================================================

        private void OnTimeMachineRollback(GameSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[时间机器] 快照为空，无法回滚");
                return;
            }

            Debug.Log("[时间机器] 正在回滚到上一回合状态...");

            // 恢复玩家状态
            for (int i = 0; i < _players.Count && i < snapshot.playerSnapshots.Count; i++)
            {
                snapshot.playerSnapshots[i].RestoreTo(_players[i]);
            }

            // 恢复棋盘状态
            snapshot.boardSnapshot?.RestoreTo(_board);

            // 恢复纪元
            if (_currentEra != snapshot.era)
            {
                _currentEra = snapshot.era;
                if (_turnManagerAsync != null)
                    _turnManagerAsync.SetEra(_currentEra);
                else
                    _turnManager.SetEra(_currentEra);
                OnEraChange?.Invoke(_currentEra);
            }

            // 更新视觉
            UpdateAllTokenPositions();
        }

        // =============================================================
        // UI交互API（供UI脚本调用）
        // =============================================================

        /// <summary>玩家选择合成</summary>
        public bool PlayerSynthesize(int playerIdx, List<int> cardHandIndices,
            PhysicsCardId targetId)
        {
            var player = _players[playerIdx];
            var cardsToUse = cardHandIndices
                .Where(i => i >= 0 && i < player.handCards.Count)
                .Select(i => player.handCards[i])
                .ToList();

            var cardIds = cardsToUse.Select(c => c.cardId).ToList();
            var result = _synthesis.TrySynthesize(cardIds, targetId);

            if (result.success)
            {
                // 移除用于合成的卡
                foreach (var card in cardsToUse)
                    player.RemoveCard(card);

                // 给予合成结果
                player.GiveCard(targetId);
                Debug.Log($"[合成] {player.playerName} 合成了{CardDatabase.Get(targetId)?.nameZH}");
                return true;
            }
            return false;
        }

        /// <summary>玩家使用主动卡（委托给CardEffectProcessor的协程）</summary>
        public void PlayerUseActiveCard(int playerIdx, int cardHandIndex,
            int targetPlayerIdx = -1)
        {
            var player = _players[playerIdx];
            if (cardHandIndex < 0 || cardHandIndex >= player.handCards.Count) return;

            var card = player.handCards[cardHandIndex];
            StartCoroutine(_cardProcessor.ExecuteActiveEffect(player, card, _players));
        }

        /// <summary>玩家选择相变形态</summary>
        public void PlayerChoosePhase(int playerIdx, PhaseState phase)
        {
            _eventProcessor.ApplyPhaseTransition(_players[playerIdx], phase);
        }

        /// <summary>玩家选择量子隧穿目标位置</summary>
        public void PlayerChooseQuantumTunneling(int playerIdx, int targetTile)
        {
            var player = _players[playerIdx];
            player.position = targetTile;
            player.quantumTunnelingUsed = true;
            _achievementChecker.CheckQuantumMechanics(player);
            UpdatePlayerTokenPosition(playerIdx);
        }

        /// <summary>获取可合成列表</summary>
        public List<SynthesisResult> GetPossibleSyntheses(int playerIdx)
        {
            var player = _players[playerIdx];
            var cardIds = player.handCards
                .Where(c => !c.isUsed)
                .Select(c => c.cardId)
                .ToList();
            return _synthesis.FindPossibleSyntheses(cardIds);
        }

        // =============================================================
        // 2D棋盘扩展
        // =============================================================

        /// <summary>棋盘扩展时重建2D视觉</summary>
        public void RebuildBoardVisual()
        {
            // 清除旧格子
            if (boardParent != null)
            {
                foreach (Transform child in boardParent)
                {
                    if (child.name.StartsWith("Tile_"))
                        Destroy(child.gameObject);
                }
            }

            // 重新生成（使用扩展后的格数）
            Setup2DBoard();
            Debug.Log("[2D] 棋盘视觉已重建");
        }

        // =============================================================
        // 调试与保存
        // =============================================================

        /// <summary>打印所有玩家状态</summary>
        [ContextMenu("打印玩家状态")]
        public void DebugPrintPlayerStates()
        {
            foreach (var p in _players)
            {
                Debug.Log($"[{p.playerName}] 位置={p.position} mol={p.mol} " +
                          $"手牌={p.handCards.Count} 创举={p.achievementPoints}分 " +
                          $"角色={p.character} 方向={p.moveDirection}");
            }
        }

        /// <summary>打印棋盘状态</summary>
        [ContextMenu("打印棋盘状态")]
        public void DebugPrintBoardState()
        {
            for (int i = 0; i < _board.TotalTiles; i++)
            {
                var tile = _board.GetTile(i);
                if (tile != null && tile.ownerIndex >= 0)
                {
                    Debug.Log($"格子{i}: {tile.tileType} 拥有者=玩家{tile.ownerIndex}");
                }
            }
        }

        /// <summary>打印最近N个回合的完整记录</summary>
        [ContextMenu("打印最近回合记录")]
        public void DebugPrintRecentTurns()
        {
            var recent = _recorder.GetRecentTurns(10);
            foreach (var turn in recent)
            {
                Debug.Log(turn.ToSummary());
            }
        }

        /// <summary>打印全部回合记录</summary>
        [ContextMenu("导出全部回合日志")]
        public void DebugExportFullLog()
        {
            Debug.Log(_recorder.ExportFullLog());
        }

        /// <summary>打印指定玩家的mol变化曲线</summary>
        public void DebugPrintMolHistory(int playerIdx)
        {
            var history = _recorder.GetMolHistory(playerIdx);
            var line = string.Join(" → ", history.Select(h => $"T{h.turn}:{h.mol}"));
            Debug.Log($"[{_players[playerIdx].playerName}] mol曲线: {line}");
        }
    }
}
