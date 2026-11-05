using System.Collections;
using CatLife.Mobile;
using UnityEngine;

public sealed class CatLifeCameraDirector : MonoBehaviour
{
    public readonly struct Preset
    {
        public Preset(Vector3 position, Vector3 euler, float fov, float duration) { Position = position; Euler = euler; Fov = fov; Duration = duration; }
        public Vector3 Position { get; }
        public Vector3 Euler { get; }
        public float Fov { get; }
        public float Duration { get; }
    }

    public static Preset HomePreset => new Preset(new Vector3(0f, 2.2f, 9f), new Vector3(12f, 180f, 0f), 74f, .45f);
    public static Preset TransitionPreset => new Preset(new Vector3(.15f, 2.1f, 8.6f), new Vector3(12f, 182f, 0f), 72f, 2f);
    public static Preset FocusPreset => new Preset(new Vector3(0f, 2.2f, 10.2f), new Vector3(12f, 180f, 0f), 74f, .45f);
    public static Preset RewardPreset => new Preset(new Vector3(.1f, 2.15f, 8.5f), new Vector3(11f, 182f, 0f), 54f, .45f);
    [SerializeField] private Camera targetCamera;
    private Coroutine move;
    private float sheetCoverage;
    // The backing's 36-unit radius plus its filtered edge, behind the UI only.
    private const float SheetUnderlap = 40f / 2400f;
    [SerializeField] private Color daylightBackground = new Color(.35f, .75f, .95f, 1f);
    public void Configure(Camera camera) { targetCamera = camera; daylightBackground = camera.backgroundColor; }
    public Preset GetPreset(CatLifeSessionPhase phase)
    {
        if (phase == CatLifeSessionPhase.Transition) return TransitionPreset;
        if (phase == CatLifeSessionPhase.Focus) return FocusPreset;
        if (phase == CatLifeSessionPhase.Reward) return RewardPreset;
        return HomePreset;
    }
    public void Show(CatLifeSessionPhase phase, bool immediate = false)
    {
        SetBottomSheetCoverage(phase == CatLifeSessionPhase.Reward ? 1104f / 2424f : 0f);
        targetCamera.backgroundColor = phase == CatLifeSessionPhase.Focus ? new Color32(64, 90, 121, 255) : daylightBackground;
        Preset preset = GetPreset(phase);
        if (move != null) StopCoroutine(move);
        if (immediate) { Apply(preset); return; }
        move = StartCoroutine(Tween(preset));
    }
    public void SetBottomSheetCoverage(float height)
    {
        sheetCoverage = height;
        float bottom = height == 0 ? 0 : height - SheetUnderlap;
        targetCamera.rect = new Rect(0f, bottom, 1f, 1f - bottom);
        UpdateSheetProjection();
    }
    private void UpdateSheetProjection()
    {
        Rect renderRect = targetCamera.rect;
        targetCamera.rect = new Rect(0, sheetCoverage, 1, 1 - sheetCoverage);
        targetCamera.ResetProjectionMatrix();
        Matrix4x4 projection = targetCamera.projectionMatrix;
        Rect frame = targetCamera.pixelRect;
        targetCamera.rect = renderRect;
        if (sheetCoverage == 0) return;
        Rect render = targetCamera.pixelRect;
        projection.m11 *= frame.height / render.height;
        projection.m12 -= (frame.y - render.y) / render.height;
        targetCamera.projectionMatrix = projection;
    }
    private IEnumerator Tween(Preset preset)
    {
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        float startFov = targetCamera.fieldOfView;
        for (float elapsed = 0f; elapsed < preset.Duration; elapsed += Time.unscaledDeltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / preset.Duration));
            transform.position = Vector3.Lerp(startPosition, preset.Position, t);
            transform.rotation = Quaternion.Slerp(startRotation, Quaternion.Euler(preset.Euler), t);
            targetCamera.fieldOfView = Mathf.Lerp(startFov, preset.Fov, t);
            UpdateSheetProjection();
            yield return null;
        }
        Apply(preset);
        move = null;
    }
    private void Apply(Preset preset) { transform.SetPositionAndRotation(preset.Position, Quaternion.Euler(preset.Euler)); targetCamera.fieldOfView = preset.Fov; UpdateSheetProjection(); }
}
