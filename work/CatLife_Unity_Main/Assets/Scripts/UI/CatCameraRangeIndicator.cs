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
        [SerializeField] private Image indicatorBorder;
        [SerializeField] private Image indicatorFill;
        [SerializeField] private Image indicatorIcon;
        [SerializeField] private Sprite catIconSprite;
        [SerializeField] private float sidePadding = 22f;
        [SerializeField] private float verticalOffset = 0f;

        private bool lastShownRight;
        private static Sprite circleSprite;

        public bool HasCoreReferences
        {
            get { return catTarget != null && ResolveCamera() != null && destinationPlanner != null && indicatorIcon != null; }
        }

        private void Awake()
        {
            ResolveReferences();
            EnsureIndicator();
            SetVisible(false);
        }

        private void OnValidate()
        {
            if (!Application.isPlaying)
            {
                SetVisible(false);
            }
        }

        private void OnDisable()
        {
            SetVisible(false);
        }

        private void Update()
        {
            ResolveFrameReferences();
            EnsureIndicator();
            if (indicatorRoot == null || indicatorIcon == null || catTarget == null)
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
                lastShownRight = showRight;
            }

            SetVisible(true);
        }

        public void Configure(Transform target, Camera camera, CatDestinationPlanner planner)
        {
            catTarget = target;
            targetCamera = camera;
            destinationPlanner = planner;
            sidePadding = 22f;
            ResolveReferences();
            EnsureIndicator();
            if (!Application.isPlaying)
            {
                SetVisible(false);
            }
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
            if (indicatorRoot != null && indicatorBorder != null && indicatorFill != null && indicatorIcon != null)
            {
                RefreshIconSprite();
                return;
            }

            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            if (indicatorRoot == null)
            {
                GameObject root = new GameObject("CatCameraRangeIndicator", typeof(RectTransform), typeof(Image));
                root.transform.SetParent(canvasRect, false);
                indicatorRoot = root.GetComponent<RectTransform>();
            }

            indicatorRoot.sizeDelta = new Vector2(92f, 92f);
            indicatorBorder = indicatorRoot.GetComponent<Image>();
            if (indicatorBorder == null)
            {
                indicatorBorder = indicatorRoot.gameObject.AddComponent<Image>();
            }

            indicatorBorder.sprite = GetCircleSprite();
            indicatorBorder.color = new Color(1f, 0.92f, 0.38f, 0.96f);
            indicatorBorder.raycastTarget = false;

            indicatorFill = EnsureChildImage("CatCameraRangeIndicatorFill", indicatorRoot, Vector2.zero, new Vector2(78f, 78f));
            indicatorFill.sprite = GetCircleSprite();
            indicatorFill.color = new Color(1f, 0.56f, 0.10f, 0.92f);
            indicatorFill.raycastTarget = false;

            indicatorIcon = EnsureChildImage("CatCameraRangeIndicatorIcon", indicatorRoot, Vector2.zero, new Vector2(52f, 52f));
            indicatorIcon.color = Color.white;
            indicatorIcon.raycastTarget = false;
            RefreshIconSprite();
            HideLegacyTextChildren();
        }

        private Image EnsureChildImage(string childName, RectTransform parent, Vector2 position, Vector2 size)
        {
            Transform child = parent.Find(childName);
            GameObject childObject;
            if (child == null)
            {
                childObject = new GameObject(childName, typeof(RectTransform), typeof(Image));
                childObject.transform.SetParent(parent, false);
            }
            else
            {
                childObject = child.gameObject;
            }

            RectTransform rect = childObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = childObject.GetComponent<Image>();
            if (image == null)
            {
                image = childObject.AddComponent<Image>();
            }

            return image;
        }

        private void RefreshIconSprite()
        {
            if (indicatorIcon == null)
            {
                return;
            }

            if (catIconSprite == null)
            {
                catIconSprite = ResolveCatButtonIconSprite();
            }

            indicatorIcon.sprite = catIconSprite;
            indicatorIcon.enabled = catIconSprite != null;
        }

        private static Sprite ResolveCatButtonIconSprite()
        {
            Image[] images = Object.FindObjectsByType<Image>(FindObjectsInactive.Include);
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null &&
                    image.name == "Icon" &&
                    image.sprite != null &&
                    HasParentNamed(image.transform, "MenuGroup_猫咪"))
                {
                    return image.sprite;
                }
            }

            return null;
        }

        private static bool HasParentNamed(Transform transform, string parentName)
        {
            Transform current = transform;
            while (current != null)
            {
                if (current.name == parentName)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private void HideLegacyTextChildren()
        {
            if (indicatorRoot == null)
            {
                return;
            }

            Text[] textChildren = indicatorRoot.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < textChildren.Length; i++)
            {
                if (textChildren[i] != null)
                {
                    textChildren[i].gameObject.SetActive(false);
                }
            }
        }

        private static Sprite GetCircleSprite()
        {
            if (circleSprite != null)
            {
                return circleSprite;
            }

            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "cat_camera_indicator_circle";
            Color clear = new Color(1f, 1f, 1f, 0f);
            Color white = Color.white;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.48f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, distance <= radius ? white : clear);
                }
            }

            texture.Apply(false, true);
            circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            circleSprite.name = "CatCameraIndicatorCircleSprite";
            return circleSprite;
        }
    }
}
