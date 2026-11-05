using CatLife.Mobile;
using CatLife.Cat;
using CatLife.Recognition;
using UnityEngine;
using UnityEngine.AI;

public sealed class CatLifeMobileCatPresenter : MonoBehaviour
{
    private const string SitDown = "CL_CAT_StandToSit_v01_72f";
    private const string SitIdle = "CL_CAT_SitIdle_v01_loop_96f";
    private const string LieDown = "CL_CAT_StandToLie_v11_150f";
    private const string FocusRest = "CL_CAT_ProneBreath_v11_loop_240f";
    private const string FocusAttention = "CL_CAT_ProneAttention_v11_48f";
    private const string WakeUp = "CL_CAT_LieToStand_v11_120f";
    public const float LieDownSeconds = 2.5f;
    public const float WakeUpSeconds = 2f;
    private const string StandIdle = "CL_CAT_StandIdle_v01_loop_96f";
    private const string SitSniffStand = "CL_CAT_SitSniffStand_v01_280f";
    private Coroutine sitRecoveryRoutine;
    public bool SessionPreparationFailed { get; private set; }
    public bool SessionPoseReady => LastAnimationCommand == LieDown && sitRecoveryRoutine == null;
    private const string ExitReach = "CL_CAT_ProneExitReach_v01_24f";
    private const string ExitHold = "CL_CAT_ProneExitHold_v01_loop_48f";
    private const string ExitRelease = "CL_CAT_ProneExitRelease_v01_24f";
    private bool exitGestureActive;
    private CatLifeExitGesturePhase exitGesturePhase;
    private float requestedExitReach;
    private Coroutine exitReleaseRoutine;
    public bool IsExitGesturePlaying => exitGestureActive;
    public bool CanBeginExitGesture => !exitGestureActive && animator != null &&
        animator.HasState(0, Animator.StringToHash(ExitReach)) &&
        animator.HasState(0, Animator.StringToHash(ExitHold)) &&
        animator.HasState(0, Animator.StringToHash(ExitRelease));
    private const string Celebrate = "CL_CAT_SitTailGreeting_v01_loop_96f";
    private Animator animator;
    public bool TransitionAnimationComplete => animator != null &&
        animator.GetCurrentAnimatorStateInfo(0).IsName(LieDown) &&
        animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
    public bool WakeUpAnimationComplete => animator != null &&
        animator.GetCurrentAnimatorStateInfo(0).IsName(WakeUp) &&
        animator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1f;
    private string aiReactionState;
    private Coroutine aiReactionRoutine;
    public bool AiReactionCompleted { get; private set; }
    public float AiReactionPawDisplacement { get; private set; }
    private Transform aiPaw;
    private Vector3 aiPawStart;
    public string LastAnimationCommand { get; private set; }
    public bool IsAiReactionPlaying => animator != null && !string.IsNullOrEmpty(aiReactionState) &&
        (animator.GetCurrentAnimatorStateInfo(0).IsName(aiReactionState) ||
        aiReactionState == FocusRest && (animator.GetCurrentAnimatorStateInfo(0).IsName(LieDown) || animator.GetCurrentAnimatorStateInfo(0).IsName(WakeUp)));
    private NavMeshAgent navigationAgent;
    private CatNavigationAgent navigationSync;
    private Rigidbody physicsBody;
    [SerializeField] private Vector3 sessionAnchorPosition;
    [SerializeField] private Quaternion sessionAnchorRotation = Quaternion.identity;
    [SerializeField] private bool sessionAnchorConfigured;
    public RecognitionSnapshot LatestRecognition { get; private set; }

    private void Awake()
    {
        animator = GetComponentInChildren<Animator>();
        navigationAgent = GetComponent<NavMeshAgent>();
        navigationSync = GetComponent<CatNavigationAgent>();
        physicsBody = GetComponent<Rigidbody>();
        if (!sessionAnchorConfigured) ConfigureSessionAnchor(transform.position, transform.rotation);
    }
    public void ConfigureSessionAnchor(Vector3 position, Quaternion rotation)
    {
        sessionAnchorPosition = position;
        sessionAnchorRotation = rotation;
        sessionAnchorConfigured = true;
    }
    public void SetRoamingEnabled(bool enabled)
    {
        if (enabled)
        {
            StopSitRecovery();
            CancelInvoke(nameof(PlayFocusRest));
        }
        if (navigationAgent == null) navigationAgent = GetComponent<NavMeshAgent>();
        if (navigationSync != null) navigationSync.VisualSyncEnabled = enabled;
        if (enabled)
        {
            navigationSync?.EnsureOnNavMesh();
        }
        if (navigationAgent != null && navigationAgent.isOnNavMesh)
        {
            if (!enabled)
            {
                navigationSync?.StopSoft();
                navigationAgent.ResetPath();
                StopHorizontalMotion();
            }
            Vector3 current = physicsBody != null ? physicsBody.position : transform.position;
            navigationAgent.nextPosition = new Vector3(current.x, navigationAgent.nextPosition.y, current.z);
            navigationAgent.isStopped = !enabled;
        }
    }
    public void ShowPhase(CatLifeSessionPhase phase)
    {
        ShowPhase(phase, LatestRecognition);
    }
    public void ShowPhase(CatLifeSessionPhase phase, RecognitionSnapshot recognition)
    {
        StopSitRecovery();
        CancelExitGesture(false);
        StopAiReaction();
        CancelInvoke(nameof(PlayFocusRest));
        LatestRecognition = recognition;
        if (animator != null) animator.speed = 1f;
        SessionPreparationFailed = false;
        if (phase == CatLifeSessionPhase.Transition)
        {
            sitRecoveryRoutine = StartCoroutine(PrepareSessionPose());
            return;
        }
        string state = phase == CatLifeSessionPhase.Focus ? FocusRest : phase == CatLifeSessionPhase.Reward ? WakeUp : SitIdle;
        CrossFade(state, .18f);
    }
    private System.Collections.IEnumerator PrepareSessionPose()
    {
        var current = animator.GetCurrentAnimatorStateInfo(0);
        if (current.IsName(SitSniffStand) && current.normalizedTime < 268f / 280f)
        {
            // Recover in place before asking the existing navigation to move.
            float phase = Mathf.Clamp01(current.normalizedTime);
            if (phase < 196f / 280f)
            {
                float recovery = Mathf.Max(196f / 280f, 1 - phase);
                var clip = System.Array.Find(animator.runtimeAnimatorController.animationClips, item => item.name == SitSniffStand);
                animator.CrossFadeInFixedTime(SitSniffStand, .12f, 0, recovery * clip.length);
            }
            LastAnimationCommand = SitSniffStand;
            yield return null;
            while (animator.IsInTransition(0) ||
                animator.GetCurrentAnimatorStateInfo(0).IsName(SitSniffStand) && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1)
                yield return null;
        }
        CrossFade(StandIdle, .12f);
        yield return new WaitForSeconds(.12f);
        Vector3 anchor = GroundedAnchor(sessionAnchorPosition);
        Vector3 currentPosition = physicsBody != null ? physicsBody.position : transform.position;
        Quaternion currentRotation = physicsBody != null ? physicsBody.rotation : transform.rotation;
        Vector3 offset = anchor - currentPosition;
        offset.y = 0;
        if (offset.magnitude > .03f || Quaternion.Angle(currentRotation, sessionAnchorRotation) > .5f)
        {
            navigationSync.VisualSyncEnabled = true;
            // Approach along the authored heading so arrival itself establishes
            // the focus pose; do not finish with a root snap or a standing spin.
            if (navigationSync.TryFindSessionApproach(anchor, sessionAnchorRotation, out var approach))
                yield return WalkToSessionPoint(approach);
            else SessionPreparationFailed = true;
            if (!SessionPreparationFailed) yield return WalkToSessionPoint(anchor, sessionAnchorRotation);
            navigationSync.StopSoft();
            navigationSync.VisualSyncEnabled = false;
            StopHorizontalMotion();
            currentPosition = physicsBody != null ? physicsBody.position : transform.position;
            currentRotation = physicsBody != null ? physicsBody.rotation : transform.rotation;
            offset = anchor - currentPosition;
            offset.y = 0;
            SessionPreparationFailed |= offset.magnitude > .03f || Quaternion.Angle(currentRotation, sessionAnchorRotation) > .5f;
        }
        sitRecoveryRoutine = null;
        if (SessionPreparationFailed) yield break;
        animator.speed = 1;
        CrossFade(LieDown, .18f);
    }
    private System.Collections.IEnumerator WalkToSessionPoint(Vector3 target, Quaternion? heading = null)
    {
        if (!navigationSync.TryMoveToSessionPoint(target, heading))
        {
            SessionPreparationFailed = true;
            yield break;
        }
        var locomotion = GetComponent<CatAnimationController>();
        locomotion.ForceLocomotion(true);
        while (navigationSync.IsPathInProgress)
        {
            locomotion.Tick(navigationSync.Speed01, true, false, 0);
            yield return null;
        }
        // The caller owns the next pose. Inserting Stand here interrupts Walk
        // immediately before the next leg requests Walk again in the same frame.
    }
    private void StopSitRecovery()
    {
        if (sitRecoveryRoutine != null)
        {
            StopCoroutine(sitRecoveryRoutine);
            navigationSync?.StopSoft();
            if (navigationSync != null) navigationSync.VisualSyncEnabled = false;
            StopHorizontalMotion();
        }
        sitRecoveryRoutine = null;
    }
    private void StopHorizontalMotion()
    {
        if (physicsBody == null) return;
        Vector3 velocity = physicsBody.linearVelocity;
        physicsBody.linearVelocity = new Vector3(0, velocity.y, 0);
        physicsBody.angularVelocity = Vector3.zero;
    }
    public void UpdateRecognition(RecognitionSnapshot recognition) { LatestRecognition = recognition; }
    public void Nudge()
    {
        if (exitGestureActive) return;
        CancelInvoke(nameof(PlayFocusRest));
        CrossFade(FocusAttention, .08f);
        Invoke(nameof(PlayFocusRest), .88f);
    }
    public void ReturnHome()
    {
        StopSitRecovery();
        CancelExitGesture(false);
        StopAiReaction();
        CancelInvoke(nameof(PlayFocusRest));
        CrossFade(StandIdle, .12f);
    }
    public void CelebrateReward() { CrossFade(Celebrate, .12f); }
    public bool PlayAiReaction(string reaction)
    {
        CancelExitGesture(false);
        string state;
        switch (reaction)
        {
            case "paw_wave": state = "CL_CAT_CrouchPawWave_v01_loop_96f"; break;
            case "tail_wag": state = "CL_CAT_RewardSideTailReturn_v01_384f"; break;
            case "focus_rest": state = FocusRest; break;
            case "stretch": state = "CL_CAT_StandStretchYawn_v01_144f"; break;
            default: return false;
        }
        if (animator == null || !animator.HasState(0, Animator.StringToHash(state))) return false;
        CancelInvoke(nameof(PlayFocusRest));
        Vector3 anchor = GroundedAnchor(sessionAnchorPosition);
        SetPhysicsPose(anchor, sessionAnchorRotation);
        StopAiReaction();
        animator.speed = 1f;
        aiPaw = System.Array.Find(GetComponentInChildren<SkinnedMeshRenderer>().bones, bone => bone.name == "frontleg2");
        aiPawStart = aiPaw.position;
        AiReactionPawDisplacement = 0;
        CrossFade(state == FocusRest ? LieDown : state, .18f);
        aiReactionState = state;
        aiReactionRoutine = StartCoroutine(FinishAiReaction(state));
        return true;
    }
    private System.Collections.IEnumerator FinishAiReaction(string state)
    {
        if (state == FocusRest)
        {
            yield return WaitForAiClip(LieDown);
            CrossFade(FocusRest, .18f);
        }
        yield return WaitForAiClip(state);
        if (state == FocusRest && animator.GetCurrentAnimatorStateInfo(0).IsName(state))
        {
            CrossFade(WakeUp, .18f);
            yield return WaitForAiClip(WakeUp);
        }
        if (animator.GetCurrentAnimatorStateInfo(0).IsName(state) || state == FocusRest && animator.GetCurrentAnimatorStateInfo(0).IsName(WakeUp))
        {
            AiReactionCompleted = true;
            Debug.Log($"[CatLife] ai_clip_completed state={state} paw_displacement_m={AiReactionPawDisplacement:F3}");
            CrossFade(StandIdle, .18f);
        }
        aiReactionState = null;
        aiReactionRoutine = null;
    }
    private System.Collections.IEnumerator WaitForAiClip(string state)
    {
        yield return new WaitForSeconds(.18f);
        while (animator.IsInTransition(0)) yield return null;
        while (animator.GetCurrentAnimatorStateInfo(0).IsName(state) &&
            animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f)
        {
            AiReactionPawDisplacement = Mathf.Max(AiReactionPawDisplacement, Vector3.Distance(aiPawStart, aiPaw.position));
            yield return null;
        }
    }
    private void StopAiReaction()
    {
        if (aiReactionRoutine != null) StopCoroutine(aiReactionRoutine);
        aiReactionRoutine = null;
        aiReactionState = null;
        AiReactionCompleted = false;
    }
    private void PlayFocusRest() { CrossFade(FocusRest, .12f); }
    public void UpdateExitGesture(CatLifeExitGesturePhase phase, float progress)
    {
        if (phase == CatLifeExitGesturePhase.Reaching)
        {
            if (exitReleaseRoutine != null) StopCoroutine(exitReleaseRoutine);
            exitReleaseRoutine = null;
            exitGestureActive = true;
            exitGesturePhase = phase;
            CancelInvoke(nameof(PlayFocusRest));
            animator.speed = 0;
            requestedExitReach = Mathf.InverseLerp(.15f, .35f, progress);
            PlayExit(ExitReach, requestedExitReach);
        }
        else if (phase == CatLifeExitGesturePhase.Preparing && exitGesturePhase != phase)
        {
            float from = exitGesturePhase == CatLifeExitGesturePhase.Reaching ? requestedExitReach : 0;
            if (exitReleaseRoutine != null)
            {
                var current = animator.GetCurrentAnimatorStateInfo(0);
                from = current.IsName(ExitRelease) ? 1 - Mathf.Clamp01(current.normalizedTime) : requestedExitReach;
                StopCoroutine(exitReleaseRoutine);
                exitReleaseRoutine = null;
            }
            requestedExitReach = from;
            exitGestureActive = true; exitGesturePhase = phase;
            CancelInvoke(nameof(PlayFocusRest));
            animator.speed = 1;
            PlayExit(ExitReach, from);
        }
        else if (phase == CatLifeExitGesturePhase.Holding && exitGesturePhase != phase)
        {
            exitGestureActive = true; exitGesturePhase = phase;
            requestedExitReach = 1;
            animator.speed = 1;
            PlayExit(ExitHold, 0);
        }
        else if (phase == CatLifeExitGesturePhase.Idle || phase == CatLifeExitGesturePhase.Dragging || phase == CatLifeExitGesturePhase.Released)
        {
            if (!exitGestureActive || exitReleaseRoutine != null) return;
            // Pointer events can reverse before Animator has evaluated Play().
            float from = requestedExitReach;
            if (exitGesturePhase == CatLifeExitGesturePhase.Preparing && animator.GetCurrentAnimatorStateInfo(0).IsName(ExitReach))
                from = Mathf.Clamp01(animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
            exitGesturePhase = phase;
            animator.speed = 1;
            PlayExit(ExitRelease, 1-from);
            exitReleaseRoutine = StartCoroutine(FinishExitRelease());
        }
    }
    private void PlayExit(string state, float phase)
    {
        LastAnimationCommand = state;
        animator.Play(state, 0, phase);
    }
    private System.Collections.IEnumerator FinishExitRelease()
    {
        yield return null;
        while (animator.GetCurrentAnimatorStateInfo(0).IsName(ExitRelease) && animator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1)
            yield return null;
        exitReleaseRoutine = null; exitGestureActive = false;
        exitGesturePhase = CatLifeExitGesturePhase.Idle;
        requestedExitReach = 0;
        CrossFade(FocusRest, .08f);
    }
    public void CancelExitGesture(bool restoreFocus)
    {
        bool wasActive = exitGestureActive;
        if (exitReleaseRoutine != null) StopCoroutine(exitReleaseRoutine);
        exitReleaseRoutine = null; exitGestureActive = false;
        exitGesturePhase = CatLifeExitGesturePhase.Idle;
        requestedExitReach = 0;
        if (animator != null) animator.speed = 1;
        if (wasActive && restoreFocus) CrossFade(FocusRest, .08f);
    }
    private void CrossFade(string state, float duration)
    {
        LastAnimationCommand = state;
        if (animator != null) animator.CrossFadeInFixedTime(state, duration, 0, 0f);
    }

    private void SetPhysicsPose(Vector3 position, Quaternion rotation)
    {
        if (physicsBody == null) physicsBody = GetComponent<Rigidbody>();
        if (physicsBody != null)
        {
            physicsBody.position = position;
            physicsBody.rotation = rotation;
            return;
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    private Vector3 GroundedAnchor(Vector3 anchor)
    {
        if (physicsBody == null) physicsBody = GetComponent<Rigidbody>();
        if (physicsBody != null) anchor.y = physicsBody.position.y;
        return anchor;
    }
}
