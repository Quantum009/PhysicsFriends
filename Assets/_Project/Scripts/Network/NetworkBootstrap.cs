// ============================================================
// NetworkBootstrap.cs — Unity Gaming Services 初始化
// 放在 MainMenu 场景中，游戏启动时自动初始化
// ============================================================
using System.Threading.Tasks;
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;

namespace PhysicsFriends.Network
{
    public class NetworkBootstrap : MonoBehaviour
    {
        public static bool IsInitialized { get; private set; }
        public static string PlayerId { get; private set; }

        private async void Start()
        {
            if (IsInitialized) return;

            try
            {
                // 1. 初始化 Unity Services
                await UnityServices.InitializeAsync();
                Debug.Log("[Network] Unity Services initialized");

                // 2. 匿名登录
                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    PlayerId = AuthenticationService.Instance.PlayerId;
                    Debug.Log($"[Network] Signed in as: {PlayerId}");
                }

                IsInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Network] 初始化失败: {e.Message}");
                // 降级为离线模式
            }
        }
    }
}
