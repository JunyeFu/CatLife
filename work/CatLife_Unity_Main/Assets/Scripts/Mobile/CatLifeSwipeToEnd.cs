using System;
using CatLife.Mobile;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CatLifeSwipeToEnd : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform handle;
    [SerializeField] private Image fill;
    private RectTransform track;
    private readonly CatLifeExitGesture gesture = new CatLifeExitGesture();
    private bool dragging;
    private int activePointerId;
    private float pointerPosition;
    private bool interventionReported;
    private Func<bool> canIntervene;
    private float handleSize = 128;
    public event Action ConfirmRequested;
    public event Action InteractionRecorded;
    public event Action InterventionStarted;
    public event Action<CatLifeExitGesturePhase, float> GestureChanged;
    public event Action GestureCancelled;
    public float Progress => gesture.Progress;
    public CatLifeExitGesturePhase GesturePhase => gesture.Phase;
    private void Awake() { track = (RectTransform)transform; ResetTrack(); }
    public void Configure(RectTransform sliderHandle, Image sliderFill) { handle = sliderHandle; fill = sliderFill; }
    public void ConfigureIntervention(Func<bool> eligibility) { canIntervene = eligibility; }
    public void SetMinimumHandleSize(float minimum) { handleSize = Mathf.Max(128, minimum); if (handle != null) handle.sizeDelta = Vector2.one * handleSize; }
    private void Update()
    {
        if (!dragging) return;
        gesture.Step(pointerPosition, Time.unscaledTimeAsDouble);
        ReportIntervention();
        Apply();
    }
    public void OnPointerDown(PointerEventData eventData)
    {
        if (dragging || !isActiveAndEnabled) return;
        if (handle != null && !RectTransformUtility.RectangleContainsScreenPoint(handle, eventData.position, eventData.pressEventCamera)) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out Vector2 point);
        activePointerId = eventData.pointerId;
        dragging = true;
        interventionReported = false;
        pointerPosition = point.x / (track.rect.width * .84f);
        // The production view remains direct-slide until its layout opts in.
        gesture.Begin(pointerPosition, canIntervene != null && canIntervene(), false);
        Apply();
    }
    public void OnDrag(PointerEventData eventData)
    {
        if (dragging && eventData.pointerId == activePointerId) UpdateProgress(eventData);
    }
    public void OnPointerUp(PointerEventData eventData)
    {
        if (!dragging || eventData.pointerId != activePointerId) return;
        UpdateProgress(eventData);
        bool confirm = gesture.End();
        dragging = false;
        Apply();
        InteractionRecorded?.Invoke();
        if (confirm) ConfirmRequested?.Invoke();
    }
    private void OnDisable() { ResetTrack(); }
    private void OnApplicationPause(bool paused) { if (paused) ResetTrack(); }
    private void OnApplicationFocus(bool focused) { if (!focused) ResetTrack(); }
    private void UpdateProgress(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
        pointerPosition = point.x / (track.rect.width * .84f);
        gesture.Step(pointerPosition, Time.unscaledTimeAsDouble);
        ReportIntervention();
        Apply();
    }
    private void ReportIntervention()
    {
        if (interventionReported || !gesture.InterventionShown) return;
        interventionReported = true;
        InterventionStarted?.Invoke();
    }
    private void ResetTrack()
    {
        dragging = false; gesture.Cancel(); Apply(false);
        GestureCancelled?.Invoke();
    }
    private void Apply(bool notify = true)
    {
        if (notify) GestureChanged?.Invoke(gesture.Phase, Progress);
        if (fill != null) fill.fillAmount = Progress;
        if (handle == null) return;
        handle.anchorMin = handle.anchorMax = new Vector2(Mathf.Lerp(.08f, .92f, Progress), .5f);
        handle.sizeDelta = Vector2.one * handleSize;
        handle.anchoredPosition = Vector2.zero;
    }
}
