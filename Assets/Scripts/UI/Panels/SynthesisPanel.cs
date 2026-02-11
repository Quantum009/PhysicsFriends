// ============================================================
// SynthesisPanel.cs — 合成面板：展示可用合成配方
// ============================================================
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using PhysicsFriends.Data;
using PhysicsFriends.Systems;

using PhysicsFriends.UI;
namespace PhysicsFriends.UI.Panels
{
    public class SynthesisPanel : MonoBehaviour
    {
        [Header("UI引用")]
        [SerializeField] private Text titleText;
        [SerializeField] private Transform recipeContainer;
        [SerializeField] private GameObject recipePrefab;
        [SerializeField] private Button cancelButton;
        [SerializeField] private Text handCountText;

        private Action<SynthesisResponse> _onComplete;
        private SynthesisRequest _request;
        private List<GameObject> _recipeViews = new List<GameObject>();

        private void Awake()
        {
            if (cancelButton != null)
                cancelButton.onClick.AddListener(OnCancel);
        }

        public void Show(SynthesisRequest request, Action<SynthesisResponse> onComplete)
        {
            _request = request;
            _onComplete = onComplete;

            if (titleText != null)
                titleText.text = request.unlimitedAttempts ? "合成（不限次数）" : "合成";
            if (handCountText != null)
                handCountText.text = $"手牌 {request.player.handCards.Count}/{Player.PlayerState.MaxHandCards}";

            // 清除旧视图
            foreach (var v in _recipeViews) Destroy(v);
            _recipeViews.Clear();

            // 生成合成配方条目
            foreach (var synth in request.possibleSyntheses)
            {
                var targetDef = CardDatabase.Get(synth.targetId);
                string materialNames = string.Join(" + ",
                    synth.materialIds.Select(id => CardDatabase.Get(id)?.nameZH ?? id.ToString()));
                string label = $"{materialNames} → {targetDef?.nameZH}";

                GameObject obj;
                if (recipePrefab != null)
                {
                    obj = Instantiate(recipePrefab, recipeContainer);
                }
                else
                {
                    obj = new GameObject("Recipe");
                    obj.transform.SetParent(recipeContainer ?? transform);
                    obj.AddComponent<RectTransform>();
                    var btn = obj.AddComponent<Button>();
                    var textObj = new GameObject("Text");
                    textObj.transform.SetParent(obj.transform);
                    textObj.AddComponent<Text>().text = label;
                }

                var text = obj.GetComponentInChildren<Text>();
                if (text != null) text.text = label;

                var button = obj.GetComponent<Button>();
                var capturedSynth = synth;
                if (button != null)
                    button.onClick.AddListener(() => OnSelectRecipe(capturedSynth));

                _recipeViews.Add(obj);
            }

            if (request.possibleSyntheses.Count == 0)
            {
                if (titleText != null) titleText.text = "没有可用的合成配方";
            }
        }

        private void OnSelectRecipe(SynthesisResult synth)
        {
            // 找到手牌中对应的材料卡
            var materials = new List<Cards.CardInstance>();
            var tempHand = new List<Cards.CardInstance>(_request.player.handCards);
            foreach (var matId in synth.materialIds)
            {
                var card = tempHand.FirstOrDefault(c => c.cardId == matId && !c.isUsed);
                if (card != null)
                {
                    materials.Add(card);
                    tempHand.Remove(card);
                }
            }

            _onComplete?.Invoke(new SynthesisResponse
            {
                doSynthesize = true,
                targetId = synth.targetId,
                materialsUsed = materials
            });
            _onComplete = null;
        }

        private void OnCancel()
        {
            _onComplete?.Invoke(new SynthesisResponse { doSynthesize = false });
            _onComplete = null;
        }
    }
}
