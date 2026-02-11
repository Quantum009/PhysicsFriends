// ============================================================
// IUIProvider.cs — UI交互接口
// 定义所有需要UI参与的交互方法。
// TurnManager通过此接口请求UI操作，具体实现由UIManager提供。
// 所有方法使用 UICallback<T> 异步返回结果，
// TurnManager在协程中 yield return 等待。
// ============================================================
using System.Collections.Generic;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Player;
using PhysicsFriends.Cards;
using PhysicsFriends.Systems;

namespace PhysicsFriends.UI
{
    /// <summary>
    /// UI交互提供者接口。
    /// 游戏逻辑通过此接口与UI层通信，实现逻辑与表现的解耦。
    /// </summary>
    public interface IUIProvider
    {
        // =============================================================
        // 骰子
        // =============================================================

        /// <summary>展示骰子投掷结果，等待玩家确认</summary>
        UICallback<bool> ShowDiceRoll(DiceRollRequest request);

        /// <summary>询问薛定谔玩家是否重投骰子</summary>
        UICallback<DiceRerollResponse> AskReroll(DiceRollRequest request);

        // =============================================================
        // 修正链
        // =============================================================

        /// <summary>询问玩家是否使用力修正及方向</summary>
        UICallback<ForceModResponse> AskForceModification(ForceModRequest request);

        /// <summary>询问掷骰者加速度修正方向</summary>
        UICallback<AccelModResponse> AskAccelModification(AccelModRequest request);

        /// <summary>询问是否使用压强无效化</summary>
        UICallback<bool> AskPressureNullify(PressureNullifyRequest request);

        /// <summary>通知弹性系数钳制结果</summary>
        void NotifySpringClamp(SpringClampNotice notice);

        // =============================================================
        // 通用选择
        // =============================================================

        /// <summary>显示通用选择对话框（多选一）</summary>
        UICallback<string> ShowChoice(ChoiceRequest request);

        /// <summary>显示确认对话框（是/否）</summary>
        UICallback<bool> ShowConfirm(string title, string message, PlayerState player = null);

        // =============================================================
        // 卡牌选择
        // =============================================================

        /// <summary>让玩家从手牌中选择卡牌</summary>
        UICallback<CardSelectResponse> SelectCards(CardSelectRequest request);

        /// <summary>让玩家选择基本物理量类型</summary>
        UICallback<BasicCardChoiceResponse> SelectBasicCards(BasicCardChoiceRequest request);

        // =============================================================
        // 棋盘格子选择
        // =============================================================

        /// <summary>让玩家选择棋盘上的格子</summary>
        UICallback<TileSelectResponse> SelectTile(TileSelectRequest request);

        // =============================================================
        // 目标玩家选择
        // =============================================================

        /// <summary>让玩家选择一个目标玩家</summary>
        UICallback<PlayerTargetResponse> SelectTargetPlayer(PlayerTargetRequest request);

        // =============================================================
        // 事件/奖励
        // =============================================================

        /// <summary>展示事件牌并等待确认</summary>
        UICallback<bool> ShowEventCard(EventCardShowRequest request);

        /// <summary>展示奖励牌并等待确认</summary>
        UICallback<bool> ShowRewardCard(RewardCardShowRequest request);

        // =============================================================
        // 特殊事件交互
        // =============================================================

        /// <summary>相变形态选择（固/液/气）</summary>
        UICallback<PhaseChoiceResponse> AskPhaseChoice(PhaseChoiceRequest request);

        /// <summary>费曼赌注：猜奇偶</summary>
        UICallback<FeynmanBetResponse> AskFeynmanBet(FeynmanBetRequest request);

        /// <summary>核反应堆：是否继续掷骰</summary>
        UICallback<NuclearContinueResponse> AskNuclearContinue(NuclearContinueRequest request);

        // =============================================================
        // 商店/交易
        // =============================================================

        /// <summary>商店购买交互</summary>
        UICallback<ShopPurchaseResponse> ShowShop(ShopPurchaseRequest request);

        /// <summary>交易界面</summary>
        UICallback<TradeResponse> ShowTrade(TradeRequest request);

        // =============================================================
        // 合成
        // =============================================================

        /// <summary>合成选择界面</summary>
        UICallback<SynthesisResponse> ShowSynthesis(SynthesisRequest request);

        // =============================================================
        // 自由行动
        // =============================================================

        /// <summary>自由行动阶段菜单</summary>
        UICallback<FreeActionResponse> ShowFreeActionMenu(FreeActionRequest request);

        // =============================================================
        // 游戏设置
        // =============================================================

        /// <summary>游戏开始设置界面</summary>
        UICallback<GameSetupResponse> ShowGameSetup(GameSetupRequest request);

        // =============================================================
        // HUD更新（非阻塞）
        // =============================================================

        /// <summary>更新HUD显示</summary>
        void UpdateHUD(List<PlayerState> players, int currentPlayerIndex,
            int roundNumber, Era era);

        /// <summary>更新手牌显示</summary>
        void UpdateHandDisplay(PlayerState player);

        /// <summary>高亮棋盘格子</summary>
        void HighlightTile(int tileIndex, bool highlight);

        /// <summary>清除所有高亮</summary>
        void ClearAllHighlights();

        // =============================================================
        // 通知/日志
        // =============================================================

        /// <summary>发送游戏通知（非阻塞，在通知栏/日志中显示）</summary>
        void SendNotification(GameNotification notification);

        /// <summary>发送通知并等待淡出（可选的短暂等待）</summary>
        UICallback<bool> SendNotificationAndWait(GameNotification notification);

        // =============================================================
        // 棋子动画
        // =============================================================

        /// <summary>播放棋子移动动画，完成后回调</summary>
        UICallback<bool> AnimateMovement(PlayerState player, int fromTile, int toTile,
            bool passedStart);

        /// <summary>播放棋子传送动画（量子隧穿/虫洞）</summary>
        UICallback<bool> AnimateTeleport(PlayerState player, int fromTile, int toTile);
    }
}
