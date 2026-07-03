using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatAnimationController : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private float locomotionFade = 0.12f;
        [SerializeField] private float actionFade = 0.1f;

        [Header("Animator Parameters")]
        [SerializeField] private string moveSpeed01Parameter = "MoveSpeed01";
        [SerializeField] private string isMovingParameter = "IsMoving";
        [SerializeField] private string inFocusModeParameter = "InFocusMode";
        [SerializeField] private string arousal01Parameter = "Arousal01";
        [SerializeField] private string legacyIsWalkingParameter = "IsWalking";

        [Header("State Names")]
        [SerializeField] private string idleStateName = "CL_CAT_IdleBreath_v06_headsync_loop_108f";
        [SerializeField] private string walkStateName = "CL_CAT_SRC_Walk_60fps";
        [SerializeField] private string alertLookStateName = "CL_CAT_AlertLook_v01_loop_120f";
        [SerializeField] private string curiousSniffStateName = "CL_CAT_CuriousSniff_v02_loop_112f";
        [SerializeField] private string earTwitchAlertStateName = "CL_CAT_EarTwitchAlert_v02_loop_120f";
        [SerializeField] private string headShakeNoStateName = "CL_CAT_HeadShakeNo_v01_loop_108f";
        [SerializeField] private string headTiltListenStateName = "CL_CAT_HeadTiltListen_v01_loop_96f";
        [SerializeField] private string tailWagHappyStateName = "CL_CAT_TailWagHappy_v01_loop_96f";
        [SerializeField] private string stretchYawnStateName = "CL_CAT_StretchYawn_v03_slow_loop_264f";
        [SerializeField] private string pawWaveStateName = "CL_CAT_PawWave_v01_loop_96f";
        [SerializeField] private string lookBackStateName = "CL_CAT_LookBack_v02_loop_112f";

        private float busyUntil;
        private bool interruptibleByMove = true;
        private string lastPlayedState;
        private int moveSpeed01Hash;
        private int isMovingHash;
        private int inFocusModeHash;
        private int arousal01Hash;
        private int legacyIsWalkingHash;
        private bool hasMoveSpeed01;
        private bool hasIsMoving;
        private bool hasInFocusMode;
        private bool hasArousal01;
        private bool hasLegacyIsWalking;

        private void Reset()
        {
            animator = GetComponentInChildren<Animator>(true);
        }

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

            CacheAnimatorParameters();
        }

        public void Tick(float moveSpeed01, bool isMoving, bool inFocusMode, float arousal01)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            SetAnimatorParameters(moveSpeed01, isMoving, inFocusMode, arousal01);
            if (Time.time >= busyUntil || (isMoving && interruptibleByMove))
            {
                EnsureLocomotion(isMoving);
            }
        }

        public void PlayAction(CatBehaviorState state, float holdSeconds, bool canInterruptByMove)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            string stateName = StateToName(state);
            if (string.IsNullOrEmpty(stateName))
            {
                return;
            }

            if (!HasAnimatorState(stateName))
            {
                busyUntil = 0f;
                return;
            }

            animator.CrossFadeInFixedTime(stateName, actionFade, 0);
            busyUntil = Time.time + Mathf.Max(0f, holdSeconds);
            interruptibleByMove = canInterruptByMove;
            lastPlayedState = stateName;
        }

        public void ForceLocomotion(bool isMoving)
        {
            busyUntil = 0f;
            EnsureLocomotion(isMoving);
        }

        private void CacheAnimatorParameters()
        {
            hasMoveSpeed01 = false;
            hasIsMoving = false;
            hasInFocusMode = false;
            hasArousal01 = false;
            hasLegacyIsWalking = false;

            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            moveSpeed01Hash = Animator.StringToHash(moveSpeed01Parameter);
            isMovingHash = Animator.StringToHash(isMovingParameter);
            inFocusModeHash = Animator.StringToHash(inFocusModeParameter);
            arousal01Hash = Animator.StringToHash(arousal01Parameter);
            legacyIsWalkingHash = Animator.StringToHash(legacyIsWalkingParameter);

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                if (parameter.nameHash == moveSpeed01Hash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasMoveSpeed01 = true;
                }
                else if (parameter.nameHash == isMovingHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasIsMoving = true;
                }
                else if (parameter.nameHash == inFocusModeHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasInFocusMode = true;
                }
                else if (parameter.nameHash == arousal01Hash && parameter.type == AnimatorControllerParameterType.Float)
                {
                    hasArousal01 = true;
                }
                else if (parameter.nameHash == legacyIsWalkingHash && parameter.type == AnimatorControllerParameterType.Bool)
                {
                    hasLegacyIsWalking = true;
                }
            }
        }

        private void SetAnimatorParameters(float moveSpeed01, bool isMoving, bool inFocusMode, float arousal01)
        {
            if (hasMoveSpeed01)
            {
                animator.SetFloat(moveSpeed01Hash, Mathf.Clamp01(moveSpeed01), 0.08f, Time.deltaTime);
            }

            if (hasIsMoving)
            {
                animator.SetBool(isMovingHash, isMoving);
            }

            if (hasInFocusMode)
            {
                animator.SetBool(inFocusModeHash, inFocusMode);
            }

            if (hasArousal01)
            {
                animator.SetFloat(arousal01Hash, Mathf.Clamp01(arousal01));
            }

            if (hasLegacyIsWalking)
            {
                animator.SetBool(legacyIsWalkingHash, isMoving);
            }
        }

        private void EnsureLocomotion(bool isMoving)
        {
            string targetState = isMoving ? walkStateName : idleStateName;
            if (string.IsNullOrEmpty(targetState) || lastPlayedState == targetState)
            {
                return;
            }

            if (!HasAnimatorState(targetState))
            {
                return;
            }

            animator.CrossFadeInFixedTime(targetState, locomotionFade, 0);
            lastPlayedState = targetState;
        }

        private bool HasAnimatorState(string stateName)
        {
            return animator != null && animator.HasState(0, Animator.StringToHash(stateName));
        }

        private string StateToName(CatBehaviorState state)
        {
            switch (state)
            {
                case CatBehaviorState.IdleBreath:
                    return idleStateName;
                case CatBehaviorState.AlertLook:
                    return alertLookStateName;
                case CatBehaviorState.CuriousSniff:
                    return curiousSniffStateName;
                case CatBehaviorState.EarTwitchAlert:
                    return earTwitchAlertStateName;
                case CatBehaviorState.HeadShakeNo:
                    return headShakeNoStateName;
                case CatBehaviorState.HeadTiltListen:
                    return headTiltListenStateName;
                case CatBehaviorState.TailWagHappy:
                    return tailWagHappyStateName;
                case CatBehaviorState.StretchYawn:
                    return stretchYawnStateName;
                case CatBehaviorState.PawWave:
                    return pawWaveStateName;
                case CatBehaviorState.LookBack:
                    return lookBackStateName;
                case CatBehaviorState.Roam:
                case CatBehaviorState.FocusedRoam:
                    return walkStateName;
                default:
                    return null;
            }
        }
    }
}
