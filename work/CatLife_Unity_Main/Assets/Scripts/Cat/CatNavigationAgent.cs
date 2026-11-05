using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class CatNavigationAgent : MonoBehaviour
    {
        [Header("Agent")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private float freeRoamSpeed = 1.15f;
        [SerializeField] private float focusedRoamSpeed = 1.15f;
        [SerializeField] private float freeAcceleration = 6f;
        [SerializeField] private float focusedAcceleration = 3f;
        [SerializeField] private float freeStoppingDistance = 0.01f;
        [SerializeField] private float focusedStoppingDistance = 0.01f;
        [SerializeField] private Rigidbody physicsBody;
        [SerializeField] private bool drawDebugPath = true;

        private float speedMultiplier = 1f;
        private bool destinationRequested;
        private bool destinationReached;
        private Vector3 physicalDestination;
        private bool onFinalSegment;
        private bool hasArrivalHeading;
        private Quaternion arrivalHeading;
        private int destinationRequestedFrame;
        private Coroutine enableAgentRoutine;
        private CatDestinationPlanner destinationPlanner;
        private bool focused;
        public bool VisualSyncEnabled { get; set; } = true;

        [System.Serializable]
        public sealed class TurnStep
        {
            public string stateName;
            public float seconds;
            public AnimationCurve x, z, yaw;
        }
        // Assigned only in the isolated turn candidate until visual acceptance.
        [SerializeField] private TurnStep leftTurn;
        [SerializeField] private TurnStep rightTurn;
        private TurnStep activeTurn;
        private Animator turnAnimator;
        private CatAnimationController locomotion;
        private Vector3 turnOrigin;
        private Quaternion turnRotation;
        private float turnTime;
        private bool turnEntered;
        public bool IsTurning => activeTurn != null;

        public bool IsOnNavMesh
        {
            get { return agent != null && agent.enabled && agent.isOnNavMesh; }
        }

        public bool IsMoving
        {
            get { return IsOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f; }
        }

        public bool IsPathInProgress
        {
            get
            {
                if (!IsOnNavMesh || agent.isStopped || !destinationRequested) return false;
                if (agent.pathPending || Time.frameCount == destinationRequestedFrame) return true;
                if (onFinalSegment) return true;
                if (!agent.hasPath || agent.pathStatus != NavMeshPathStatus.PathComplete) return false;
                return agent.remainingDistance > agent.stoppingDistance || agent.velocity.sqrMagnitude > 0.01f;
            }
        }

        public float Speed01
        {
            get
            {
                if (!IsOnNavMesh || agent.speed <= 0.001f)
                {
                    return 0f;
                }

                Vector3 velocity = physicsBody != null ? physicsBody.linearVelocity : agent.velocity;
                return Mathf.Clamp01(new Vector2(velocity.x, velocity.z).magnitude / Mathf.Max(0.01f, agent.speed));
            }
        }

        public float RemainingDistance
        {
            get
            {
                if (!IsOnNavMesh || agent.pathPending || !agent.hasPath)
                {
                    return 0f;
                }

                return agent.remainingDistance;
            }
        }

        public string PathStatusText
        {
            get
            {
                if (!IsOnNavMesh)
                {
                    return "off_navmesh";
                }

                if (agent.pathPending)
                {
                    return "pending";
                }

                if (!agent.hasPath)
                {
                    return "none";
                }

                return agent.pathStatus.ToString();
            }
        }

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Awake()
        {
            turnAnimator = GetComponentInChildren<Animator>();
            locomotion = GetComponent<CatAnimationController>();
            destinationPlanner = GetComponent<CatDestinationPlanner>();
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (physicsBody == null)
            {
                physicsBody = GetComponent<Rigidbody>();
            }

            if (agent != null)
            {
                agent.updatePosition = false;
                agent.updateRotation = false;
                agent.autoBraking = false;
                if (!EnsureOnNavMesh())
                {
                    enableAgentRoutine = StartCoroutine(EnableAgentWhenNavMeshReady());
                }
            }
        }

        private IEnumerator EnableAgentWhenNavMeshReady()
        {
            while (isActiveAndEnabled && !EnsureOnNavMesh())
            {
                yield return null;
            }

            enableAgentRoutine = null;
        }

        public bool EnsureOnNavMesh(float maxDistance = 2f)
        {
            if (agent == null)
            {
                return false;
            }

            if (agent.enabled && agent.isOnNavMesh)
            {
                return true;
            }

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, maxDistance, NavMesh.AllAreas))
            {
                return false;
            }

            float bodyY = physicsBody != null ? physicsBody.position.y : transform.position.y;
            if (agent.enabled)
            {
                agent.enabled = false;
            }

            // Attaching is a teleport, not a deferred physics movement. Register the
            // agent on the sampled surface before restoring the gravity-owned body.
            Vector3 groundedPosition = new Vector3(hit.position.x, bodyY, hit.position.z);
            if (physicsBody != null) physicsBody.position = hit.position;
            transform.position = hit.position;
            agent.enabled = true;
            if (!agent.Warp(hit.position))
            {
                return false;
            }

            if (physicsBody != null) physicsBody.position = groundedPosition;
            else transform.position = groundedPosition;
            SyncVisualTransformToAgent();
            return true;
        }

        private void FixedUpdate()
        {
            if (!VisualSyncEnabled || agent == null || !agent.enabled || !agent.isOnNavMesh)
            {
                return;
            }

            if (activeTurn != null)
            {
                AdvanceTurn();
                return;
            }

            Vector3 direction = destinationRequested && !agent.isStopped ? agent.desiredVelocity : Vector3.zero;
            direction.y = 0f;
            float landingDistance = float.PositiveInfinity;
            if (destinationRequested && !agent.pathPending)
            {
                if (agent.hasPath && agent.path.corners.Length == 2) onFinalSegment = true;
                Vector3 toLanding = physicalDestination - (physicsBody != null ? physicsBody.position : transform.position);
                toLanding.y = 0f;
                landingDistance = toLanding.magnitude;
                // The Rigidbody owns movement. Do not let one physical step pass
                // the validated endpoint while the NavMesh simulation catches up.
                if (toLanding.magnitude <= agent.stoppingDistance)
                {
                    StopSoft();
                    destinationReached = true;
                    direction = Vector3.zero;
                }
                else if (onFinalSegment)
                {
                    // Steer the final segment from the physical position, not the
                    // NavMesh simulation's stale heading after a turn-in-place.
                    direction = toLanding.normalized * Mathf.Min(agent.speed,
                        toLanding.magnitude / Time.fixedDeltaTime);
                }
            }
            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion currentRotation = physicsBody != null ? physicsBody.rotation : transform.rotation;
                Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                if (hasArrivalHeading && onFinalSegment && landingDistance < .2f) targetRotation = arrivalHeading;
                float angle = Vector3.SignedAngle(currentRotation * Vector3.forward,
                    targetRotation * Vector3.forward, Vector3.up);
                if (Mathf.Abs(angle) >= 30f && !string.IsNullOrEmpty(leftTurn?.stateName))
                {
                    BeginTurn(angle < 0 ? leftTurn : rightTurn);
                    AdvanceTurn();
                    return;
                }
                float forwardAlignment = Mathf.Max(0f, Vector3.Dot(currentRotation * Vector3.forward, direction.normalized));
                SetPhysicsRotation(Quaternion.RotateTowards(currentRotation, targetRotation,
                    agent.angularSpeed * Time.fixedDeltaTime));
                direction *= forwardAlignment;
            }
            if (locomotion != null && locomotion.NavigationOwnsPose) EndTurn();
            if (physicsBody != null)
            {
                Vector3 velocity = physicsBody.linearVelocity;
                physicsBody.linearVelocity = new Vector3(direction.x, velocity.y, direction.z);
                agent.nextPosition = new Vector3(physicsBody.position.x, agent.nextPosition.y, physicsBody.position.z);
            }
            else
            {
                transform.position += direction * Time.fixedDeltaTime;
                agent.nextPosition = new Vector3(transform.position.x, agent.nextPosition.y, transform.position.z);
            }

        }

        private void BeginTurn(TurnStep step)
        {
            activeTurn = step;
            turnOrigin = physicsBody.position;
            turnRotation = physicsBody.rotation;
            turnTime = 0;
            turnEntered = locomotion.NavigationOwnsPose;
            locomotion.NavigationOwnsPose = true;
            turnAnimator.speed = turnEntered ? 0 : 1;
            if (turnEntered) turnAnimator.Play(step.stateName, 0, 0);
            else turnAnimator.CrossFadeInFixedTime(step.stateName, .12f, 0, 0);
        }

        private void AdvanceTurn()
        {
            Vector3 velocity = physicsBody.linearVelocity;
            physicsBody.linearVelocity = new Vector3(0, velocity.y, 0);
            if (!turnEntered)
            {
                if (turnAnimator.IsInTransition(0) ||
                    !turnAnimator.GetCurrentAnimatorStateInfo(0).IsName(activeTurn.stateName))
                {
                    agent.nextPosition = new Vector3(physicsBody.position.x, agent.nextPosition.y, physicsBody.position.z);
                    return;
                }
                turnEntered = true;
                turnAnimator.speed = 0;
            }
            float phase = Mathf.Min(1, turnTime / activeTurn.seconds);
            Vector3 target = turnOrigin + turnRotation * new Vector3(
                activeTurn.x.Evaluate(phase), 0, activeTurn.z.Evaluate(phase));
            target.y = physicsBody.position.y; // Gravity, not the authored schedule, owns height.
            physicsBody.MovePosition(target);
            physicsBody.MoveRotation(turnRotation * Quaternion.Euler(0, activeTurn.yaw.Evaluate(phase), 0));
            turnAnimator.Play(activeTurn.stateName, 0, phase);
            turnAnimator.Update(0);
            agent.nextPosition = new Vector3(target.x, agent.nextPosition.y, target.z);
            turnTime += Time.fixedDeltaTime;
            // Keep pose ownership until the next steering decision: consecutive
            // steps must not insert one frame of Walk between identical endpoints.
            if (phase >= 1) activeTurn = null;
        }

        private void EndTurn()
        {
            activeTurn = null;
            locomotion.NavigationOwnsPose = false;
            turnAnimator.speed = 1;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Clamp(multiplier, 0.1f, 3f);
        }

        public void Configure(bool focused)
        {
            this.focused = focused;
            if (agent == null)
            {
                return;
            }

            agent.speed = (focused ? focusedRoamSpeed : freeRoamSpeed) * speedMultiplier;
            agent.acceleration = focused ? focusedAcceleration : freeAcceleration;
            agent.stoppingDistance = focused ? focusedStoppingDistance : freeStoppingDistance;
        }

        public bool WarpToNearestNavMesh(float maxDistance = 2f)
        {
            if (agent == null || !agent.enabled)
            {
                return false;
            }

            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, maxDistance, NavMesh.AllAreas))
            {
                return false;
            }

            bool warped = agent.Warp(hit.position);
            if (warped)
            {
                SyncVisualTransformToAgent();
            }

            return warped;
        }

        public bool TryMoveTo(Vector3 target)
        {
            hasArrivalHeading = false;
            if (destinationPlanner != null && destinationPlanner.TryTakePlannedRoamingPath(target, out var path))
                return StartRoute(target, path);
            return MoveToValidatedPoint(target, false);
        }

        internal bool TryResolveRoamingRoute(Vector3 requested, out Vector3 landing, out NavMeshPath path)
        {
            if (!IsOnNavMesh) { landing = requested; path = null; return false; }
            // Root height is gravity-owned and differs from the NavMesh surface.
            // Project before the narrow footprint query, as session landing does.
            if (!NavMesh.SamplePosition(requested, out var projected, 2f, NavMesh.AllAreas))
            { landing = requested; path = null; return false; }
            return TrySupportedLanding(projected.position, out landing, out path, false, false);
        }

        private bool StartRoute(Vector3 landing, NavMeshPath path)
        {
            destinationReached = false;
            agent.isStopped = false;
            destinationRequested = agent.SetPath(path);
            physicalDestination = landing;
            onFinalSegment = false;
            destinationRequestedFrame = Time.frameCount;
            return destinationRequested;
        }

        public bool TryMoveToSessionPoint(Vector3 target, Quaternion? heading = null)
        {
            Configure(false);
            hasArrivalHeading = heading.HasValue;
            if (hasArrivalHeading) arrivalHeading = heading.Value;
            return MoveToValidatedPoint(target, true);
        }

        public bool TryFindSessionApproach(Vector3 anchor, Quaternion heading, out Vector3 approach)
        {
            Configure(false);
            hasArrivalHeading = false;
            // Select inside the current portrait view and supported road, rather
            // than assuming the desktop-tested .6m approach fits every viewport.
            for (int step = 12; step >= 4; step--)
                if (TryFindSupportedLanding(anchor - heading * Vector3.forward * (step * .05f), out approach, true))
                    return true;
            approach = anchor;
            return false;
        }

        private bool MoveToValidatedPoint(Vector3 target, bool exactLanding)
        {
            if (!IsOnNavMesh)
            {
                return false;
            }

            if (!TryFindSupportedLanding(target, out var landing, exactLanding)) return false;
            destinationReached = false;
            agent.isStopped = false;
            destinationRequested = agent.SetDestination(landing);
            physicalDestination = landing;
            onFinalSegment = false;
            destinationRequestedFrame = Time.frameCount;
            return destinationRequested;
        }

        // Metre-space footprint measured from the accepted v12 standing cat.
        // Reject a stop that leaves its paws straddling the raised road edge.
        private static readonly Vector3[] StandingFeet =
        {
            new Vector3(-.14f, 0, .45f), new Vector3(.14f, 0, .45f),
            new Vector3(-.25f, 0, -.33f), new Vector3(.25f, 0, -.33f)
        };

        private bool TryFindSupportedLanding(Vector3 requested, out Vector3 landing, bool exactLanding)
        {
            // Semantic anchors retain their authored XZ location; their old Y is
            // not the current physical road height. Project before foot testing.
            if (!NavMesh.SamplePosition(requested, out var projected, 2f, NavMesh.AllAreas))
            {
                landing = requested;
                return false;
            }
            requested = projected.position;
            // A deliberate session approach may be shorter than an ambient
            // roam, but must not silently substitute a nearby focus position.
            if (exactLanding) return TrySupportedLanding(requested, out landing, true);
            if (TrySupportedLanding(requested, out landing)) return true;
            for (int ring = 1; ring <= 6; ring++)
            for (int direction = 0; direction < 16; direction++)
            {
                float angle = direction * Mathf.PI / 8;
                Vector3 candidate = requested + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * (ring * .1f);
                if (TrySupportedLanding(candidate, out landing)) return true;
            }
            landing = requested;
            return false;
        }

        private bool TrySupportedLanding(Vector3 candidate, out Vector3 landing, bool allowShortMove = false)
            => TrySupportedLanding(candidate, out landing, out _, allowShortMove, true);

        private bool TrySupportedLanding(Vector3 candidate, out Vector3 landing, out NavMeshPath path,
            bool allowShortMove, bool useArrivalHeading)
        {
            landing = candidate;
            path = null;
            if (!NavMesh.SamplePosition(candidate, out var hit, .15f, NavMesh.AllAreas)) return false;
            if (destinationPlanner != null && !(allowShortMove
                ? destinationPlanner.IsSessionDestinationValid(hit.position, transform.position, focused)
                : destinationPlanner.IsDestinationValid(hit.position, transform.position, focused))) return false;
            path = new NavMeshPath();
            if (!agent.CalculatePath(hit.position, path) || path.status != NavMeshPathStatus.PathComplete) return false;
            var corners = path.corners;
            if (corners.Length < 2) return false;
            Vector3 direction = corners.Length >= 2 ? corners[corners.Length - 1] - corners[corners.Length - 2] : transform.forward;
            direction.y = 0;
            Quaternion heading = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : transform.rotation;
            if (useArrivalHeading && hasArrivalHeading) heading = arrivalHeading;
            float low = float.PositiveInfinity, high = float.NegativeInfinity;
            foreach (var foot in StandingFeet)
            {
                Vector3 origin = hit.position + heading * foot + Vector3.up * 2;
                var surfaces = Physics.RaycastAll(origin, Vector3.down, 5, ~0, QueryTriggerInteraction.Ignore);
                float nearest = float.PositiveInfinity;
                float height = 0;
                foreach (var surface in surfaces)
                {
                    string name = surface.collider.name;
                    bool ground = name == "CL_ENV_IslandBase_01" || name == "CL_ENV_IslandGrass_01" ||
                        name == "CL_ROAD_CentralGardenRing_01" || name == "CL_ROAD_PlazaPath_01" ||
                        name == "CL_ROAD_PlazaRing_01" || name == "CL_PROP_CenterStone_01";
                    if (!ground || surface.distance >= nearest) continue;
                    nearest = surface.distance;
                    height = surface.point.y;
                }
                if (float.IsPositiveInfinity(nearest)) return false;
                low = Mathf.Min(low, height);
                high = Mathf.Max(high, height);
            }
            if (high - low > .02f) return false;
            landing = hit.position;
            return true;
        }

        public void StopSoft()
        {
            destinationReached = false;
            if (locomotion != null && locomotion.NavigationOwnsPose) EndTurn();
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.isStopped = true;
            }

            destinationRequested = false;
            hasArrivalHeading = false;
        }

        public void SyncVisualTransformToAgent()
        {
            if (!VisualSyncEnabled || agent == null || !agent.enabled || agent.updatePosition || !agent.isOnNavMesh)
            {
                return;
            }

            Vector3 current = physicsBody != null ? physicsBody.position : transform.position;
            Vector3 next = agent.nextPosition;
            SetPhysicsPosition(new Vector3(next.x, current.y, next.z));
        }

        public bool HasArrived()
        {
            return destinationReached;
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawDebugPath || agent == null || agent.path == null)
            {
                return;
            }

            Vector3[] corners = agent.path.corners;
            if (corners == null || corners.Length < 2)
            {
                return;
            }

            Gizmos.color = Color.cyan;
            for (int i = 0; i < corners.Length - 1; i++)
            {
                Gizmos.DrawLine(corners[i] + Vector3.up * 0.05f, corners[i + 1] + Vector3.up * 0.05f);
            }

            if (agent.hasPath)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(agent.destination + Vector3.up * 0.05f, 0.08f);
            }
        }

        private void SetPhysicsPosition(Vector3 position)
        {
            if (physicsBody != null)
            {
                physicsBody.MovePosition(position);
                return;
            }

            transform.position = position;
        }

        private void SetPhysicsRotation(Quaternion rotation)
        {
            if (physicsBody != null)
            {
                physicsBody.MoveRotation(rotation);
                return;
            }

            transform.rotation = rotation;
        }
    }
}
