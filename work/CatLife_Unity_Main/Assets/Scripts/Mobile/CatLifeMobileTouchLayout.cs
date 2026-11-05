using UnityEngine;
using UnityEngine.UI;

// Serialized only on the isolated candidate. dp uses Android's logical display density.
[DefaultExecutionOrder(9500)]
public sealed class CatLifeMobileTouchLayout : MonoBehaviour
{
    [SerializeField] private ScrollRect settingsScroll;
    [SerializeField] private RectTransform[] settingsButtons;
    [SerializeField] private CatLifeSwipeToEnd swipe;
    [SerializeField] private RectTransform swipeTrack;
    [SerializeField] private RectTransform confirmCard;
    [SerializeField] private RectTransform[] confirmButtons;
    [SerializeField] private RectTransform confirmText;
    [SerializeField] private float visualTrackHeight = 144;
    private Canvas canvas;
    private float pixelsPerDp = 2.625f;
    private void Awake()
    {
        canvas = GetComponent<Canvas>();
#if UNITY_ANDROID && !UNITY_EDITOR
        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        using var resources = activity.Call<AndroidJavaObject>("getResources");
        using var metrics = resources.Call<AndroidJavaObject>("getDisplayMetrics");
        pixelsPerDp = metrics.Get<float>("density");
#endif
    }
    public void Configure(ScrollRect scroll, RectTransform[] rows, CatLifeSwipeToEnd slider, RectTransform track,
        RectTransform card, RectTransform[] buttons, RectTransform text)
    {
        settingsScroll = scroll; settingsButtons = rows; swipe = slider; swipeTrack = track;
        confirmCard = card; confirmButtons = buttons; confirmText = text;
    }
    public void SetVisualTrackHeight(float height) { visualTrackHeight = height; }
#if UNITY_EDITOR
    public void SetReviewDensity(float value) { pixelsPerDp = value; }
#endif
    private void LateUpdate()
    {
        float dp = pixelsPerDp / canvas.scaleFactor;
        float minimum = 48 * dp;
        float fraction = float.PositiveInfinity;
        float contentHeight = settingsScroll.content.rect.height;
        foreach (var button in settingsButtons) fraction = Mathf.Min(fraction, button.rect.height / contentHeight);
        settingsScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(settingsScroll.viewport.rect.height, minimum / fraction));
        swipe.SetMinimumHandleSize(minimum);
        swipeTrack.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(visualTrackHeight, minimum));
        confirmCard.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(((RectTransform)confirmCard.parent).rect.height * .22f, 176 * dp));
        foreach (var button in confirmButtons)
        {
            button.anchorMin = new Vector2(button.anchorMin.x, 0); button.anchorMax = new Vector2(button.anchorMax.x, 0);
            button.pivot = new Vector2(.5f, 0);
            button.anchoredPosition = new Vector2(button.anchoredPosition.x, 16 * dp);
            button.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, minimum);
        }
        confirmText.anchorMin = new Vector2(confirmText.anchorMin.x, 0); confirmText.anchorMax = new Vector2(confirmText.anchorMax.x, 1);
        confirmText.offsetMin = new Vector2(confirmText.offsetMin.x, 80 * dp);
        confirmText.offsetMax = new Vector2(confirmText.offsetMax.x, -16 * dp);
    }
}
