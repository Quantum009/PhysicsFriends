// ============================================================
// Singleton.cs — 单例基类（普通MonoBehaviour & NetworkBehaviour）
// ============================================================
using UnityEngine;
using Unity.Netcode;

namespace PhysicsFriends.Utils
{
    /// <summary>普通单例（非网络对象，如 UIManager, AudioManager）</summary>
    public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }

    /// <summary>网络单例：所有 Server-authoritative Manager 继承此类</summary>
    public class NetworkSingleton<T> : NetworkBehaviour where T : NetworkBehaviour
    {
        public static T Instance { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this as T;
        }

        public override void OnNetworkDespawn()
        {
            if (Instance == this) Instance = null;
        }
    }
}
