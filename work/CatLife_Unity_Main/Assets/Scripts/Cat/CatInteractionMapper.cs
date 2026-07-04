using UnityEngine;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatInteractionMapper : MonoBehaviour
    {
        [SerializeField] private CatBehaviorDriver behaviorDriver;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private Transform catRoot;
        [SerializeField] private LayerMask raycastMask = ~0;
        [SerializeField] private float maxRayDistance = 200f;
        [SerializeField] private float longPressSeconds = 0.6f;
        [SerializeField] private bool enableMouseInput = true;
        [SerializeField] private bool enableTouchInput = true;

        private readonly RaycastHit[] hitBuffer = new RaycastHit[24];
        private bool pressActive;
        private bool pressStartedOnCat;
        private int activePointerId = -999;
        private float pressStartedAt;
        private Vector2 pressScreenPosition;

        private void Reset()
        {
            behaviorDriver = GetComponent<CatBehaviorDriver>();
            catRoot = transform;
            inputCamera = Camera.main;
        }

        private void Awake()
        {
            if (behaviorDriver == null)
            {
                behaviorDriver = GetComponent<CatBehaviorDriver>();
            }

            if (catRoot == null)
            {
                catRoot = transform;
            }

            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }
        }

        private void Update()
        {
            if (behaviorDriver == null || inputCamera == null)
            {
                return;
            }

#if ENABLE_INPUT_SYSTEM
            if (HandleInputSystemPointer())
            {
                return;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (enableTouchInput && Input.touchCount > 0)
            {
                HandleTouch(Input.GetTouch(0));
                return;
            }

            if (enableMouseInput)
            {
                HandleMouse();
            }
#endif
        }

        public void Configure(CatBehaviorDriver driver, Camera camera, Transform cat)
        {
            behaviorDriver = driver;
            inputCamera = camera;
            catRoot = cat != null ? cat : transform;
        }

#if ENABLE_INPUT_SYSTEM
        private bool HandleInputSystemPointer()
        {
            if (enableTouchInput && Touchscreen.current != null)
            {
                TouchControl touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    BeginPress(-1, touch.position.ReadValue());
                    return true;
                }

                if (touch.press.wasReleasedThisFrame)
                {
                    EndPress(-1, touch.position.ReadValue());
                    return true;
                }
            }

            if (enableMouseInput && Mouse.current != null)
            {
                if (Mouse.current.leftButton.wasPressedThisFrame)
                {
                    BeginPress(-1, Mouse.current.position.ReadValue());
                    return true;
                }

                if (Mouse.current.leftButton.wasReleasedThisFrame)
                {
                    EndPress(-1, Mouse.current.position.ReadValue());
                    return true;
                }
            }

            return false;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                BeginPress(-1, Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                EndPress(-1, Input.mousePosition);
            }
        }

        private void HandleTouch(Touch touch)
        {
            if (touch.phase == TouchPhase.Began)
            {
                BeginPress(touch.fingerId, touch.position);
            }
            else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                EndPress(touch.fingerId, touch.position);
            }
        }
#endif

        private void BeginPress(int pointerId, Vector2 screenPosition)
        {
            if (IsPointerOverUi(pointerId))
            {
                pressActive = false;
                return;
            }

            pressActive = true;
            activePointerId = pointerId;
            pressStartedAt = Time.unscaledTime;
            pressScreenPosition = screenPosition;
            pressStartedOnCat = TryRaycast(screenPosition, out RaycastHit hit) && IsCatHit(hit);
        }

        private void EndPress(int pointerId, Vector2 screenPosition)
        {
            if (!pressActive || pointerId != activePointerId)
            {
                return;
            }

            pressActive = false;
            if (IsPointerOverUi(pointerId))
            {
                return;
            }

            float pressSeconds = Time.unscaledTime - pressStartedAt;
            float dragPixels = Vector2.Distance(pressScreenPosition, screenPosition);
            if (dragPixels > 48f)
            {
                return;
            }

            RaycastHit hit;
            bool hasHit = TryRaycast(screenPosition, out hit);
            if (pressStartedOnCat && hasHit && IsCatHit(hit))
            {
                if (pressSeconds >= Mathf.Max(0.1f, longPressSeconds))
                {
                    behaviorDriver.NotifyCatLongPressed();
                }
                else
                {
                    behaviorDriver.NotifyCatTapped();
                }

                return;
            }

            if (hasHit)
            {
                behaviorDriver.NotifyGroundTapped(hit.point);
            }
        }

        private bool TryRaycast(Vector2 screenPosition, out RaycastHit bestHit)
        {
            bestHit = default(RaycastHit);
            Ray ray = inputCamera.ScreenPointToRay(screenPosition);
            int count = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                Mathf.Max(1f, maxRayDistance),
                raycastMask,
                QueryTriggerInteraction.Collide);

            if (count <= 0)
            {
                return false;
            }

            int bestIndex = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (hitBuffer[i].collider == null)
                {
                    continue;
                }

                if (hitBuffer[i].distance < bestDistance)
                {
                    bestDistance = hitBuffer[i].distance;
                    bestIndex = i;
                }
            }

            if (bestIndex < 0)
            {
                return false;
            }

            bestHit = hitBuffer[bestIndex];
            return true;
        }

        private bool IsCatHit(RaycastHit hit)
        {
            if (catRoot == null || hit.collider == null)
            {
                return false;
            }

            Transform current = hit.collider.transform;
            while (current != null)
            {
                if (current == catRoot)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static bool IsPointerOverUi(int pointerId)
        {
            if (EventSystem.current == null)
            {
                return false;
            }

            return pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
        }
    }
}
