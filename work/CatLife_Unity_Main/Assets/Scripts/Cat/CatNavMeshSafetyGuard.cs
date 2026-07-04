using UnityEngine;
using UnityEngine.AI;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CatNavMeshSafetyGuard : MonoBehaviour
    {
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private CatNavigationAgent navigationAgent;
        [SerializeField] private bool autoRecover = true;
        [SerializeField] private float navMeshProbeDistance = 0.9f;
        [SerializeField] private float maxSurfaceDrift = 0.35f;
        [SerializeField] private float stuckSeconds = 2.5f;
        [SerializeField] private float minProgressSpeed = 0.02f;

        private Vector3 previousPosition;
        private float stuckTimer;
        private int recoveryCount;
        private int stuckRecoveryCount;
        private int invalidPathRecoveryCount;
        private float lastNavMeshDistance;
        private string lastRecoveryReason = "none";
        private string lastPathStatus = "none";
        private bool hasLastSafePosition;
        private Vector3 lastSafeNavMeshPosition;

        public int RecoveryCount
        {
            get { return recoveryCount; }
        }

        public int StuckRecoveryCount
        {
            get { return stuckRecoveryCount; }
        }

        public int InvalidPathRecoveryCount
        {
            get { return invalidPathRecoveryCount; }
        }

        public float LastNavMeshDistance
        {
            get { return lastNavMeshDistance; }
        }

        public string LastRecoveryReason
        {
            get { return lastRecoveryReason; }
        }

        public string LastPathStatus
        {
            get { return lastPathStatus; }
        }

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
            navigationAgent = GetComponent<CatNavigationAgent>();
        }

        private void Awake()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (navigationAgent == null)
            {
                navigationAgent = GetComponent<CatNavigationAgent>();
            }

            previousPosition = transform.position;
            CacheSafePositionIfAvailable();
        }

        private void LateUpdate()
        {
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (!agent.isOnNavMesh)
            {
                TryRecoverToNearestNavMesh("off_navmesh");
                previousPosition = transform.position;
                return;
            }

            GuardSurfaceDrift();
            GuardPathStatus();
            GuardStuckMovement();
            previousPosition = transform.position;
        }

        public string BuildStatusLine()
        {
            return string.Format(
                "recoveries={0}; stuck={1}; invalidPath={2}; navMeshDistance={3:F3}; pathStatus={4}; last={5}",
                recoveryCount,
                stuckRecoveryCount,
                invalidPathRecoveryCount,
                lastNavMeshDistance,
                lastPathStatus,
                lastRecoveryReason);
        }

        private void GuardSurfaceDrift()
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, Mathf.Max(0.05f, navMeshProbeDistance), NavMesh.AllAreas))
            {
                TryRecoverToNearestNavMesh("navmesh_sample_miss");
                return;
            }

            lastNavMeshDistance = Vector3.Distance(transform.position, hit.position);
            if (lastNavMeshDistance > Mathf.Max(0.02f, maxSurfaceDrift))
            {
                RecoverByWarp(hit.position, "surface_drift");
                return;
            }

            lastSafeNavMeshPosition = hit.position;
            hasLastSafePosition = true;
        }

        private void GuardPathStatus()
        {
            if (agent.pathPending || !agent.hasPath)
            {
                lastPathStatus = "none";
                return;
            }

            lastPathStatus = agent.pathStatus.ToString();
            if (agent.pathStatus == NavMeshPathStatus.PathComplete)
            {
                return;
            }

            invalidPathRecoveryCount += 1;
            RecoverByStopping("invalid_path_" + agent.pathStatus);
        }

        private void GuardStuckMovement()
        {
            if (!agent.hasPath || agent.pathPending || agent.isStopped)
            {
                stuckTimer = 0f;
                return;
            }

            if (agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            {
                stuckTimer = 0f;
                return;
            }

            float minSpeed = Mathf.Max(0.001f, minProgressSpeed);
            if (agent.desiredVelocity.magnitude < minSpeed)
            {
                stuckTimer = 0f;
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float moved = Vector3.Distance(transform.position, previousPosition);
            float progressSpeed = moved / deltaTime;
            if (progressSpeed >= minSpeed || agent.velocity.magnitude >= minSpeed)
            {
                stuckTimer = 0f;
                return;
            }

            stuckTimer += Time.deltaTime;
            if (stuckTimer < Mathf.Max(0.1f, stuckSeconds))
            {
                return;
            }

            stuckTimer = 0f;
            stuckRecoveryCount += 1;
            RecoverByStopping("stuck_no_progress");
        }

        private void TryRecoverToNearestNavMesh(string reason)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(transform.position, out hit, Mathf.Max(0.05f, navMeshProbeDistance), NavMesh.AllAreas))
            {
                if (hasLastSafePosition)
                {
                    RecoverByWarp(lastSafeNavMeshPosition, reason + "_last_safe");
                }
                else
                {
                    lastRecoveryReason = reason + "_failed";
                }

                return;
            }

            RecoverByWarp(hit.position, reason);
        }

        private void CacheSafePositionIfAvailable()
        {
            NavMeshHit hit;
            if (NavMesh.SamplePosition(transform.position, out hit, Mathf.Max(0.05f, navMeshProbeDistance), NavMesh.AllAreas))
            {
                lastSafeNavMeshPosition = hit.position;
                hasLastSafePosition = true;
                lastNavMeshDistance = Vector3.Distance(transform.position, hit.position);
            }
        }

        private void RecoverByWarp(Vector3 position, string reason)
        {
            if (!autoRecover)
            {
                lastRecoveryReason = reason + "_blocked";
                return;
            }

            if (agent.Warp(position))
            {
                if (navigationAgent != null)
                {
                    navigationAgent.SyncVisualTransformToAgent();
                }

                recoveryCount += 1;
                lastRecoveryReason = reason;
            }
            else
            {
                lastRecoveryReason = reason + "_warp_failed";
            }
        }

        private void RecoverByStopping(string reason)
        {
            if (!autoRecover)
            {
                lastRecoveryReason = reason + "_blocked";
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.isStopped = true;
            recoveryCount += 1;
            lastRecoveryReason = reason;
        }
    }
}
