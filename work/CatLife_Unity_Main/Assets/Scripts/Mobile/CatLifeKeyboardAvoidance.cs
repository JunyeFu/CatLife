using UnityEngine;
using UnityEngine.UI;

// Only lift the active input's card while the docked Android IME covers that input.
// Normal positions and InputField's keyboard ownership are unchanged.
[DefaultExecutionOrder(9600)]
public sealed class CatLifeKeyboardAvoidance : MonoBehaviour
{
    [SerializeField] private InputField input;
    [SerializeField] private RectTransform card;
    private Canvas canvas;
    private readonly Vector3[] corners = new Vector3[4];
    private Vector2 restingPosition;
    private float lift;
    private bool shifted;
#if UNITY_ANDROID && !UNITY_EDITOR
    private AndroidJavaObject decor, visibleFrame;
    private int sdk, imeType;
#endif
#if UNITY_EDITOR
    private float reviewInset;
    public void SetReviewInset(float pixels) { reviewInset = pixels; }
#endif
    public void Configure(InputField field, RectTransform panel) { input = field; card = panel; }
    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
#if UNITY_ANDROID && !UNITY_EDITOR
        using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
        using var window = activity.Call<AndroidJavaObject>("getWindow");
        decor = window.Call<AndroidJavaObject>("getDecorView");
        using var version = new AndroidJavaClass("android.os.Build$VERSION");
        sdk = version.GetStatic<int>("SDK_INT");
        if (sdk >= 30)
        {
            using var type = new AndroidJavaClass("android.view.WindowInsets$Type");
            imeType = type.CallStatic<int>("ime");
        }
        else visibleFrame = new AndroidJavaObject("android.graphics.Rect");
#endif
    }
    private float KeyboardInset()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        if (sdk >= 30)
        {
            using var insets = decor.Call<AndroidJavaObject>("getRootWindowInsets");
            if (insets == null || !insets.Call<bool>("isVisible", imeType)) return 0;
            using var ime = insets.Call<AndroidJavaObject>("getInsets", imeType);
            return ime.Get<int>("bottom");
        }
        if (!TouchScreenKeyboard.visible) return 0;
        decor.Call("getWindowVisibleDisplayFrame", visibleFrame);
        return Mathf.Max(0, Screen.height - visibleFrame.Get<int>("bottom"));
#elif UNITY_EDITOR
        return reviewInset;
#else
        return 0;
#endif
    }
    private void LateUpdate()
    {
        if (!input.isFocused) { Restore(); return; }
        float inset = KeyboardInset();
        if (inset <= 0) { Restore(); return; }
        if (!shifted) { restingPosition = card.anchoredPosition; shifted = true; }
        ((RectTransform)input.transform).GetWorldCorners(corners);
        float bottom = RectTransformUtility.WorldToScreenPoint(null, corners[0]).y - lift * canvas.scaleFactor;
        lift = Mathf.Max(0, inset + 16 * canvas.scaleFactor - bottom) / canvas.scaleFactor;
        card.anchoredPosition = restingPosition + Vector2.up * lift;
    }
    private void Restore()
    {
        if (shifted && card != null) card.anchoredPosition = restingPosition;
        lift = 0; shifted = false;
    }
    private void OnDisable() { Restore(); }
    private void OnApplicationPause(bool paused) { if (paused) Restore(); }
    private void OnDestroy()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        decor?.Dispose(); visibleFrame?.Dispose();
#endif
    }
}
