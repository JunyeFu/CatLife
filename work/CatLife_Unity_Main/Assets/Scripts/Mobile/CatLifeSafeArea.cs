using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public sealed class CatLifeSafeArea : MonoBehaviour
{
    private Rect last;
    private void OnEnable() { Apply(); }
    private void Update() { if (last != Screen.safeArea) Apply(); }
    public void Apply()
    {
        last = Screen.safeArea;
        RectTransform rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(last.xMin / Screen.width, last.yMin / Screen.height);
        rect.anchorMax = new Vector2(last.xMax / Screen.width, last.yMax / Screen.height);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
