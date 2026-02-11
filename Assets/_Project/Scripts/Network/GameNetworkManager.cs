// ============================================================
// GameNetworkManager.cs — 网络消息中枢
// 集中处理所有 ServerRpc（客户端→Host）和 ClientRpc（Host→客户端）
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
using PhysicsFriends.Core;
using PhysicsFriends.Data;
using PhysicsFriends.Utils;

namespace PhysicsFriends.Network
{
    public class GameNetworkManager : NetworkSingleton<GameNetworkManager>
    {
        // ---- 待处理的玩家输入（Host 端等待客户端回复）----
        private Dictionary<int, Action<int>> _pendingChoiceInputs = new();
        private Dictionary<int, Action<bool>> _pendingBoolInputs = new();

        // ---- clientId → playerIndex 映射 ----
        private Dictionary<ulong, int> _clientToPlayer = new();

        public void RegisterPlayer(ulong clientId, int playerIndex)
        {
            _clientToPlayer[clientId] = playerIndex;
        }

        public int GetPlayerIndex(ulong clientId)
        {
            return _clientToPlayer.TryGetValue(clientId, out int idx) ? idx : -1;
        }

        public int GetLocalPlayerIndex()
        {
            if (NetworkManager.Singleton == null) return 0;
            return GetPlayerIndex(NetworkManager.Singleton.LocalClientId);
        }

        // ================================================================
        // 客户端 → 服务端 (ServerRpc)
        // ================================================================

        /// <summary>请求投骰子</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestRollDiceServerRpc(ServerRpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            int idx = GetPlayerIndex(clientId);
            if (idx < 0) return;
            // 转发给 GameManager
            Debug.Log($"[Net] Player {idx} requested dice roll");
        }

        /// <summary>请求使用力修正</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestUseForceDiceServerRpc(int targetPlayerIndex, int modifier,
            ServerRpcParams rpcParams = default)
        {
            int idx = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            Debug.Log($"[Net] Player {idx} uses force ({modifier:+0;-0}) on player {targetPlayerIndex}");
        }

        /// <summary>请求合成</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestSynthesisServerRpc(int[] materialInstanceIds, int targetCardId,
            ServerRpcParams rpcParams = default)
        {
            int idx = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            Debug.Log($"[Net] Player {idx} requests synthesis → card {targetCardId}");
        }

        /// <summary>请求购买卡牌</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestBuyCardServerRpc(int cardId, bool isRandom,
            ServerRpcParams rpcParams = default)
        {
            int idx = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            Debug.Log($"[Net] Player {idx} buys card {cardId} (random={isRandom})");
        }

        /// <summary>请求使用主动卡</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestUseActiveCardServerRpc(int cardInstanceId, int targetPlayerIndex,
            ServerRpcParams rpcParams = default)
        {
            int idx = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            Debug.Log($"[Net] Player {idx} uses active card {cardInstanceId} on {targetPlayerIndex}");
        }

        /// <summary>请求结束自由行动</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestEndFreeActionServerRpc(ServerRpcParams rpcParams = default)
        {
            int idx = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            Debug.Log($"[Net] Player {idx} ends free action");
        }

        /// <summary>回复选择结果（通用）</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReplyChoiceServerRpc(int choiceIndex, ServerRpcParams rpcParams = default)
        {
            int idx = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            if (_pendingChoiceInputs.TryGetValue(idx, out var callback))
            {
                callback(choiceIndex);
                _pendingChoiceInputs.Remove(idx);
            }
        }

        /// <summary>回复是/否结果</summary>
        [ServerRpc(RequireOwnership = false)]
        public void ReplyBoolServerRpc(bool value, ServerRpcParams rpcParams = default)
        {
            int idx = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            if (_pendingBoolInputs.TryGetValue(idx, out var callback))
            {
                callback(value);
                _pendingBoolInputs.Remove(idx);
            }
        }

        /// <summary>请求交易</summary>
        [ServerRpc(RequireOwnership = false)]
        public void RequestTradeServerRpc(int otherPlayerIndex,
            int[] offeredCardIds, int offeredMol,
            int[] requestedCardIds, int requestedMol,
            ServerRpcParams rpcParams = default)
        {
            int idx = GetPlayerIndex(rpcParams.Receive.SenderClientId);
            Debug.Log($"[Net] Player {idx} requests trade with {otherPlayerIndex}");
        }

        // ================================================================
        // 服务端 → 客户端 (ClientRpc)
        // ================================================================

        /// <summary>通知骰子结果</summary>
        [ClientRpc]
        public void NotifyDiceResultClientRpc(int playerIndex, int rawValue, int finalValue)
        {
            GameEvents.FireDiceRolled(playerIndex, rawValue);
            GameEvents.FireDiceFinalResolved(playerIndex, finalValue);
        }

        /// <summary>通知事件牌</summary>
        [ClientRpc]
        public void NotifyEventCardClientRpc(int playerIndex, int eventId)
        {
            GameEvents.FireEventTriggered(playerIndex, (EventCardId)eventId);
        }

        /// <summary>通知奖励牌</summary>
        [ClientRpc]
        public void NotifyRewardCardClientRpc(int playerIndex, int rewardId)
        {
            GameEvents.FireRewardTriggered(playerIndex, (RewardCardId)rewardId);
        }

        /// <summary>通知创举达成</summary>
        [ClientRpc]
        public void NotifyAchievementClientRpc(int playerIndex, int achId)
        {
            GameEvents.FireAchievementCompleted(playerIndex, (AchievementId)achId);
        }

        /// <summary>通知时代变化</summary>
        [ClientRpc]
        public void NotifyEraChangedClientRpc(int era)
        {
            GameEvents.FireEraChanged((Era)era);
        }

        /// <summary>通知棋子移动</summary>
        [ClientRpc]
        public void NotifyPlayerMovedClientRpc(int playerIndex, int fromTile, int toTile)
        {
            GameEvents.FirePlayerMoved(playerIndex, fromTile, toTile);
        }

        /// <summary>显示 Toast 消息</summary>
        [ClientRpc]
        public void ShowToastClientRpc(FixedString128Bytes message)
        {
            Debug.Log($"[Toast] {message}");
            // UIManager.Instance?.ShowToast(message.ToString());
        }

        /// <summary>请求特定玩家输入（仅发给目标客户端）</summary>
        [ClientRpc]
        public void RequestPlayerInputClientRpc(int playerIndex,
            FixedString64Bytes inputType, FixedString512Bytes context,
            ClientRpcParams rpcParams = default)
        {
            if (GetLocalPlayerIndex() != playerIndex) return;
            // 只有目标玩家显示输入UI
            Debug.Log($"[Input] 等待玩家 {playerIndex} 输入: {inputType} ({context})");
            // UIManager.Instance?.ShowInputRequest(inputType.ToString(), context.ToString());
        }

        /// <summary>通知胜利</summary>
        [ClientRpc]
        public void NotifyVictoryClientRpc(int playerIndex, int victoryType)
        {
            GameEvents.FireVictory(playerIndex, (VictoryType)victoryType);
        }

        // ================================================================
        // Host 端工具：等待玩家输入
        // ================================================================

        /// <summary>向指定玩家请求选择，阻塞直到收到回复（带超时）</summary>
        public void RequestChoice(int playerIndex, string[] options,
            Action<int> onResult, float timeout = 30f)
        {
            _pendingChoiceInputs[playerIndex] = onResult;

            // 构造选项字符串
            string optStr = string.Join("|", options);
            // 发给目标客户端
            var target = GetClientRpcParamsForPlayer(playerIndex);
            RequestPlayerInputClientRpc(playerIndex, "choice", optStr, target);

            // 超时处理
            StartCoroutine(TimeoutCoroutine(playerIndex, timeout, () =>
            {
                if (_pendingChoiceInputs.ContainsKey(playerIndex))
                {
                    _pendingChoiceInputs[playerIndex](0); // 超时默认选第一个
                    _pendingChoiceInputs.Remove(playerIndex);
                }
            }));
        }

        /// <summary>向指定玩家请求是/否</summary>
        public void RequestYesNo(int playerIndex, string question,
            Action<bool> onResult, float timeout = 30f)
        {
            _pendingBoolInputs[playerIndex] = onResult;

            var target = GetClientRpcParamsForPlayer(playerIndex);
            RequestPlayerInputClientRpc(playerIndex, "yes_no", question, target);

            StartCoroutine(TimeoutCoroutine(playerIndex, timeout, () =>
            {
                if (_pendingBoolInputs.ContainsKey(playerIndex))
                {
                    _pendingBoolInputs[playerIndex](false);
                    _pendingBoolInputs.Remove(playerIndex);
                }
            }));
        }

        private ClientRpcParams GetClientRpcParamsForPlayer(int playerIndex)
        {
            ulong targetClientId = _clientToPlayer
                .FirstOrDefault(kv => kv.Value == playerIndex).Key;
            return new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { targetClientId }
                }
            };
        }

        private System.Collections.IEnumerator TimeoutCoroutine(
            int playerIndex, float timeout, Action onTimeout)
        {
            yield return new WaitForSeconds(timeout);
            onTimeout?.Invoke();
        }
    }
}
