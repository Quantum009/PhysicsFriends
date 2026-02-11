// ============================================================
// GameOverPanel.cs — 游戏结束面板
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Core;
using PhysicsFriends.Player;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private Text winnerText;
        [SerializeField] private Text victoryTypeText;
        [SerializeField] private Text statsText;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button quitButton;

        public void Show(PlayerState winner, VictoryType victoryType)
        {
            gameObject.SetActive(true);

            if (winnerText != null)
                winnerText.text = $"{winner.playerName} 获胜！";

            if (victoryTypeText != null)
            {
                string typeStr = victoryType == VictoryType.Wealth
                    ? $"财富胜利（{winner.mol} mol）"
                    : $"创举胜利（{winner.achievementPoints} 分）";
                victoryTypeText.text = typeStr;
            }

            if (statsText != null)
            {
                statsText.text = $"角色：{winner.character}\n" +
                                 $"mol：{winner.mol}\n" +
                                 $"创举分：{winner.achievementPoints}\n" +
                                 $"手牌：{winner.handCards.Count}张\n" +
                                 $"建筑：{winner.buildings.Count}座";
            }

            if (restartButton != null)
                restartButton.onClick.AddListener(() =>
                    UnityEngine.SceneManagement.SceneManager.LoadScene(
                        UnityEngine.SceneManagement.SceneManager.GetActiveScene().name));
        }
    }
}
