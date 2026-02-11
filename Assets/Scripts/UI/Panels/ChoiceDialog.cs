// ============================================================
// ChoiceDialog.cs — 通用选择对话框
// 支持多选一、确认/取消等场景
// ============================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    /// <summary>
    /// 通用选择对话框：显示标题、说明和若干按钮选项。
    /// 用于相变选择、费曼赌注、各种确认弹窗等。
    /// </summary>
    public class ChoiceDialog : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text messageText;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private GameObject buttonPrefab;     // 选项按钮预制体
        [SerializeField] private Button cancelButton;

        private Action<string> _onChoice;
        private List<GameObject> _spawnedButtons = new List<GameObject>();

        private void Awake()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(() => OnOptionClick(null));
        }

        /// <summary>显示选择对话框</summary>
        public void Show(ChoiceRequest request, Action<string> onChoice)
        {
            _onChoice = onChoice;

            if (titleText != null)
                titleText.text = request.title;
            if (messageText != null)
                messageText.text = request.message;

            // 清除旧按钮
            foreach (var btn in _spawnedButtons)
                Destroy(btn);
            _spawnedButtons.Clear();

            // 生成选项按钮
            foreach (var option in request.options)
            {
                GameObject btnObj;
                if (buttonPrefab != null && buttonContainer != null)
                {
                    btnObj = Instantiate(buttonPrefab, buttonContainer);
                }
                else
                {
                    btnObj = new GameObject(option.id);
                    btnObj.transform.SetParent(buttonContainer ?? transform);
                    btnObj.AddComponent<RectTransform>();
                    var btn = btnObj.AddComponent<Button>();
                    var textObj = new GameObject("Text");
                    textObj.transform.SetParent(btnObj.transform);
                    textObj.AddComponent<Text>();
                }

                var button = btnObj.GetComponent<Button>();
                var text = btnObj.GetComponentInChildren<Text>();

                if (text != null)
                {
                    text.text = string.IsNullOrEmpty(option.description)
                        ? option.label
                        : $"{option.label}\n<size=12>{option.description}</size>";
                }

                if (button != null)
                {
                    button.interactable = option.enabled;
                    string id = option.id; // 闭包捕获
                    button.onClick.AddListener(() => OnOptionClick(id));
                }

                _spawnedButtons.Add(btnObj);
            }

            // 取消按钮
            if (cancelButton != null)
                cancelButton.gameObject.SetActive(request.allowCancel);
        }

        private void OnOptionClick(string id)
        {
            var callback = _onChoice;
            _onChoice = null;
            callback?.Invoke(id);
        }
    }
}
