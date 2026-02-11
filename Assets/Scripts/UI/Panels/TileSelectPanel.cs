// ============================================================
// TileSelectPanel.cs — 格子选择面板
// 用于量子隧穿传送目标、建筑放置等
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Systems;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class TileSelectPanel : MonoBehaviour
    {
        [SerializeField] private Text titleText;
        [SerializeField] private Text instructionText;
        [SerializeField] private Button cancelButton;

        private Action<int> _onSelect;
        private TileSelectRequest _request;

        private void Awake()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(() => { _onSelect?.Invoke(-1); _onSelect = null; });
        }

        public void Show(TileSelectRequest request, Action<int> onSelect)
        {
            _request = request;
            _onSelect = onSelect;

            if (titleText != null) titleText.text = request.title;
            if (instructionText != null) instructionText.text = "点击棋盘上的格子来选择";

            // 高亮所有可选格子
            var uiMgr = UIManager.Instance;
            if (uiMgr != null)
            {
                uiMgr.ClearAllHighlights();
                var board = GameManager.Instance?.Board;
                if (board != null)
                {
                    for (int i = 0; i < board.TotalTiles; i++)
                    {
                        if (request.filter == null || request.filter(i))
                            uiMgr.HighlightTile(i, true);
                    }
                }
            }
        }

        /// <summary>由棋盘视觉层调用：玩家点击了某个格子</summary>
        public void OnTileClicked(int tileIndex)
        {
            if (_request != null && _onSelect != null)
            {
                if (_request.filter == null || _request.filter(tileIndex))
                {
                    UIManager.Instance?.ClearAllHighlights();
                    var cb = _onSelect;
                    _onSelect = null;
                    cb(tileIndex);
                }
            }
        }
    }
}
