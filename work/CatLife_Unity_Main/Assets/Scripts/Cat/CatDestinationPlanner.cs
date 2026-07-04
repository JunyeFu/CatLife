using System;
using UnityEngine;
using UnityEngine.AI;
using CatLife.Recognition;

namespace CatLife.Cat
{
    public sealed class CatDestinationPlanner : MonoBehaviour
    {
        [Serializable]
        public sealed class DestinationAnchor
        {
            public Transform point;
            public float nonFocusWeight = 1f;
            public float focusWeight = 1f;
        }

        [Header("Context")]
        [SerializeField] private Transform userAnchor;
        [SerializeField] private DestinationAnchor[] anchors;

        [Header("Sampling")]
        [SerializeField] private float nonFocusSampleRadius = 8f;
        [SerializeField] private float focusSampleRadius = 3.5f;
        [SerializeField] private float minMoveDistance = 1.2f;
        [SerializeField] private float minDistanceFromUserAnchorWhenFocused = 3f;
        [SerializeField] private int sampleAttempts = 16;

        [Header("Validation")]
        [SerializeField] private LayerMask blockerMask;
        [SerializeField] private float blockerCheckRadius = 0.3f;
        [SerializeField] private float navMeshProbeDistance = 1.5f;
        [SerializeField] private CatForbiddenZone[] forbiddenZones;
        [SerializeField] private float forbiddenPathSampleStep = 0.25f;

        private readonly Collider[] overlapBuffer = new Collider[12];

        public bool TryPlanNext(
            RecognitionSnapshot snapshot,
            CatBehaviorState behaviorState,
            Vector3 currentPosition,
            out Vector3 result)
        {
            bool focused = snapshot.IsFocused || behaviorState == CatBehaviorState.FocusedRoam;

            if (anchors != null && anchors.Length > 0 && TryAnchorPlan(focused, currentPosition, out result))
            {
                return true;
            }

            return TryRandomPlan(focused, currentPosition, out result);
        }

        private bool TryAnchorPlan(bool focused, Vector3 origin, out Vector3 result)
        {
            float total = 0f;
            for (int i = 0; i < anchors.Length; i++)
            {
                DestinationAnchor anchor = anchors[i];
                if (anchor == null || anchor.point == null)
                {
                    continue;
                }

                total += focused ? anchor.focusWeight : anchor.nonFocusWeight;
            }

            if (total <= 0f)
            {
                result = origin;
                return false;
            }

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < anchors.Length; i++)
            {
                DestinationAnchor anchor = anchors[i];
                if (anchor == null || anchor.point == null)
                {
                    continue;
                }

                float weight = focused ? anchor.focusWeight : anchor.nonFocusWeight;
                roll -= weight;
                if (roll > 0f)
                {
                    continue;
                }

                float radius = focused ? focusSampleRadius : nonFocusSampleRadius * 0.5f;
                if (TryRandomAroundCenter(anchor.point.position, origin, radius, focused, out result))
                {
                    return true;
                }
            }

            result = origin;
            return false;
        }

        private bool TryRandomPlan(bool focused, Vector3 origin, out Vector3 result)
        {
            float radius = focused ? focusSampleRadius : nonFocusSampleRadius;
            return TryRandomAroundCenter(origin, origin, radius, focused, out result);
        }

        private bool TryRandomAroundCenter(
            Vector3 center,
            Vector3 origin,
            float radius,
            bool focused,
            out Vector3 result)
        {
            for (int i = 0; i < sampleAttempts; i++)
            {
                Vector2 random = UnityEngine.Random.insideUnitCircle * Mathf.Max(0.2f, radius);
                Vector3 candidate = center + new Vector3(random.x, 0f, random.y);

                NavMeshHit hit;
                if (!NavMesh.SamplePosition(candidate, out hit, navMeshProbeDistance, NavMesh.AllAreas))
                {
                    continue;
                }

                if (!IsDestinationValid(hit.position, origin, focused))
                {
                    continue;
                }

                result = hit.position;
                return true;
            }

            result = origin;
            return false;
        }

        private bool IsDestinationValid(Vector3 point, Vector3 origin, bool focused)
        {
            if (Vector3.Distance(origin, point) < minMoveDistance)
            {
                return false;
            }

            if (focused && userAnchor != null &&
                Vector3.Distance(userAnchor.position, point) < minDistanceFromUserAnchorWhenFocused)
            {
                return false;
            }

            if (blockerMask.value != 0)
            {
                int hitCount = Physics.OverlapSphereNonAlloc(
                    point + Vector3.up * 0.25f,
                    blockerCheckRadius,
                    overlapBuffer,
                    blockerMask,
                    QueryTriggerInteraction.Ignore);

                if (hitCount > 0)
                {
                    return false;
                }
            }

            if (IsForbidden(point, blockerCheckRadius))
            {
                return false;
            }

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin, point, NavMesh.AllAreas, path))
            {
                return false;
            }

            return path.status == NavMeshPathStatus.PathComplete && IsPathClear(path, blockerCheckRadius);
        }

        private bool IsPathClear(NavMeshPath path, float radius)
        {
            if (path == null || path.corners == null || path.corners.Length == 0)
            {
                return false;
            }

            Vector3[] corners = path.corners;
            for (int i = 0; i < corners.Length; i++)
            {
                if (IsForbidden(corners[i], radius))
                {
                    return false;
                }
            }

            float step = Mathf.Max(0.05f, forbiddenPathSampleStep);
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Vector3 from = corners[i];
                Vector3 to = corners[i + 1];
                float distance = Vector3.Distance(from, to);
                if (distance <= step)
                {
                    continue;
                }

                int sampleCount = Mathf.CeilToInt(distance / step);
                for (int sample = 1; sample < sampleCount; sample++)
                {
                    Vector3 point = Vector3.Lerp(from, to, sample / (float)sampleCount);
                    if (IsForbidden(point, radius))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool IsForbidden(Vector3 point, float radius)
        {
            if (forbiddenZones == null || forbiddenZones.Length == 0)
            {
                return false;
            }

            for (int i = 0; i < forbiddenZones.Length; i++)
            {
                CatForbiddenZone zone = forbiddenZones[i];
                if (zone == null || !zone.isActiveAndEnabled)
                {
                    continue;
                }

                if (zone.ContainsProjectedPoint(point, radius))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
