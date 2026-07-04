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
        [SerializeField] private Camera planningCamera;
        [SerializeField] private CatInterestPointRegistry interestPointRegistry;
        [SerializeField] private CatNeedModel needModel;
        [SerializeField] private CatBehaviorMemory behaviorMemory;
        [SerializeField] private DestinationAnchor[] anchors;

        [Header("Sampling")]
        [SerializeField] private float nonFocusSampleRadius = 8f;
        [SerializeField] private float focusSampleRadius = 3.5f;
        [SerializeField] private float minMoveDistance = 1.2f;
        [SerializeField] private float minDistanceFromUserAnchorWhenFocused = 3f;
        [SerializeField] private int sampleAttempts = 16;

        [Header("Camera Preference")]
        [SerializeField] private bool preferCameraRangeWhenNonFocused = true;
        [SerializeField] private bool preferCameraRangeWhenFocused = true;
        [SerializeField] private float viewportSafeMargin = 0.08f;
        [SerializeField] private float viewportProbeHeight = 0.28f;
        [SerializeField] private float cameraVisibleBiasWeight = 4f;
        [SerializeField] private float cameraReturnBiasWeight = 10f;
        [SerializeField] private float nonFocusNearCameraBiasWeight = 2.5f;
        [SerializeField] private float focusFarCameraBiasWeight = 3.5f;

        [Header("Validation")]
        [SerializeField] private LayerMask blockerMask;
        [SerializeField] private float blockerCheckRadius = 0.3f;
        [SerializeField] private float navMeshProbeDistance = 1.5f;
        [SerializeField] private CatForbiddenZone[] forbiddenZones;
        [SerializeField] private float forbiddenPathSampleStep = 0.25f;

        private readonly Collider[] overlapBuffer = new Collider[12];
        private string lastPlannedInterestPointId = "";

        public string LastPlannedInterestPointId
        {
            get { return lastPlannedInterestPointId; }
        }

        public bool IsPointInPreferredCameraRange(Vector3 worldPoint)
        {
            Camera camera = ResolvePlanningCamera();
            if (camera == null)
            {
                return false;
            }

            return IsViewportInside(camera.WorldToViewportPoint(worldPoint + Vector3.up * viewportProbeHeight), viewportSafeMargin);
        }

        public bool TryPlanNext(
            RecognitionSnapshot snapshot,
            CatBehaviorState behaviorState,
            Vector3 currentPosition,
            out Vector3 result)
        {
            CatBehaviorDecision decision = CatBehaviorDecision.Create(
                behaviorState,
                0f,
                0f,
                0,
                CatActionInterruptPolicy.DropIfBusy,
                false,
                "legacy_plan");
            return TryPlanNext(
                snapshot,
                decision,
                needModel != null ? needModel.Current : CatNeedState.CreateDefault(),
                behaviorMemory,
                currentPosition,
                out result);
        }

        public bool TryPlanNext(
            RecognitionSnapshot snapshot,
            CatBehaviorDecision decision,
            CatNeedState needs,
            CatBehaviorMemory memory,
            Vector3 currentPosition,
            out Vector3 result)
        {
            bool focused = snapshot.IsFocused || decision.state == CatBehaviorState.FocusedRoam;
            lastPlannedInterestPointId = "";

            if (focused && ShouldUseCameraPreference(true) && TryCameraReturnPlan(true, currentPosition, out result))
            {
                return true;
            }

            if (!focused && ShouldReturnToCamera(false, currentPosition) && TryCameraReturnPlan(false, currentPosition, out result))
            {
                return true;
            }

            Vector3 plannedResult;
            if (TryInterestPointPlan(snapshot, decision, needs, memory, currentPosition, out plannedResult))
            {
                if (!focused &&
                    ShouldUseCameraPreference(false) &&
                    !IsPointInPreferredCameraRange(plannedResult) &&
                    TryCameraReturnPlan(false, currentPosition, out result))
                {
                    return true;
                }

                result = plannedResult;
                return true;
            }

            if (anchors != null && anchors.Length > 0 && TryAnchorPlan(focused, currentPosition, out result))
            {
                return true;
            }

            return TryRandomPlan(focused, currentPosition, out result);
        }

        public bool TryPlanRequestedPoint(
            RecognitionSnapshot snapshot,
            Vector3 requestedPoint,
            Vector3 currentPosition,
            out Vector3 result)
        {
            lastPlannedInterestPointId = "";

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(requestedPoint, out hit, navMeshProbeDistance, NavMesh.AllAreas))
            {
                result = currentPosition;
                return false;
            }

            bool focused = snapshot.IsFocused;
            if (!IsDestinationValid(hit.position, currentPosition, focused))
            {
                result = currentPosition;
                return false;
            }

            result = hit.position;
            return true;
        }

        private bool TryInterestPointPlan(
            RecognitionSnapshot snapshot,
            CatBehaviorDecision decision,
            CatNeedState needs,
            CatBehaviorMemory memory,
            Vector3 origin,
            out Vector3 result)
        {
            if (interestPointRegistry == null || !decision.IsLocomotion)
            {
                result = origin;
                return false;
            }

            CatInterestPoint point;
            if (!interestPointRegistry.TryPickPoint(snapshot, decision, needs, memory, origin, out point) || point == null)
            {
                result = origin;
                return false;
            }

            bool focused = snapshot.IsFocused || decision.state == CatBehaviorState.FocusedRoam;
            if (TryRandomAroundCenter(point.transform.position, origin, point.SampleRadius, focused, out result))
            {
                lastPlannedInterestPointId = point.InterestId;
                return true;
            }

            result = origin;
            return false;
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

        private bool ShouldReturnToCamera(bool focused, Vector3 origin)
        {
            return ShouldUseCameraPreference(focused) &&
                ResolvePlanningCamera() != null &&
                !IsPointInPreferredCameraRange(origin);
        }

        private bool TryCameraReturnPlan(bool focused, Vector3 origin, out Vector3 result)
        {
            if (anchors != null)
            {
                for (int i = 0; i < anchors.Length; i++)
                {
                    DestinationAnchor anchor = anchors[i];
                    if (anchor == null || anchor.point == null)
                    {
                        continue;
                    }

                    float weight = focused ? anchor.focusWeight : anchor.nonFocusWeight;
                    if (weight <= 0f)
                    {
                        continue;
                    }

                    float radius = focused ? nonFocusSampleRadius * 0.75f : nonFocusSampleRadius * 0.5f;
                    if (TryRandomAroundCenter(anchor.point.position, origin, radius, focused, out result) &&
                        IsPointInPreferredCameraRange(result))
                    {
                        return true;
                    }
                }
            }

            float fallbackRadius = focused ? nonFocusSampleRadius : nonFocusSampleRadius;
            if (TryRandomAroundCenter(origin, origin, fallbackRadius, focused, out result) &&
                IsPointInPreferredCameraRange(result))
            {
                return true;
            }

            result = origin;
            return false;
        }

        private bool TryRandomAroundCenter(
            Vector3 center,
            Vector3 origin,
            float radius,
            bool focused,
            out Vector3 result)
        {
            bool useCameraPreference = ShouldUseCameraPreference(focused);
            bool originInCameraRange = !useCameraPreference || IsPointInPreferredCameraRange(origin);
            Vector3 bestResult = origin;
            float bestScore = float.NegativeInfinity;
            bool hasBestResult = false;

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

                if (!useCameraPreference)
                {
                    result = hit.position;
                    return true;
                }

                float score = ScoreCameraPreferredDestination(hit.position, originInCameraRange, focused);
                score += UnityEngine.Random.value * 0.05f;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestResult = hit.position;
                    hasBestResult = true;
                }
            }

            result = hasBestResult ? bestResult : origin;
            return hasBestResult;
        }

        private bool ShouldUseCameraPreference(bool focused)
        {
            return focused ? preferCameraRangeWhenFocused : preferCameraRangeWhenNonFocused;
        }

        private float ScoreCameraPreferredDestination(Vector3 point, bool originInCameraRange, bool focused)
        {
            Camera camera = ResolvePlanningCamera();
            if (camera == null)
            {
                return 0f;
            }

            Vector3 viewport = camera.WorldToViewportPoint(point + Vector3.up * viewportProbeHeight);
            if (viewport.z <= 0f)
            {
                return -cameraReturnBiasWeight;
            }

            float centerDistance = Vector2.Distance(new Vector2(viewport.x, viewport.y), new Vector2(0.5f, 0.5f));
            float centeredScore = Mathf.Clamp01(1f - centerDistance * 2f);
            float score = centeredScore;
            float cameraDistance = Vector3.Distance(camera.transform.position, point);

            if (IsViewportInside(viewport, viewportSafeMargin))
            {
                score += cameraVisibleBiasWeight;
                score += focused
                    ? Mathf.InverseLerp(3f, 10f, cameraDistance) * focusFarCameraBiasWeight
                    : (1f - Mathf.InverseLerp(1.5f, 8f, cameraDistance)) * nonFocusNearCameraBiasWeight;

                if (!originInCameraRange)
                {
                    score += cameraReturnBiasWeight;
                }
            }
            else if (!originInCameraRange)
            {
                float outsideX = Mathf.Max(0f, -viewport.x, viewport.x - 1f);
                float outsideY = Mathf.Max(0f, -viewport.y, viewport.y - 1f);
                float outsideDistance = outsideX + outsideY;
                score += Mathf.Clamp01(1f - outsideDistance) * cameraReturnBiasWeight * 0.35f;
            }

            return score;
        }

        private Camera ResolvePlanningCamera()
        {
            if (planningCamera == null)
            {
                planningCamera = Camera.main;
            }

            return planningCamera;
        }

        private static bool IsViewportInside(Vector3 viewport, float margin)
        {
            float safeMargin = Mathf.Clamp(margin, 0f, 0.45f);
            return viewport.z > 0f &&
                viewport.x >= safeMargin &&
                viewport.x <= 1f - safeMargin &&
                viewport.y >= safeMargin &&
                viewport.y <= 1f - safeMargin;
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
