using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CatForbiddenZone : MonoBehaviour
    {
        public enum ZoneSourceKind
        {
            Collider,
            RendererBounds,
            ManualOverride
        }

        [SerializeField] private string sourceObjectName;
        [SerializeField] private ZoneSourceKind sourceKind = ZoneSourceKind.RendererBounds;
        [SerializeField] private float projectionScale = 1.05f;

        private BoxCollider boxCollider;

        public string SourceObjectName { get { return sourceObjectName; } }
        public ZoneSourceKind SourceKind { get { return sourceKind; } }
        public float ProjectionScale { get { return projectionScale; } }

        private void Reset()
        {
            EnsureCollider();
        }

        private void Awake()
        {
            EnsureCollider();
        }

        public bool ContainsProjectedPoint(Vector3 worldPoint, float extraRadius)
        {
            BoxCollider zoneCollider = EnsureCollider();
            Bounds bounds = zoneCollider.bounds;
            float radius = Mathf.Max(0f, extraRadius);
            return Mathf.Abs(worldPoint.x - bounds.center.x) <= bounds.extents.x + radius &&
                Mathf.Abs(worldPoint.z - bounds.center.z) <= bounds.extents.z + radius;
        }

        public void Configure(
            string sourceName,
            ZoneSourceKind kind,
            float scale,
            Vector3 worldCenter,
            Vector3 worldSize)
        {
            sourceObjectName = string.IsNullOrEmpty(sourceName) ? "unknown" : sourceName;
            sourceKind = kind;
            projectionScale = Mathf.Max(1f, scale);

            transform.position = worldCenter;
            transform.rotation = Quaternion.identity;
            transform.localScale = new Vector3(
                Mathf.Max(0.05f, worldSize.x),
                Mathf.Max(0.05f, worldSize.y),
                Mathf.Max(0.05f, worldSize.z));

            BoxCollider zoneCollider = EnsureCollider();
            zoneCollider.center = Vector3.zero;
            zoneCollider.size = Vector3.one;
            zoneCollider.isTrigger = false;
        }

        private BoxCollider EnsureCollider()
        {
            if (boxCollider == null)
            {
                boxCollider = GetComponent<BoxCollider>();
            }

            if (boxCollider == null)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();
            }

            return boxCollider;
        }
    }
}
