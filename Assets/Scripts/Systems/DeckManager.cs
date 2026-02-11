// ============================================================
// DeckManager.cs — 牌堆管理器：事件牌堆、奖励牌堆、物理量牌堆
// 负责洗牌、抽牌、弃牌、回收等操作
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using PhysicsFriends.Core;
using PhysicsFriends.Data;

namespace PhysicsFriends.Systems
{
    /// <summary>
    /// 通用牌堆管理器，支持洗牌、抽牌、弃牌
    /// 使用泛型T表示牌的类型
    /// </summary>
    [Serializable]
    public class Deck<T>
    {
        public List<T> drawPile;     // 抽牌堆（可以从中抽牌）
        public List<T> discardPile;  // 弃牌堆（使用后的牌放这里）
        private System.Random _random; // 随机数生成器

        public Deck()
        {
            drawPile = new List<T>();     // 初始化抽牌堆
            discardPile = new List<T>();  // 初始化弃牌堆
            _random = new System.Random(); // 初始化随机数
        }

        /// <summary>添加一张牌到抽牌堆（初始化时使用）</summary>
        public void AddToDraw(T card)
        {
            drawPile.Add(card);
        }

        /// <summary>洗牌：随机打乱抽牌堆的顺序</summary>
        public void Shuffle()
        {
            for (int i = drawPile.Count - 1; i > 0; i--) // Fisher-Yates洗牌算法
            {
                int j = _random.Next(0, i + 1);           // 随机选一个位置
                var temp = drawPile[i];                    // 交换
                drawPile[i] = drawPile[j];
                drawPile[j] = temp;
            }
        }

        /// <summary>从抽牌堆顶部抽一张牌</summary>
        public T Draw()
        {
            if (drawPile.Count == 0)       // 抽牌堆为空
            {
                RecycleDiscard();          // 回收弃牌堆
                if (drawPile.Count == 0)   // 弃牌堆也为空
                    return default(T);     // 返回默认值
            }
            var card = drawPile[0];        // 取出顶部的牌
            drawPile.RemoveAt(0);          // 从抽牌堆移除
            return card;
        }

        /// <summary>将一张牌放入弃牌堆</summary>
        public void Discard(T card)
        {
            discardPile.Add(card);
        }

        /// <summary>将一张牌洗回抽牌堆</summary>
        public void ReturnToDraw(T card)
        {
            drawPile.Add(card);           // 加入抽牌堆
            Shuffle();                    // 重新洗牌
        }

        /// <summary>回收弃牌堆：将弃牌堆的牌洗入抽牌堆</summary>
        public void RecycleDiscard()
        {
            drawPile.AddRange(discardPile); // 弃牌堆全部加入抽牌堆
            discardPile.Clear();            // 清空弃牌堆
            Shuffle();                      // 洗牌
        }

        /// <summary>从弃牌堆随机抽一张（古老论文使用）</summary>
        public T DrawFromDiscard()
        {
            if (discardPile.Count == 0) return default(T); // 弃牌堆为空
            int idx = _random.Next(0, discardPile.Count); // 随机选一张
            var card = discardPile[idx];
            discardPile.RemoveAt(idx);      // 从弃牌堆移除
            return card;
        }

        /// <summary>剩余可抽牌数量</summary>
        public int Count => drawPile.Count;
    }

    /// <summary>
    /// 总牌堆管理器：管理所有类型的牌堆
    /// </summary>
    public class DeckManager
    {
        public Deck<EventCardId> eventDeck;     // 事件牌堆
        public Deck<RewardCardId> rewardDeck;   // 奖励牌堆
        public Deck<PhysicsCardId> physicsDeck; // 物理量牌堆（购买用）

        private System.Random _random;
        private int _playerCount = 4; // 实际玩家数量，用于计算牌堆初始数量

        public DeckManager()
        {
            _random = new System.Random();
        }

        /// <summary>初始化所有牌堆</summary>
        public void Initialize(int playerCount = 4)
        {
            _playerCount = playerCount;    // 保存玩家数量
            InitializeEventDeck();     // 初始化事件牌堆
            InitializeRewardDeck();    // 初始化奖励牌堆
            InitializePhysicsDeck();   // 初始化物理量牌堆
        }

        /// <summary>初始化事件牌堆：26张事件牌</summary>
        private void InitializeEventDeck()
        {
            eventDeck = new Deck<EventCardId>();
            var allIds = EventCardDatabase.GetAllIds(); // 获取所有事件牌ID
            foreach (var id in allIds)
            {
                eventDeck.AddToDraw(id);   // 加入抽牌堆
            }
            eventDeck.Shuffle();           // 洗牌
        }

        /// <summary>初始化奖励牌堆：25张奖励牌</summary>
        private void InitializeRewardDeck()
        {
            rewardDeck = new Deck<RewardCardId>();
            var allIds = RewardCardDatabase.GetAllIds(); // 获取所有奖励牌ID
            foreach (var id in allIds)
            {
                rewardDeck.AddToDraw(id);  // 加入抽牌堆
            }
            rewardDeck.Shuffle();          // 洗牌
        }

        /// <summary>
        /// 初始化物理量牌堆（用于商店购买和随机获取）
        /// 根据每种卡牌的totalCount创建对应数量的牌
        /// </summary>
        private void InitializePhysicsDeck()
        {
            physicsDeck = new Deck<PhysicsCardId>();
            var allDefs = CardDatabase.GetAll(); // 获取所有卡牌定义

            foreach (var kvp in allDefs)
            {
                var def = kvp.Value;
                // 总数减去开局已发放的数量（根据实际玩家数）
                int deckCount = def.totalCount - (def.startCount * _playerCount);
                for (int i = 0; i < deckCount && deckCount > 0; i++)
                {
                    physicsDeck.AddToDraw(def.id); // 加入抽牌堆
                }
            }
            physicsDeck.Shuffle();             // 洗牌
        }

        /// <summary>抽一张事件牌</summary>
        public EventCardId DrawEvent()
        {
            return eventDeck.Draw();
        }

        /// <summary>抽一张奖励牌</summary>
        public RewardCardId DrawReward()
        {
            return rewardDeck.Draw();
        }

        /// <summary>
        /// 随机获取一张基本物理量牌
        /// 规则：掷骰子(不受修正)，1=时间,2=长度,3=质量,4=电流,5=温度,6=光照
        /// </summary>
        public PhysicsCardId DrawRandomBasic(int diceRoll)
        {
            switch (diceRoll)
            {
                case 1: return PhysicsCardId.Time;              // 1=时间
                case 2: return PhysicsCardId.Length;             // 2=长度
                case 3: return PhysicsCardId.Mass;              // 3=质量
                case 4: return PhysicsCardId.Current;           // 4=电流
                case 5: return PhysicsCardId.Temperature;       // 5=温度
                case 6: return PhysicsCardId.LuminousIntensity; // 6=光照强度
                default: return PhysicsCardId.Time;
            }
        }

        /// <summary>
        /// 随机获取一张非基本物理量牌
        /// 规则：从力学量抽3张+电磁学量抽2张+热学量抽1张，编号1~6掷骰
        /// </summary>
        public PhysicsCardId DrawRandomNonBasic()
        {
            // 获取各类别的非基本物理量
            var mechanics = GetCardsByBranch(PhysicsBranch.Mechanics);
            var em = GetCardsByBranch(PhysicsBranch.Electromagnetics);
            var thermo = GetCardsByBranch(PhysicsBranch.Thermodynamics);

            // 随机选取
            var pool = new List<PhysicsCardId>();
            pool.AddRange(RandomSelect(mechanics, 3)); // 力学量抽3
            pool.AddRange(RandomSelect(em, 2));         // 电磁学量抽2
            pool.AddRange(RandomSelect(thermo, 1));     // 热学量抽1

            if (pool.Count < 6)
            {
                // 如果不足6张，用已有的补充
                while (pool.Count < 6 && pool.Count > 0)
                    pool.Add(pool[_random.Next(pool.Count)]);
            }

            int roll = _random.Next(0, pool.Count); // 掷骰决定
            return pool[roll];
        }

        /// <summary>获取指定学科分类的所有非基本物理量</summary>
        private List<PhysicsCardId> GetCardsByBranch(PhysicsBranch branch)
        {
            var result = new List<PhysicsCardId>();
            foreach (var kvp in CardDatabase.GetAll())
            {
                if (kvp.Value.branch == branch && !CardDatabase.IsBasicQuantity(kvp.Key))
                    result.Add(kvp.Key);
            }
            return result;
        }

        /// <summary>从列表中随机选取n个元素</summary>
        private List<T> RandomSelect<T>(List<T> source, int count)
        {
            var shuffled = source.OrderBy(x => _random.Next()).ToList();
            return shuffled.Take(Math.Min(count, shuffled.Count)).ToList();
        }
    }
}
