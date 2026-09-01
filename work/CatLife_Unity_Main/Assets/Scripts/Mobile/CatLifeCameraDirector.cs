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

    public static Preset HomePreset => new Preset(new Vector3(.1f, 1.9f, 1.2f), new Vector3(6.654f, 182.601f, .362f), 80f, .45f);
    public static Preset TransitionPreset => new Preset(new Vector3(.1f, 1.82f, .85f), new Vector3(5.8f, 182.601f, .362f), 76f, 2f);
    public static Preset FocusPreset => new Preset(new Vector3(.1f, 1.55f, .65f), new Vector3(4.5f, 182.601f, .362f), 72f, .45f);
    public static Preset RewardPreset => new Preset(new Vector3(.1f, 1.72f, .8f), new Vector3(5.5f, 182.601f, .362f), 74f, .45f);
    [SerializeField] private Camera targetCamera;
    private Coroutine move;
    public void Configure(Camera camera) { targetCamera = camera; }
    public Preset GetPreset(CatLifeSessionPhase phase)
    {
        if (phase == CatLifeSessionPhase.Transition) return TransitionPreset;
        if (phase == CatLifeSessionPhase.Focus) return FocusPreset;
        if (phase == CatLifeSessionPhase.Reward) return RewardPreset;
        return HomePreset;
    }
    public void Show(CatLifeSessionPhase phase, bool immediate = false)
    {
        Preset preset = GetPreset(phase);
        if (move != null) StopCoroutine(move);
        if (immediate) { Apply(preset); return; }
        move = StartCoroutine(Tween(preset));
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
            yield return null;
        }
        Apply(preset);
        move = null;
    }
    private void Apply(Preset preset) { transform.SetPositionAndRotation(preset.Position, Quaternion.Euler(preset.Euler)); targetCamera.fieldOfView = preset.Fov; }
}
