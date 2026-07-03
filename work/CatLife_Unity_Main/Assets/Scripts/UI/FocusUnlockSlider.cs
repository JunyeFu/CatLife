using UnityEngine;
using UnityEngine.EventSystems;

namespace CatLife.UI
{
    [DisallowMultipleComponent]
    public sealed class FocusUnlockSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        [SerializeField] private CatLifeHomeUiController homeUiController;
        [SerializeField] private RectTransform track;
        [SerializeField] private RectTransform handle;
        [SerializeField] private float unlockThreshold = 0.72f;

        private bool dragging;
        private float maxOffset;
        private Vector2 handleBasePosition;
        private bool hasHandleBasePosition;

        public void Configure(CatLifeHomeUiController controller, RectTransform sliderTrack, RectTransform sliderHandle, float threshold)
        {
            homeUiController = controller;
            track = sliderTrack;
            handle = sliderHandle;
            unlockThreshold = Mathf.Clamp01(threshold);
            CacheHandleBasePosition();
            ResetHandle();
        }

        private void OnDisable()
        {
            dragging = false;
            ResetHandle();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            dragging = true;
            UpdateDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (dragging)
            {
                UpdateDrag(eventData);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EndDrag();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EndDrag();
        }

        private void UpdateDrag(PointerEventData eventData)
        {
            if (track == null || handle == null)
            {
                return;
            }

            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out localPoint))
            {
                return;
            }

            float halfHandle = handle.rect.height * 0.5f;
            maxOffset = Mathf.Max(1f, track.rect.height - halfHandle);
            float offset = Mathf.Clamp(localPoint.y, 0f, maxOffset);
            CacheHandleBasePosition();
            Vector2 handlePosition = handleBasePosition;
            handlePosition.y += offset;
            handle.anchoredPosition = handlePosition;

            if (offset / maxOffset >= unlockThreshold)
            {
                Unlock();
            }
        }

        private void EndDrag()
        {
            dragging = false;
            ResetHandle();
        }

        private void Unlock()
        {
            dragging = false;
            ResetHandle();

            if (homeUiController != null)
            {
                homeUiController.UnlockFocusSession();
            }
        }

        private void ResetHandle()
        {
            if (handle == null)
            {
                return;
            }

            CacheHandleBasePosition();
            handle.anchoredPosition = handleBasePosition;
        }

        private void CacheHandleBasePosition()
        {
            if (!hasHandleBasePosition && handle != null)
            {
                handleBasePosition = handle.anchoredPosition;
                hasHandleBasePosition = true;
            }
        }
    }
}
