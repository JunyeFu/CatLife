using UnityEngine;
using UnityEngine.UI;

namespace CatLife.UI
{
    [DisallowMultipleComponent]
    public sealed class CatBubblePresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform bubbleRoot;
        [SerializeField] private Text bubbleText;
        [SerializeField] private Image bubbleBackground;
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
            bubbleText.text = FormatGuidanceText(safeText, source);
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
                DisableBackground();
                return;
            }

            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject root = new GameObject("CatFeedbackBubble", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvasRect, false);
            bubbleRoot = root.GetComponent<RectTransform>();
            bubbleRoot.anchorMin = new Vector2(0.5f, 0f);
            bubbleRoot.anchorMax = new Vector2(0.5f, 0f);
            bubbleRoot.pivot = new Vector2(0.5f, 0f);
            bubbleRoot.anchoredPosition = new Vector2(0f, 216f);
            bubbleRoot.sizeDelta = new Vector2(720f, 104f);

            bubbleBackground = root.GetComponent<Image>();
            DisableBackground();

            GameObject textObject = new GameObject("CatFeedbackBubbleText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            bubbleText = textObject.GetComponent<Text>();
            bubbleText.font = font;
            bubbleText.fontSize = 30;
            bubbleText.fontStyle = FontStyle.Bold;
            bubbleText.alignment = TextAnchor.MiddleCenter;
            bubbleText.color = new Color(1f, 0.96f, 0.86f, 1f);
            bubbleText.supportRichText = true;
            bubbleText.raycastTarget = false;

            Shadow shadow = textObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.25f, 0.12f, 0.03f, 0.55f);
            shadow.effectDistance = new Vector2(0f, -3f);

            Outline outline = textObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.23f, 0.11f, 0.03f, 0.72f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);
        }

        private void DisableBackground()
        {
            if (bubbleBackground == null && bubbleRoot != null)
            {
                bubbleBackground = bubbleRoot.GetComponent<Image>();
            }

            if (bubbleBackground == null)
            {
                return;
            }

            bubbleBackground.color = new Color(1f, 1f, 1f, 0f);
            bubbleBackground.raycastTarget = false;
            bubbleBackground.enabled = false;
        }

        private static string FormatGuidanceText(string text, string source)
        {
            string safeSource = string.IsNullOrEmpty(source) ? "local_template" : source;
            int style = StableStyleIndex(text, safeSource, 5);
            bool smart = IsSmartSource(safeSource);

            switch (style)
            {
                case 0:
                    return text;
                case 1:
                    return smart ? "猫咪观察到：" + text : "猫咪轻声：" + text;
                case 2:
                    return "喵，" + text;
                case 3:
                    return smart ? "小提示｜" + text : "陪伴提示｜" + text;
                default:
                    return "“" + text + "”";
            }
        }

        private static int StableStyleIndex(string text, string source, int count)
        {
            int hash = 17;
            string combined = (text ?? "") + "|" + (source ?? "");
            for (int i = 0; i < combined.Length; i++)
            {
                hash = hash * 31 + combined[i];
            }

            return Mathf.Abs(hash) % Mathf.Max(1, count);
        }

        private static bool IsSmartSource(string source)
        {
            return source == "mock_llm" ||
                source == "mock_llm_structured" ||
                source == "llm_structured";
        }
    }
}
