using UnityEngine;
using UnityEngine.AI;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class CatNavigationAgent : MonoBehaviour
    {
        [Header("Agent")]
        [SerializeField] private NavMeshAgent agent;
        [SerializeField] private float freeRoamSpeed = 1.15f;
        [SerializeField] private float focusedRoamSpeed = 0.55f;
        [SerializeField] private float freeAcceleration = 6f;
        [SerializeField] private float focusedAcceleration = 3f;
        [SerializeField] private float freeStoppingDistance = 0.14f;
        [SerializeField] private float focusedStoppingDistance = 0.22f;
        [SerializeField] private bool drawDebugPath = true;

        private float speedMultiplier = 1f;

        public bool IsOnNavMesh
        {
            get { return agent != null && agent.enabled && agent.isOnNavMesh; }
        }

        public bool IsMoving
        {
            get { return IsOnNavMesh && !agent.isStopped && agent.velocity.sqrMagnitude > 0.01f; }
        }

        public float Speed01
        {
            get
            {
                if (!IsOnNavMesh || agent.speed <= 0.001f)
                {
                    return 0f;
                }

                return Mathf.Clamp01(agent.velocity.magnitude / Mathf.Max(0.01f, agent.speed));
            }
        }

        private void Reset()
        {
            agent = GetComponent<NavMeshAgent>();
        }

        private void Awake()
        {
            if (agent == null)
            {
                agent = GetComponent<NavMeshAgent>();
            }

            if (agent != null)
            {
                agent.updatePosition = true;
                agent.updateRotation = true;
                agent.autoBraking = false;
            }
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            speedMultiplier = Mathf.Clamp(multiplier, 0.1f, 3f);
        }

        public void Configure(bool focused)
        {
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

            return agent.Warp(hit.position);
        }

        public bool TryMoveTo(Vector3 target)
        {
            if (!IsOnNavMesh)
            {
                return false;
            }

            agent.isStopped = false;
            return agent.SetDestination(target);
        }

        public void StopSoft()
        {
            if (agent == null || !agent.enabled)
            {
                return;
            }

            if (agent.isOnNavMesh)
            {
                agent.ResetPath();
            }

            agent.isStopped = true;
        }

        public bool HasArrived()
        {
            if (!IsOnNavMesh || agent.pathPending)
            {
                return false;
            }

            if (agent.remainingDistance > agent.stoppingDistance)
            {
                return false;
            }

            return !agent.hasPath || agent.velocity.sqrMagnitude < 0.01f;
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
    }
}
