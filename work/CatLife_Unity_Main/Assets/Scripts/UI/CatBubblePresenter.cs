using UnityEngine;
using UnityEngine.UI;

namespace CatLife.UI
{
    [DisallowMultipleComponent]
    public sealed class CatBubblePresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform bubbleRoot;
        [SerializeField] private Text bubbleText;
        [SerializeField] private float defaultVisibleSeconds = 4f;

        private float hideAt;

        private void Awake()
        {
            EnsureBubble();
            Hide();
        }

        private void Update()
        {
            if (bubbleRoot != null && bubbleRoot.gameObject.activeSelf && Time.unscaledTime >= hideAt)
            {
                Hide();
            }
        }

        public void Show(string text, string source)
        {
            EnsureBubble();
            if (bubbleText == null || bubbleRoot == null)
            {
                return;
            }

            string safeText = string.IsNullOrEmpty(text) ? "猫咪会继续陪你。" : text;
            bubbleText.text = safeText + "\n<color=#8F541C>" + GetSourceLabel(source) + "</color>";
            bubbleRoot.gameObject.SetActive(true);
            hideAt = Time.unscaledTime + Mathf.Max(1f, defaultVisibleSeconds);
        }

        public void Hide()
        {
            if (bubbleRoot != null)
            {
                bubbleRoot.gameObject.SetActive(false);
            }
        }

        private void EnsureBubble()
        {
            if (bubbleRoot != null && bubbleText != null)
            {
                return;
            }

            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            GameObject root = new GameObject("CatFeedbackBubble", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvasRect, false);
            bubbleRoot = root.GetComponent<RectTransform>();
            bubbleRoot.anchorMin = new Vector2(0.5f, 0f);
            bubbleRoot.anchorMax = new Vector2(0.5f, 0f);
            bubbleRoot.pivot = new Vector2(0.5f, 0f);
            bubbleRoot.anchoredPosition = new Vector2(0f, 216f);
            bubbleRoot.sizeDelta = new Vector2(650f, 118f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(1f, 0.96f, 0.78f, 0.92f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject("CatFeedbackBubbleText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(24f, 14f);
            textRect.offsetMax = new Vector2(-24f, -12f);

            bubbleText = textObject.GetComponent<Text>();
            bubbleText.font = font;
            bubbleText.fontSize = 26;
            bubbleText.fontStyle = FontStyle.Bold;
            bubbleText.alignment = TextAnchor.MiddleCenter;
            bubbleText.color = new Color(0.34f, 0.18f, 0.06f, 1f);
            bubbleText.supportRichText = true;
            bubbleText.raycastTarget = false;
        }

        private static string GetSourceLabel(string source)
        {
            return source == "mock_llm" ? "智能反馈" : "本地反馈";
        }
    }
}
