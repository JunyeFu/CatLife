using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CatLifeSwipeToEnd : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private RectTransform handle;
    [SerializeField] private Image fill;
    private RectTransform track;
    private float progress;
    public event Action ConfirmRequested;
    public event Action InteractionRecorded;
    public float Progress => progress;
    private void Awake() { track = (RectTransform)transform; ResetTrack(); }
    public void Configure(RectTransform sliderHandle, Image sliderFill) { handle = sliderHandle; fill = sliderFill; }
    public void OnPointerDown(PointerEventData eventData) { UpdateProgress(eventData); }
    public void OnDrag(PointerEventData eventData) { UpdateProgress(eventData); }
    public void OnPointerUp(PointerEventData eventData) { InteractionRecorded?.Invoke(); if (progress >= .65f) ConfirmRequested?.Invoke(); ResetTrack(); }
    private void UpdateProgress(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
        progress = Mathf.Clamp01((point.x + track.rect.width * .5f) / track.rect.width);
        Apply();
    }
    private void ResetTrack() { progress = 0f; Apply(); }
    private void Apply() { if (fill != null) fill.fillAmount = progress; if (handle != null) handle.anchorMin = handle.anchorMax = new Vector2(progress, .5f); }
}
