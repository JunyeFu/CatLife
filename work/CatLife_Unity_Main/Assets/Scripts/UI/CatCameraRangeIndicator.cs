using CatLife.Cat;
using UnityEngine;
using UnityEngine.UI;

namespace CatLife.UI
{
    [DisallowMultipleComponent]
    public sealed class CatCameraRangeIndicator : MonoBehaviour
    {
        [SerializeField] private Transform catTarget;
        [SerializeField] private Camera targetCamera;
        [SerializeField] private CatDestinationPlanner destinationPlanner;
        [SerializeField] private RectTransform indicatorRoot;
        [SerializeField] private Text indicatorText;
        [SerializeField] private float sidePadding = 82f;
        [SerializeField] private float verticalOffset = 0f;

        private const string RightIndicatorText = "猫  ▶";
        private const string LeftIndicatorText = "◀  猫";
        private bool lastShownRight;

        public bool HasCoreReferences
        {
            get { return catTarget != null && ResolveCamera() != null && destinationPlanner != null; }
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureIndicator();
            SetVisible(false);
        }

        private void Update()
        {
            ResolveFrameReferences();
            EnsureIndicator();
            if (indicatorRoot == null || indicatorText == null || catTarget == null)
            {
                return;
            }

            Camera camera = ResolveCamera();
            if (camera == null)
            {
                SetVisible(false);
                return;
            }

            if (destinationPlanner != null && destinationPlanner.IsPointInPreferredCameraRange(catTarget.position))
            {
                SetVisible(false);
                return;
            }

            bool showRight = ShouldShowRight(camera);
            PositionIndicator(showRight);
            if (!indicatorRoot.gameObject.activeSelf || showRight != lastShownRight)
            {
                indicatorText.text = showRight ? RightIndicatorText : LeftIndicatorText;
                lastShownRight = showRight;
            }

            SetVisible(true);
        }

        public void Configure(Transform target, Camera camera, CatDestinationPlanner planner)
        {
            catTarget = target;
            targetCamera = camera;
            destinationPlanner = planner;
            ResolveReferences();
            EnsureIndicator();
        }

        private void ResolveReferences()
        {
            if (catTarget == null)
            {
                CatBehaviorDriver driver = FindAnyObjectByType<CatBehaviorDriver>();
                if (driver != null)
                {
                    catTarget = driver.transform;
                }
            }

            if (destinationPlanner == null && catTarget != null)
            {
                destinationPlanner = catTarget.GetComponent<CatDestinationPlanner>();
            }

            ResolveCamera();
        }

        private void ResolveFrameReferences()
        {
            if (destinationPlanner == null && catTarget != null)
            {
                destinationPlanner = catTarget.GetComponent<CatDestinationPlanner>();
            }

            ResolveCamera();
        }

        private Camera ResolveCamera()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            return targetCamera;
        }

        private bool ShouldShowRight(Camera camera)
        {
            Vector3 viewport = camera.WorldToViewportPoint(catTarget.position + Vector3.up * 0.28f);
            if (viewport.z < 0f)
            {
                Vector3 local = camera.transform.InverseTransformPoint(catTarget.position);
                return local.x > 0f;
            }

            return viewport.x >= 0.5f;
        }

        private void PositionIndicator(bool right)
        {
            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            indicatorRoot.anchorMin = right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            indicatorRoot.anchorMax = indicatorRoot.anchorMin;
            indicatorRoot.pivot = right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);
            indicatorRoot.anchoredPosition = new Vector2(right ? -sidePadding : sidePadding, verticalOffset);
        }

        private void SetVisible(bool visible)
        {
            if (indicatorRoot != null && indicatorRoot.gameObject.activeSelf != visible)
            {
                indicatorRoot.gameObject.SetActive(visible);
            }
        }

        private void EnsureIndicator()
        {
            if (indicatorRoot != null && indicatorText != null)
            {
                return;
            }

            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            GameObject root = new GameObject("CatCameraRangeIndicator", typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvasRect, false);
            indicatorRoot = root.GetComponent<RectTransform>();
            indicatorRoot.sizeDelta = new Vector2(132f, 96f);

            Image background = root.GetComponent<Image>();
            background.color = new Color(1f, 0.77f, 0.22f, 0.82f);
            background.raycastTarget = false;

            GameObject textObject = new GameObject("CatCameraRangeIndicatorText", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(root.transform, false);
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            indicatorText = textObject.GetComponent<Text>();
            indicatorText.font = font;
            indicatorText.fontSize = 30;
            indicatorText.fontStyle = FontStyle.Bold;
            indicatorText.alignment = TextAnchor.MiddleCenter;
            indicatorText.color = Color.white;
            indicatorText.raycastTarget = false;
        }
    }
}
