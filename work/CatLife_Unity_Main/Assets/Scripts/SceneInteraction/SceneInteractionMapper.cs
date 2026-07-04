using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
#endif

namespace CatLife.SceneInteraction
{
    [DisallowMultipleComponent]
    public sealed class SceneInteractionMapper : MonoBehaviour
    {
        [Serializable]
        public sealed class SceneInteractionPayloadEvent : UnityEvent<SceneInteractionPayload>
        {
        }

        [SerializeField] private Camera inputCamera;
        [SerializeField] private LayerMask interactionLayerMask = ~0;
        [SerializeField] private float maxRayDistance = 200f;
        [SerializeField] private bool enableMouseInput = true;
        [SerializeField] private bool enableTouchInput = true;
        [SerializeField] private SceneInteractionPayloadEvent onInteractionMapped = new SceneInteractionPayloadEvent();

        private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
        private SceneInteractionPayload lastPayload;
        private SceneInteractionPoint lastPoint;

        public SceneInteractionPayload LastPayload
        {
            get { return lastPayload; }
        }

        public SceneInteractionPoint LastPoint
        {
            get { return lastPoint; }
        }

        public event Action<SceneInteractionPayload, SceneInteractionPoint> InteractionMapped;

        private void Reset()
        {
            inputCamera = Camera.main;
        }

        private void Awake()
        {
            ResolveCamera();
        }

        private void Update()
        {
            if (ResolveCamera() == null)
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

        public void Configure(Camera camera, LayerMask layerMask)
        {
            inputCamera = camera != null ? camera : Camera.main;
            interactionLayerMask = layerMask;
        }

        public bool TryMapScreenPoint(
            Vector2 screenPosition,
            out SceneInteractionPayload payload,
            out SceneInteractionPoint point)
        {
            payload = default(SceneInteractionPayload);
            point = null;

            Camera camera = ResolveCamera();
            if (camera == null)
            {
                return false;
            }

            Ray ray = camera.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                hitBuffer,
                Mathf.Max(1f, maxRayDistance),
                interactionLayerMask,
                QueryTriggerInteraction.Collide);

            if (hitCount <= 0)
            {
                return false;
            }

            float bestDistance = float.MaxValue;
            Vector3 bestHitPosition = Vector3.zero;
            SceneInteractionPoint bestPoint = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = hitBuffer[i];
                if (hit.collider == null)
                {
                    continue;
                }

                SceneInteractionPoint candidate = hit.collider.GetComponentInParent<SceneInteractionPoint>();
                if (candidate == null || hit.distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = hit.distance;
                bestHitPosition = hit.point;
                bestPoint = candidate;
            }

            if (bestPoint == null)
            {
                return false;
            }

            point = bestPoint;
            payload = bestPoint.CreatePayload(bestHitPosition, Time.time);
            return payload.IsValid;
        }

        private void MapPointerDown(int pointerId, Vector2 screenPosition)
        {
            if (IsPointerOverUi(pointerId))
            {
                return;
            }

            SceneInteractionPayload payload;
            SceneInteractionPoint point;
            if (!TryMapScreenPoint(screenPosition, out payload, out point))
            {
                return;
            }

            Publish(payload, point);
        }

        private void Publish(SceneInteractionPayload payload, SceneInteractionPoint point)
        {
            lastPayload = payload;
            lastPoint = point;

            onInteractionMapped.Invoke(payload);
            Action<SceneInteractionPayload, SceneInteractionPoint> handler = InteractionMapped;
            if (handler != null)
            {
                handler(payload, point);
            }
        }

#if ENABLE_INPUT_SYSTEM
        private bool HandleInputSystemPointer()
        {
            if (enableTouchInput && Touchscreen.current != null)
            {
                TouchControl touch = Touchscreen.current.primaryTouch;
                if (touch.press.wasPressedThisFrame)
                {
                    MapPointerDown(-1, touch.position.ReadValue());
                    return true;
                }
            }

            if (enableMouseInput && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                MapPointerDown(-1, Mouse.current.position.ReadValue());
                return true;
            }

            return false;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        private void HandleMouse()
        {
            if (Input.GetMouseButtonDown(0))
            {
                MapPointerDown(-1, Input.mousePosition);
            }
        }

        private void HandleTouch(Touch touch)
        {
            if (touch.phase == TouchPhase.Began)
            {
                MapPointerDown(touch.fingerId, touch.position);
            }
        }
#endif

        private Camera ResolveCamera()
        {
            if (inputCamera == null)
            {
                inputCamera = Camera.main;
            }

            return inputCamera;
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
