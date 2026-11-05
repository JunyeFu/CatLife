using System.Collections;
using CatLife.Mobile;
using CatLife.Recognition;
using CatLife.Cat;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CatLifeMobileRuntimeCoordinator : MonoBehaviour
{
    [SerializeField] private RealtimeFeatureEngine featureEngine;
    [SerializeField] private MockRecognitionProvider recognitionProvider;
    [SerializeField] private CatLifeMobileCatPresenter catPresenter;
    [SerializeField] private CatBehaviorDriver behaviorDriver;

    private bool hasAppliedPhase;
    private CatLifeSessionPhase appliedPhase;
    private Coroutine returnHomeRoutine;

    public CatLifeSessionPhase AppliedPhase => appliedPhase;
    public bool TransitionAnimationComplete => catPresenter != null && catPresenter.TransitionAnimationComplete;
    public bool SessionPreparationFailed => catPresenter != null && catPresenter.SessionPreparationFailed;
    public bool SessionPoseReady => catPresenter != null && catPresenter.SessionPoseReady;
    public bool WakeUpAnimationComplete => catPresenter != null && catPresenter.WakeUpAnimationComplete;
    public bool BehaviorOwnsAnimator => hasAppliedPhase && appliedPhase == CatLifeSessionPhase.Normal && returnHomeRoutine == null &&
        behaviorDriver != null && behaviorDriver.enabled;

    public RecognitionSnapshot LatestRecognition => recognitionProvider != null
        ? recognitionProvider.Latest
        : RecognitionSnapshot.CreateDefault();

    public void Configure(
        RealtimeFeatureEngine features,
        MockRecognitionProvider recognition,
        CatLifeMobileCatPresenter presenter,
        CatBehaviorDriver behavior)
    {
        featureEngine = features;
        recognitionProvider = recognition;
        catPresenter = presenter;
        behaviorDriver = behavior;
    }

    private void Update()
    {
        if (behaviorDriver == null || !behaviorDriver.enabled)
            recognitionProvider?.Tick(Time.unscaledDeltaTime);
        catPresenter?.UpdateRecognition(LatestRecognition);
    }

    public void ApplyPhase(CatLifeSessionPhase phase)
    {
        if (hasAppliedPhase && appliedPhase == phase) return;
        if (returnHomeRoutine != null)
        {
            StopCoroutine(returnHomeRoutine);
            returnHomeRoutine = null;
        }
        if (!hasAppliedPhase || appliedPhase != phase)
        {
            if (phase == CatLifeSessionPhase.Focus)
                featureEngine?.RecordFocusSessionStarted();
            else if (hasAppliedPhase && appliedPhase == CatLifeSessionPhase.Focus)
                featureEngine?.RecordFocusSessionEnded(phase == CatLifeSessionPhase.Reward);

            appliedPhase = phase;
            hasAppliedPhase = true;
        }

        bool behaviorOwnsAnimator = phase == CatLifeSessionPhase.Normal && behaviorDriver != null;
        if (behaviorDriver != null) behaviorDriver.enabled = behaviorOwnsAnimator;
        catPresenter?.SetRoamingEnabled(behaviorOwnsAnimator);
        if (!behaviorOwnsAnimator) catPresenter?.ShowPhase(phase, LatestRecognition);
    }

    public void RecordUiEvent(string eventName) => featureEngine?.RecordUiEvent(eventName);
    public void LogNavigationSnapshot()
    {
        var body = catPresenter.GetComponent<Rigidbody>();
        var agent = catPresenter.GetComponent<UnityEngine.AI.NavMeshAgent>();
        var animator = catPresenter.GetComponentInChildren<Animator>();
        var clips = animator.GetCurrentAnimatorClipInfo(0);
        var camera = Camera.main;
        var planner = catPresenter.GetComponent<CatDestinationPlanner>();
        int validNearby = 0;
        for (int i = 0; i < 24; i++)
        {
            float angle = i * Mathf.PI / 12f;
            Vector3 candidate = body.position + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * .5f;
            if (UnityEngine.AI.NavMesh.SamplePosition(candidate, out var hit, 1f, UnityEngine.AI.NavMesh.AllAreas)
                && planner.IsDestinationValid(hit.position, body.position, false)) validNearby++;
        }
        Debug.Log($"CATLIFE_NAV_SNAPSHOT phase={appliedPhase} owner={BehaviorOwnsAnimator} state={behaviorDriver.CurrentState} " +
            $"position={body.position:F3} yaw={body.rotation.eulerAngles.y:F2} velocity={body.linearVelocity:F3} " +
            $"nav={agent.nextPosition:F3} desired={agent.desiredVelocity:F3} stopped={agent.isStopped} " +
            $"path={agent.hasPath} remaining={agent.remainingDistance:F3} " +
            $"animatorEuler={animator.transform.eulerAngles:F2} clip={clips[0].clip.name} " +
            $"animationPhase={animator.GetCurrentAnimatorStateInfo(0).normalizedTime:F5} blending={animator.IsInTransition(0)} " +
            $"camera={camera.transform.position:F3} aspect={camera.aspect:F4} fov={camera.fieldOfView:F2} " +
            $"rect={camera.rect} nearbyValid={validNearby}/24 " + behaviorDriver.NavigationDecisionSummary);
    }
    public void SetBehaviorStatistics(bool enabled) => featureEngine.SetCollectionEnabled(enabled);
    public void RecordUiTap(string eventName) => featureEngine?.RecordUiTap(eventName);
    public void RecordFocusTouch() => featureEngine?.RecordUiEvent("focus_touch");
    public void NudgeCat() => catPresenter?.Nudge();
    public bool CanBeginExitGesture => appliedPhase == CatLifeSessionPhase.Focus && catPresenter != null && catPresenter.CanBeginExitGesture;
    public void BindExitContact(CatLifePawContactView view, Camera camera) => view.Bind(camera, catPresenter.transform);
    public void UpdateExitGesture(CatLifeExitGesturePhase phase, float progress)
    {
        if (appliedPhase == CatLifeSessionPhase.Focus) catPresenter.UpdateExitGesture(phase, progress);
    }
    public void CancelExitGesture(bool restoreFocus)
    {
        if (catPresenter != null) catPresenter.CancelExitGesture(restoreFocus && appliedPhase == CatLifeSessionPhase.Focus);
    }
    public string CompanionStatus => behaviorDriver == null ? "安静陪伴" :
        behaviorDriver.CurrentState == CatBehaviorState.Roam || behaviorDriver.CurrentState == CatBehaviorState.FocusedRoam ? "好奇 · 正在探索小镇" :
        behaviorDriver.CurrentState == CatBehaviorState.IdleBreath ? "放松 · 在你身边休息" :
        behaviorDriver.CurrentState == CatBehaviorState.CuriousSniff ? "好奇 · 闻闻周围的气味" : "亲近 · 正在回应你";
    public void RespondToLandmark(CatLifeLandmarkAction action)
    {
        behaviorDriver.NotifyUiAction(action == CatLifeLandmarkAction.FishShop ? CatBehaviorState.CuriousSniff :
            action == CatLifeLandmarkAction.RewardTree ? CatBehaviorState.TailWagHappy : CatBehaviorState.StretchYawn, "landmark_" + action);
    }
    public void CelebrateReward() => catPresenter?.CelebrateReward();
    public bool IsAiReactionPlaying => catPresenter != null && catPresenter.IsAiReactionPlaying;
    public bool PlayAiReaction(string reaction) => appliedPhase == CatLifeSessionPhase.Reward &&
        catPresenter != null && catPresenter.PlayAiReaction(reaction);
    public void ReturnCatHome()
    {
        if (returnHomeRoutine != null) StopCoroutine(returnHomeRoutine);
        if (behaviorDriver != null) behaviorDriver.enabled = false;
        catPresenter?.SetRoamingEnabled(false);
        catPresenter?.ReturnHome();
        returnHomeRoutine = StartCoroutine(FinishReturnHome());
    }

    private IEnumerator FinishReturnHome()
    {
        yield return new WaitForSeconds(1.2f);
        returnHomeRoutine = null;
        if (appliedPhase != CatLifeSessionPhase.Normal) yield break;
        catPresenter?.SetRoamingEnabled(true);
        if (behaviorDriver != null) behaviorDriver.enabled = true;
    }
}
