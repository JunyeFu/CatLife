using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatTownWalker : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private string isWalkingParameter = "IsWalking";
        [SerializeField] private string walkStateName = "CL_CAT_SRC_Walk_60fps";
        [SerializeField] private string idleStateName = "CL_CAT_IdleBreath_v06_headsync_loop_108f";
        [SerializeField] private Vector2 xRange = new Vector2(-6.5f, 6.5f);
        [SerializeField] private Vector2 zRange = new Vector2(-12.5f, -4.0f);
        [SerializeField] private Vector2 localPatrolSize = new Vector2(2.6f, 1.6f);
        [SerializeField] private float groundY = 0.03f;
        [SerializeField] private float walkSpeed = 1.15f;
        [SerializeField] private float turnSpeed = 5.5f;
        [SerializeField] private Vector2 waitSecondsRange = new Vector2(0.05f, 0.18f);
        [SerializeField] private float initialIdleSeconds = 0.45f;
        [SerializeField] private float targetMinDistance = 0.45f;
        [SerializeField] private float targetTolerance = 0.08f;
        [SerializeField] private float maxMovementDeltaTime = 0.05f;
        [SerializeField] private float walkTransitionSeconds = 0.12f;
        [SerializeField] private float idleTransitionSeconds = 0.16f;
        [SerializeField] private bool startWalkingOnEnable = true;
        [SerializeField] private bool useCurrentPositionAsPatrolCenter = true;
        [SerializeField] private bool keepSkinnedMeshVisibleWhileAnimated = true;
        [SerializeField] private Bounds skinnedMeshLocalBounds = new Bounds(new Vector3(0f, 0.18f, 0f), new Vector3(1.2f, 1.2f, 1.2f));

        private Vector3 targetPosition;
        private Vector3 patrolCenter;
        private Vector2 runtimeXRange;
        private Vector2 runtimeZRange;
        private float waitUntil;
        private bool isWalking;
        private int isWalkingParameterHash;
        private bool hasWalkingParameter;

        private void Awake()
        {
            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>(true);
            }

            if (animator != null)
            {
                animator.applyRootMotion = false;
            }

            ConfigureAnimatedRenderers();
            CacheAnimatorBindings();
        }

        private void OnEnable()
        {
            SnapToGround(false);
            BuildRuntimeRanges();
            ConfigureAnimatedRenderers();
            CacheAnimatorBindings();
            targetPosition = transform.position;

            if (startWalkingOnEnable)
            {
                waitUntil = Time.time + Mathf.Max(0f, initialIdleSeconds);
                SetWalking(false);
            }
            else
            {
                waitUntil = Time.time + Random.Range(waitSecondsRange.x, waitSecondsRange.y);
                SetWalking(false);
            }
        }

        private void Update()
        {
            if (!isWalking)
            {
                if (Time.time >= waitUntil)
                {
                    PickTarget();
                    SetWalking(true);
                }

                return;
            }

            Vector3 current = transform.position;
            Vector3 toTarget = targetPosition - current;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude <= targetTolerance * targetTolerance)
            {
                waitUntil = Time.time + Random.Range(waitSecondsRange.x, waitSecondsRange.y);
                SetWalking(false);
                return;
            }

            ApplyWalkingAnimator(true);

            Vector3 direction = toTarget.normalized;
            Quaternion lookRotation = Quaternion.LookRotation(direction, Vector3.up);
            float deltaTime = Mathf.Min(Time.deltaTime, Mathf.Max(0.001f, maxMovementDeltaTime));
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation, turnSpeed * deltaTime);

            Vector3 next = Vector3.MoveTowards(current, targetPosition, walkSpeed * deltaTime);
            next.y = groundY;
            transform.position = next;
        }

        public void Recenter(Vector3 center, Vector2 size)
        {
            xRange = new Vector2(center.x - size.x * 0.5f, center.x + size.x * 0.5f);
            zRange = new Vector2(center.z - size.y * 0.5f, center.z + size.y * 0.5f);
            localPatrolSize = size;
            patrolCenter = new Vector3(center.x, groundY, center.z);
            BuildRuntimeRanges();
            SnapToGround(true);
            targetPosition = transform.position;
        }

        private void BuildRuntimeRanges()
        {
            Vector3 current = transform.position;
            patrolCenter = useCurrentPositionAsPatrolCenter
                ? new Vector3(current.x, groundY, current.z)
                : new Vector3((xRange.x + xRange.y) * 0.5f, groundY, (zRange.x + zRange.y) * 0.5f);

            Vector2 patrolSize = new Vector2(Mathf.Max(0.1f, localPatrolSize.x), Mathf.Max(0.1f, localPatrolSize.y));
            runtimeXRange = ClampRangeToBounds(
                new Vector2(patrolCenter.x - patrolSize.x * 0.5f, patrolCenter.x + patrolSize.x * 0.5f),
                xRange);
            runtimeZRange = ClampRangeToBounds(
                new Vector2(patrolCenter.z - patrolSize.y * 0.5f, patrolCenter.z + patrolSize.y * 0.5f),
                zRange);
        }

        private void PickTarget()
        {
            Vector3 current = transform.position;
            Vector3 candidate = current;
            float minDistanceSqr = Mathf.Max(0f, targetMinDistance * targetMinDistance);

            for (int i = 0; i < 8; i++)
            {
                candidate = new Vector3(
                    Random.Range(runtimeXRange.x, runtimeXRange.y),
                    groundY,
                    Random.Range(runtimeZRange.x, runtimeZRange.y));

                Vector3 delta = candidate - current;
                delta.y = 0f;
                if (delta.sqrMagnitude >= minDistanceSqr)
                {
                    targetPosition = candidate;
                    return;
                }
            }

            float fallbackX = Mathf.Approximately(current.x, runtimeXRange.x) ? runtimeXRange.y : runtimeXRange.x;
            targetPosition = new Vector3(fallbackX, groundY, Mathf.Clamp(current.z, runtimeZRange.x, runtimeZRange.y));
        }

        private void SnapToGround(bool clampHorizontal)
        {
            Vector3 position = transform.position;
            if (clampHorizontal)
            {
                position.x = Mathf.Clamp(position.x, runtimeXRange.x, runtimeXRange.y);
                position.z = Mathf.Clamp(position.z, runtimeZRange.x, runtimeZRange.y);
            }

            position.y = groundY;
            transform.position = position;
        }

        private void SetWalking(bool walking)
        {
            isWalking = walking;
            ApplyWalkingAnimator(walking);
        }

        private void CacheAnimatorBindings()
        {
            if (animator == null)
            {
                hasWalkingParameter = false;
                return;
            }

            isWalkingParameterHash = Animator.StringToHash(isWalkingParameter);
            hasWalkingParameter = false;
            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Bool && parameters[i].nameHash == isWalkingParameterHash)
                {
                    hasWalkingParameter = true;
                    break;
                }
            }
        }

        private void ConfigureAnimatedRenderers()
        {
            if (!keepSkinnedMeshVisibleWhileAnimated)
            {
                return;
            }

            SkinnedMeshRenderer[] skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < skinnedRenderers.Length; i++)
            {
                SkinnedMeshRenderer skinnedRenderer = skinnedRenderers[i];
                skinnedRenderer.updateWhenOffscreen = true;
                if (skinnedMeshLocalBounds.size.sqrMagnitude > 0.001f)
                {
                    skinnedRenderer.localBounds = skinnedMeshLocalBounds;
                }
            }
        }

        private void ApplyWalkingAnimator(bool walking)
        {
            if (animator == null)
            {
                return;
            }

            animator.applyRootMotion = false;
            if (hasWalkingParameter)
            {
                animator.SetBool(isWalkingParameterHash, walking);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.62f, 0.1f, 0.75f);
            Vector3 center = Application.isPlaying ? patrolCenter : transform.position;
            Vector3 size = new Vector3(Mathf.Max(0.1f, localPatrolSize.x), 0.05f, Mathf.Max(0.1f, localPatrolSize.y));
            Gizmos.DrawWireCube(center, size);
        }

        private static Vector2 ClampRangeToBounds(Vector2 range, Vector2 bounds)
        {
            float min = Mathf.Min(bounds.x, bounds.y);
            float max = Mathf.Max(bounds.x, bounds.y);
            float rangeSize = Mathf.Abs(range.y - range.x);

            if (rangeSize >= max - min)
            {
                return new Vector2(min, max);
            }

            float clampedMin = Mathf.Clamp(Mathf.Min(range.x, range.y), min, max - rangeSize);
            return new Vector2(clampedMin, clampedMin + rangeSize);
        }
    }
}
