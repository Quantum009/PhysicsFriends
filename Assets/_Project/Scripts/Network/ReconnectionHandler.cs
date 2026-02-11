// ============================================================
// ReconnectionHandler.cs — 断线重连处理
// ============================================================
using UnityEngine;
using Unity.Netcode;
using PhysicsFriends.Core;
using PhysicsFriends.Utils;

namespace PhysicsFriends.Network
{
    public class ReconnectionHandler : NetworkSingleton<ReconnectionHandler>
    {
        private bool[] _playerDisconnected;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                _playerDisconnected = new bool[4];
                NetworkManager.OnClientConnectedCallback += OnClientConnected;
                NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            base.OnNetworkDespawn();
        }

        public bool IsPlayerDisconnected(int playerIndex)
        {
            return _playerDisconnected != null && playerIndex < _playerDisconnected.Length
                && _playerDisconnected[playerIndex];
        }

        private void OnClientDisconnected(ulong clientId)
        {
            int idx = GameNetworkManager.Instance.GetPlayerIndex(clientId);
            if (idx < 0) return;

            _playerDisconnected[idx] = true;
            Debug.Log($"[Reconnect] Player {idx} disconnected");
            GameEvents.FirePlayerDisconnected(idx);

            // 通知所有客户端
            GameNetworkManager.Instance.ShowToastClientRpc(
                $"玩家 {idx} 掉线（跳过回合直到重连）");
        }

        private void OnClientConnected(ulong clientId)
        {
            int idx = GameNetworkManager.Instance.GetPlayerIndex(clientId);
            if (idx < 0) return;

            if (_playerDisconnected[idx])
            {
                _playerDisconnected[idx] = false;
                Debug.Log($"[Reconnect] Player {idx} reconnected");
                GameEvents.FirePlayerConnected(idx);

                // NetworkVariable 会自动同步最新值
                GameNetworkManager.Instance.ShowToastClientRpc(
                    $"玩家 {idx} 重新连接");
            }
        }
    }
}
