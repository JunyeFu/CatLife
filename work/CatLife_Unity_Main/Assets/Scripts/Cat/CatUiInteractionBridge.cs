using UnityEngine;
using UnityEngine.EventSystems;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatUiInteractionBridge : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private CatBehaviorDriver behaviorDriver;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private Transform catRoot;
        [SerializeField] private float hitRadiusPixels = 160f;

        public void Configure(CatBehaviorDriver driver, Camera camera, Transform cat)
        {
            behaviorDriver = driver;
            inputCamera = camera;
            catRoot = cat;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (behaviorDriver == null || inputCamera == null || catRoot == null || eventData == null) return;
            Renderer[] renderers = catRoot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 projected = inputCamera.WorldToScreenPoint(bounds.center);
            if (projected.z <= 0f) return;
            if (Vector2.Distance(eventData.position, new Vector2(projected.x, projected.y)) <= Mathf.Max(48f, hitRadiusPixels))
                behaviorDriver.NotifyCatTapped();
        }
    }
}
