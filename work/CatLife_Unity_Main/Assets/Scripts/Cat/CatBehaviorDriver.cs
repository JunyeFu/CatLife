using CatLife.LLM;
using CatLife.Recognition;
using CatLife.SceneInteraction;
using UnityEngine;

namespace CatLife.Cat
{
    [DisallowMultipleComponent]
    public sealed class CatBehaviorDriver : MonoBehaviour
    {
        [Header("Core")]
        [SerializeField] private MonoBehaviour recognitionProviderComponent;
        [SerializeField] private MonoBehaviour llmClientComponent;
        [SerializeField] private CatNavigationAgent navigationAgent;
        [SerializeField] private CatAnimationController animationController;
        [SerializeField] private CatDestinationPlanner destinationPlanner;
        [SerializeField] private CatActionRouter actionRouter;
        [SerializeField] private RealtimeFeatureEngine featureEngine;
        [SerializeField] private CatNeedModel needModel;
        [SerializeField] private CatBehaviorMemory behaviorMemory;
        [SerializeField] private CatBehaviorBrainScorer behaviorScorer;
        [SerializeField] private Transform focusLookAtTarget;

        [Header("Scene Interaction")]
        [SerializeField] private SceneInteractionMapper sceneInteractionMapper;
        [SerializeField] private SceneInteractionRegistry sceneInteractionRegistry;
        [SerializeField] private SceneInteractionMemory sceneInteractionMemory = SceneInteractionMemory.CreateDefault();

        [Header("Focus Presence")]
        [SerializeField] private bool faceCameraWhenFocusedIdle = true;
        [SerializeField] private float focusFaceCameraDegreesPerSecond = 360f;

        [Header("Timing")]
        [SerializeField] private float decisionInterval = 0.5f;
        [SerializeField] private float llmRefreshInterval = 15f;

        [Header("Action Holds")]
        [SerializeField] private float shortActionSeconds = 0.9f;
        [SerializeField] private float mediumActionSeconds = 1.4f;
        [SerializeField] private float longActionSeconds = 2.1f;

        [Header("Non Focus Weights")]
        [SerializeField] private float nonFocusRoamWeight = 70f;
        [SerializeField] private float nonFocusSniffWeight = 10f;
        [SerializeField] private float nonFocusLookBackWeight = 8f;
        [SerializeField] private float nonFocusTailWagWeight = 7f;
        [SerializeField] private float nonFocusStretchWeight = 5f;

        [Header("Focus Weights")]
        [SerializeField] private float focusRoamWeight = 35f;
        [SerializeField] private float focusIdleWeight = 35f;
        [SerializeField] private float focusEarTwitchWeight = 18f;
        [SerializeField] private float focusAlertLookWeight = 8f;
        [SerializeField] private float focusTailWagWeight = 4f;

        private IRecognitionProvider recognitionProvider;
        private ICatLLMClient llmClient;
        private CatPromptBuilder promptBuilder;
        private RecognitionSnapshot snapshot;
        private LLMBehaviorSuggestion llmSuggestion;
        private CatBehaviorState currentState = CatBehaviorState.IdleBreath;
        private float nextDecisionTime;
        private float nextLlmTime;
        private float actionHoldUntil;
        private bool walkingEnabled = true;
        private float navigationSpeedMultiplier = 1f;
        private readonly string[] recentEvents = new string[4];
        private SceneInteractionPayload latestSceneInteractionPayload;
        private SceneInteractionPoint latestSceneInteractionPoint;
        private bool sceneInteractionMapperSubscribed;

        public CatBehaviorState CurrentState
        {
            get { return currentState; }
        }

        public RecognitionSnapshot LatestRecognitionSnapshot
        {
            get { return snapshot; }
        }

        public LLMBehaviorSuggestion LatestLlmSuggestion
        {
            get { return GetSuggestion(); }
        }

        public float ActionHoldRemaining
        {
            get { return Mathf.Max(0f, actionHoldUntil - Time.time); }
        }

        public bool IsActionHeld
        {
            get { return Time.time < actionHoldUntil; }
        }

        public SceneInteractionPayload LatestSceneInteractionPayload
        {
            get { return latestSceneInteractionPayload; }
        }

        public SceneInteractionPoint LatestSceneInteractionPoint
        {
            get { return latestSceneInteractionPoint; }
        }

        private void Reset()
        {
            navigationAgent = GetComponent<CatNavigationAgent>();
            animationController = GetComponent<CatAnimationController>();
            destinationPlanner = GetComponent<CatDestinationPlanner>();
            actionRouter = GetComponent<CatActionRouter>();
            needModel = GetComponent<CatNeedModel>();
            behaviorMemory = GetComponent<CatBehaviorMemory>();
            behaviorScorer = GetComponent<CatBehaviorBrainScorer>();
        }

        private void Awake()
        {
            if (navigationAgent == null)
            {
                navigationAgent = GetComponent<CatNavigationAgent>();
            }

            if (animationController == null)
            {
                animationController = GetComponent<CatAnimationController>();
            }

            if (destinationPlanner == null)
            {
                destinationPlanner = GetComponent<CatDestinationPlanner>();
            }

            if (actionRouter == null)
            {
                actionRouter = GetComponent<CatActionRouter>();
            }

            if (actionRouter == null)
            {
                actionRouter = gameObject.AddComponent<CatActionRouter>();
            }

            if (needModel == null)
            {
                needModel = GetComponent<CatNeedModel>();
            }

            if (needModel == null)
            {
                needModel = gameObject.AddComponent<CatNeedModel>();
            }

            if (behaviorMemory == null)
            {
                behaviorMemory = GetComponent<CatBehaviorMemory>();
            }

            if (behaviorMemory == null)
            {
                behaviorMemory = gameObject.AddComponent<CatBehaviorMemory>();
            }

            if (behaviorScorer == null)
            {
                behaviorScorer = GetComponent<CatBehaviorBrainScorer>();
            }

            if (behaviorScorer == null)
            {
                behaviorScorer = gameObject.AddComponent<CatBehaviorBrainScorer>();
            }

            ResolveFocusLookAtTarget();
            ResolveFeatureEngine();
            ResolveSceneInteractionReferences();
            SubscribeSceneInteractionMapper();
            recognitionProvider = recognitionProviderComponent as IRecognitionProvider;
            llmClient = llmClientComponent as ICatLLMClient;
            promptBuilder = new CatPromptBuilder();
            llmSuggestion = LLMBehaviorSuggestion.Default();
            snapshot = RecognitionSnapshot.CreateDefault();

            if (recognitionProvider != null)
            {
                recognitionProvider.Initialize();
            }

            if (navigationAgent != null)
            {
                navigationAgent.WarpToNearestNavMesh();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeSceneInteractionMapper();
        }

        private void Update()
        {
            float unscaledDeltaTime = Time.unscaledDeltaTime;
            if (recognitionProvider != null)
            {
                recognitionProvider.Tick(unscaledDeltaTime);
                snapshot = recognitionProvider.Latest;
            }
            else
            {
                snapshot = RecognitionSnapshot.CreateDefault();
            }

            if (needModel != null)
            {
                needModel.Tick(
                    snapshot,
                    featureEngine != null ? featureEngine.Latest : default(RealtimeFeatureSnapshot),
                    featureEngine != null,
                    unscaledDeltaTime);
            }

            TickLlm();
            TickAnimation();
            TryPlayQueuedAction();
            TickFocusedLookAtCamera(unscaledDeltaTime);

            if (Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + Mathf.Max(0.1f, decisionInterval);
            Decide();
        }

        public void NotifyCatTapped()
        {
            PushRecentEvent("cat_tap");
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordUserInteraction();
            }

            if (featureEngine != null)
            {
                featureEngine.RecordCatInteraction("cat_tap");
            }

            MockRecognitionProvider mock = recognitionProvider as MockRecognitionProvider;
            if (mock != null)
            {
                mock.NotifyCatTapped();
            }

            RouteAction(CatActionRequest.Create(
                WeightedInteractionPick(),
                CatActionSource.User,
                "cat_tap",
                70,
                8f,
                2f,
                CatActionInterruptPolicy.QueueIfMoving,
                false));
        }

        public void SetFocusMode(bool focused)
        {
            MockRecognitionProvider mock = recognitionProvider as MockRecognitionProvider;
            if (mock != null)
            {
                mock.SetFocusState(focused ? FocusState.Focused : FocusState.NonFocus);
                snapshot = mock.Latest;
            }
            else
            {
                snapshot.focusState = focused ? FocusState.Focused : FocusState.NonFocus;
            }

            ApplyCurrentMovementConfiguration(focused);
            if (focused)
            {
                TryReturnToCameraRangeForFocus();
            }
        }

        public void SetContinuousWalking(bool enabled, float speedMultiplier)
        {
            walkingEnabled = enabled;
            navigationSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 3f);

            if (navigationAgent != null)
            {
                navigationAgent.SetSpeedMultiplier(navigationSpeedMultiplier);
                navigationAgent.Configure(snapshot.IsFocused);
                if (!walkingEnabled)
                {
                    navigationAgent.StopSoft();
                }
            }

            if (animationController != null)
            {
                animationController.SetLocomotionPlaybackMultiplier(navigationSpeedMultiplier);
            }
        }

        private void ApplyCurrentMovementConfiguration(bool focused)
        {
            if (navigationAgent != null)
            {
                navigationAgent.SetSpeedMultiplier(navigationSpeedMultiplier);
                navigationAgent.Configure(focused);
            }

            if (animationController != null)
            {
                animationController.SetLocomotionPlaybackMultiplier(navigationSpeedMultiplier);
            }
        }

        public void NotifyCatLongPressed()
        {
            PushRecentEvent("cat_long_press");
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordUserInteraction();
            }

            if (featureEngine != null)
            {
                featureEngine.RecordCatInteraction("cat_long_press");
            }

            MockRecognitionProvider mock = recognitionProvider as MockRecognitionProvider;
            if (mock != null)
            {
                mock.NotifyCatLongPressed();
            }

            RouteAction(CatActionRequest.Create(
                snapshot.IsFocused ? CatBehaviorState.HeadTiltListen : CatBehaviorState.TailWagHappy,
                CatActionSource.User,
                "cat_long_press",
                70,
                12f,
                2f,
                CatActionInterruptPolicy.QueueIfMoving,
                false));
        }

        public bool NotifyGroundTapped(Vector3 worldPoint)
        {
            PushRecentEvent("ground_tap");
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordUserInteraction();
            }

            if (featureEngine != null)
            {
                featureEngine.RecordUiEvent("tap_ground");
            }

            if (!walkingEnabled || navigationAgent == null || destinationPlanner == null)
            {
                return false;
            }

            bool focused = snapshot.IsFocused;
            navigationAgent.SetSpeedMultiplier(navigationSpeedMultiplier);
            navigationAgent.Configure(focused);

            Vector3 target;
            if (!destinationPlanner.TryPlanRequestedPoint(snapshot, worldPoint, transform.position, out target))
            {
                RouteAction(CatActionRequest.Create(
                    CatBehaviorState.LookBack,
                    CatActionSource.User,
                    "tap_ground_rejected",
                    70,
                    8f,
                    1f,
                    CatActionInterruptPolicy.DropIfBusy,
                    false));
                return false;
            }

            if (!navigationAgent.TryMoveTo(target))
            {
                return false;
            }

            currentState = focused ? CatBehaviorState.FocusedRoam : CatBehaviorState.Roam;
            actionHoldUntil = 0f;
            if (animationController != null)
            {
                animationController.ForceLocomotion(true);
            }

            if (actionRouter != null)
            {
                CatActionRequest sniffRequest = CatActionRequest.Create(
                    CatBehaviorState.CuriousSniff,
                    CatActionSource.User,
                    "tap_ground_arrival",
                    70,
                    10f,
                    3f,
                    CatActionInterruptPolicy.QueueIfMoving,
                    false);
                CatActionRequest playableRequest;
                actionRouter.TryRoute(sniffRequest, true, false, out playableRequest);
            }

            return true;
        }

        public bool NotifySceneInteraction(SceneInteractionPayload payload)
        {
            if (!payload.IsValid)
            {
                return false;
            }

            ResolveSceneInteractionReferences();
            SceneInteractionPoint point;
            if (sceneInteractionRegistry == null || !sceneInteractionRegistry.TryGet(payload.pointId, out point))
            {
                return false;
            }

            return NotifySceneInteraction(payload, point);
        }

        public bool NotifySceneInteraction(SceneInteractionPayload payload, SceneInteractionPoint point)
        {
            if (!payload.IsValid || point == null)
            {
                return false;
            }

            PushRecentEvent("scene_interaction");
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordUserInteraction();
            }

            if (featureEngine != null)
            {
                featureEngine.RecordUiEvent("scene_interaction");
            }

            latestSceneInteractionPayload = payload;
            latestSceneInteractionPoint = point;
            float now = Time.time;
            bool focused = snapshot.IsFocused;
            sceneInteractionMemory.RecordClick(payload.pointId, now);

            if (!point.CanTrigger(focused, now))
            {
                RouteAction(CatActionRequest.Create(
                    focused ? CatBehaviorState.EarTwitchAlert : CatBehaviorState.HeadShakeNo,
                    CatActionSource.User,
                    "scene_interaction_unavailable",
                    focused ? 45 : 60,
                    focused ? 8f : 5f,
                    1f,
                    CatActionInterruptPolicy.DropIfBusy,
                    false));
                return false;
            }

            if (!TryStartSceneInteractionMove(payload, point, focused))
            {
                RouteAction(CatActionRequest.Create(
                    CatBehaviorState.LookBack,
                    CatActionSource.User,
                    "scene_interaction_rejected",
                    65,
                    8f,
                    1f,
                    CatActionInterruptPolicy.DropIfBusy,
                    false));
                return false;
            }

            point.MarkTriggered(now);
            return true;
        }

        public void NotifyUiAction(CatBehaviorState state, string reason)
        {
            if (state == CatBehaviorState.None)
            {
                return;
            }

            PushRecentEvent(reason);
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordUserInteraction();
            }

            if (featureEngine != null)
            {
                featureEngine.RecordUiEvent(reason);
            }

            RouteAction(CatActionRequest.Create(
                state,
                CatActionSource.Ui,
                reason,
                60,
                10f,
                2f,
                CatActionInterruptPolicy.QueueIfMoving,
                false));
        }

        public void NotifyFocusSessionStarted()
        {
            PushRecentEvent("focus_started");
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordUserInteraction();
            }

            if (featureEngine != null)
            {
                featureEngine.RecordFocusSessionStarted();
            }
        }

        public void NotifyFocusSessionEnded(bool completed)
        {
            PushRecentEvent(completed ? "focus_completed" : "focus_unlocked");
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordUserInteraction();
            }

            if (featureEngine != null)
            {
                featureEngine.RecordFocusSessionEnded(completed);
            }
        }

        private void ResolveFeatureEngine()
        {
            if (featureEngine != null)
            {
                return;
            }

            if (recognitionProviderComponent != null)
            {
                featureEngine = recognitionProviderComponent.GetComponent<RealtimeFeatureEngine>();
            }

            if (featureEngine == null)
            {
                featureEngine = FindAnyObjectByType<RealtimeFeatureEngine>();
            }
        }

        private void ResolveSceneInteractionReferences()
        {
            if (sceneInteractionRegistry == null)
            {
                sceneInteractionRegistry = FindAnyObjectByType<SceneInteractionRegistry>();
            }

            if (sceneInteractionMapper == null)
            {
                sceneInteractionMapper = FindAnyObjectByType<SceneInteractionMapper>();
            }
        }

        private void SubscribeSceneInteractionMapper()
        {
            if (sceneInteractionMapperSubscribed || sceneInteractionMapper == null)
            {
                return;
            }

            sceneInteractionMapper.InteractionMapped += HandleSceneInteractionMapped;
            sceneInteractionMapperSubscribed = true;
        }

        private void UnsubscribeSceneInteractionMapper()
        {
            if (!sceneInteractionMapperSubscribed || sceneInteractionMapper == null)
            {
                return;
            }

            sceneInteractionMapper.InteractionMapped -= HandleSceneInteractionMapped;
            sceneInteractionMapperSubscribed = false;
        }

        private void HandleSceneInteractionMapped(SceneInteractionPayload payload, SceneInteractionPoint point)
        {
            NotifySceneInteraction(payload, point);
        }

        private void ResolveFocusLookAtTarget()
        {
            if (focusLookAtTarget != null)
            {
                return;
            }

            Camera camera = Camera.main;
            if (camera != null)
            {
                focusLookAtTarget = camera.transform;
            }
        }

        private void TryReturnToCameraRangeForFocus()
        {
            if (!walkingEnabled || navigationAgent == null || destinationPlanner == null)
            {
                return;
            }

            if (destinationPlanner.IsPointInPreferredCameraRange(transform.position))
            {
                return;
            }

            navigationAgent.SetSpeedMultiplier(navigationSpeedMultiplier);
            navigationAgent.Configure(true);
            TryStartMove(CatBehaviorDecision.Create(
                CatBehaviorState.FocusedRoam,
                0f,
                0f,
                80,
                CatActionInterruptPolicy.DropIfBusy,
                true,
                "focus_enter_return_camera"));
        }

        private void TickFocusedLookAtCamera(float dt)
        {
            if (!faceCameraWhenFocusedIdle || !snapshot.IsFocused)
            {
                return;
            }

            if (navigationAgent != null && navigationAgent.IsMoving)
            {
                return;
            }

            ResolveFocusLookAtTarget();
            if (focusLookAtTarget == null)
            {
                return;
            }

            Vector3 direction = focusLookAtTarget.position - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                Mathf.Max(1f, focusFaceCameraDegreesPerSecond) * Mathf.Max(0f, dt));
        }

        private void TickLlm()
        {
            if (llmClient == null || !llmClient.Enabled || llmClient.IsBusy || Time.time < nextLlmTime)
            {
                return;
            }

            nextLlmTime = Time.time + Mathf.Max(5f, llmRefreshInterval);
            float secondsSinceSceneInteraction = latestSceneInteractionPayload.IsValid
                ? Mathf.Max(0f, Time.time - latestSceneInteractionPayload.occurredAt)
                : 999f;
            CatPromptContext context = CatPromptContext.Create(
                snapshot,
                currentState,
                navigationAgent != null ? navigationAgent.Speed01 : 0f,
                llmSuggestion != null ? llmSuggestion.moodBias : "calm",
                recentEvents,
                featureEngine != null ? featureEngine.Latest : default(RealtimeFeatureSnapshot),
                featureEngine != null,
                latestSceneInteractionPayload,
                latestSceneInteractionPoint,
                secondsSinceSceneInteraction);

            llmClient.RequestSuggestion(
                context,
                promptBuilder,
                suggestion => { llmSuggestion = LLMBehaviorSuggestion.ClampToWhitelist(suggestion); },
                error => { llmSuggestion = LLMBehaviorSuggestion.Default(); });
        }

        private void PushRecentEvent(string eventName)
        {
            string safeEventName = string.IsNullOrEmpty(eventName) ? "unknown_event" : eventName;
            for (int i = recentEvents.Length - 1; i > 0; i--)
            {
                recentEvents[i] = recentEvents[i - 1];
            }

            recentEvents[0] = safeEventName;
        }

        private void TickAnimation()
        {
            if (animationController == null)
            {
                return;
            }

            bool isMoving = navigationAgent != null && navigationAgent.IsMoving;
            float speed01 = navigationAgent != null ? navigationAgent.Speed01 : 0f;
            animationController.Tick(speed01, isMoving, snapshot.IsFocused, snapshot.userArousal);
        }

        private void Decide()
        {
            if (Time.time < actionHoldUntil)
            {
                return;
            }

            if (navigationAgent != null && navigationAgent.IsMoving && !navigationAgent.HasArrived())
            {
                currentState = snapshot.IsFocused ? CatBehaviorState.FocusedRoam : CatBehaviorState.Roam;
                return;
            }

            CatBehaviorDecision scoredDecision;
            if (behaviorScorer != null && behaviorScorer.TryDecide(
                    snapshot,
                    needModel != null ? needModel.Current : CatNeedState.CreateDefault(),
                    behaviorMemory,
                    GetSuggestion(),
                    out scoredDecision) &&
                scoredDecision.IsValid)
            {
                ApplyDecision(scoredDecision);
                return;
            }

            CatBehaviorState nextState = snapshot.IsFocused ? PickFocusState() : PickNonFocusState();
            ApplyState(nextState);
        }

        private CatBehaviorState PickNonFocusState()
        {
            float roam = Mathf.Max(0f, nonFocusRoamWeight + GetSuggestion().roamWeightBias * 100f);
            float sniff = Mathf.Max(0f, nonFocusSniffWeight);
            float lookBack = Mathf.Max(0f, nonFocusLookBackWeight);
            float tail = Mathf.Max(0f, nonFocusTailWagWeight + GetSuggestion().socialResponseWeightBias * 40f);
            float stretch = Mathf.Max(0f, nonFocusStretchWeight);
            float roll = Random.Range(0f, roam + sniff + lookBack + tail + stretch);

            if ((roll -= roam) <= 0f) return CatBehaviorState.Roam;
            if ((roll -= sniff) <= 0f) return CatBehaviorState.CuriousSniff;
            if ((roll -= lookBack) <= 0f) return CatBehaviorState.LookBack;
            if ((roll -= tail) <= 0f) return CatBehaviorState.TailWagHappy;
            return CatBehaviorState.StretchYawn;
        }

        private CatBehaviorState PickFocusState()
        {
            float roam = Mathf.Max(0f, focusRoamWeight + GetSuggestion().roamWeightBias * 70f);
            float idle = Mathf.Max(0f, focusIdleWeight + GetSuggestion().quietIdleWeightBias * 80f);
            float ear = Mathf.Max(0f, focusEarTwitchWeight);
            float alert = Mathf.Max(0f, focusAlertLookWeight);
            float tail = Mathf.Max(0f, focusTailWagWeight + GetSuggestion().socialResponseWeightBias * 20f);
            float roll = Random.Range(0f, roam + idle + ear + alert + tail);

            if ((roll -= roam) <= 0f) return CatBehaviorState.FocusedRoam;
            if ((roll -= idle) <= 0f) return CatBehaviorState.IdleBreath;
            if ((roll -= ear) <= 0f) return CatBehaviorState.EarTwitchAlert;
            if ((roll -= alert) <= 0f) return CatBehaviorState.AlertLook;
            return CatBehaviorState.TailWagHappy;
        }

        private void ApplyState(CatBehaviorState state)
        {
            currentState = state;
            bool focused = snapshot.IsFocused || state == CatBehaviorState.FocusedRoam;

            if (navigationAgent != null)
            {
                navigationAgent.SetSpeedMultiplier(navigationSpeedMultiplier);
                navigationAgent.Configure(focused);
            }

            if (animationController != null)
            {
                animationController.SetLocomotionPlaybackMultiplier(navigationSpeedMultiplier);
            }

            if (state == CatBehaviorState.Roam || state == CatBehaviorState.FocusedRoam)
            {
                if (!walkingEnabled)
                {
                    PlayIdleFallback();
                    return;
                }

                TryStartMove(state);
                return;
            }

            if (navigationAgent != null)
            {
                navigationAgent.StopSoft();
            }

            RouteAction(CatActionRequest.Create(
                state,
                focused ? CatActionSource.Recognition : CatActionSource.Ambient,
                focused ? "focused_state" : "ambient_state",
                focused ? 50 : 10,
                focused ? 12f : 6f,
                1f,
                CatActionInterruptPolicy.DropIfBusy,
                false));
        }

        private void ApplyDecision(CatBehaviorDecision decision)
        {
            currentState = decision.state;
            bool focused = snapshot.IsFocused || decision.state == CatBehaviorState.FocusedRoam;

            if (navigationAgent != null)
            {
                navigationAgent.SetSpeedMultiplier(navigationSpeedMultiplier);
                navigationAgent.Configure(focused);
            }

            if (animationController != null)
            {
                animationController.SetLocomotionPlaybackMultiplier(navigationSpeedMultiplier);
            }

            if (decision.IsLocomotion)
            {
                if (!walkingEnabled)
                {
                    PlayIdleFallback();
                    return;
                }

                if (TryStartMove(decision) && behaviorMemory != null)
                {
                    behaviorMemory.RecordDecision(decision, Time.time);
                }

                return;
            }

            if (navigationAgent != null)
            {
                navigationAgent.StopSoft();
            }

            RouteAction(CatActionRequest.Create(
                decision.state,
                focused ? CatActionSource.Recognition : CatActionSource.Ambient,
                decision.reason,
                decision.priority,
                decision.cooldownSeconds,
                1f,
                decision.interruptPolicy,
                decision.canInterruptByMove));
        }

        private bool TryStartMove(CatBehaviorState state)
        {
            return TryStartMove(CatBehaviorDecision.Create(
                state,
                0f,
                0f,
                0,
                CatActionInterruptPolicy.DropIfBusy,
                false,
                "legacy_move"));
        }

        private bool TryStartMove(CatBehaviorDecision decision)
        {
            if (navigationAgent == null || destinationPlanner == null)
            {
                PlayIdleFallback();
                return false;
            }

            Vector3 target;
            if (!destinationPlanner.TryPlanNext(
                    snapshot,
                    decision,
                    needModel != null ? needModel.Current : CatNeedState.CreateDefault(),
                    behaviorMemory,
                    transform.position,
                    out target))
            {
                PlayIdleFallback();
                return false;
            }

            if (!navigationAgent.TryMoveTo(target))
            {
                PlayIdleFallback();
                return false;
            }

            actionHoldUntil = 0f;
            if (animationController != null)
            {
                animationController.ForceLocomotion(true);
            }

            if (behaviorMemory != null)
            {
                behaviorMemory.RecordInterestPointVisit(destinationPlanner.LastPlannedInterestPointId);
            }

            return true;
        }

        private bool TryStartSceneInteractionMove(
            SceneInteractionPayload payload,
            SceneInteractionPoint point,
            bool focused)
        {
            if (!walkingEnabled || navigationAgent == null || destinationPlanner == null)
            {
                return false;
            }

            Transform anchor = point.NavigationAnchor;
            Vector3 requestedPoint = anchor != null ? anchor.position : payload.hitWorldPosition;
            navigationAgent.SetSpeedMultiplier(navigationSpeedMultiplier);
            navigationAgent.Configure(focused);

            Vector3 target;
            if (!destinationPlanner.TryPlanRequestedPoint(snapshot, requestedPoint, transform.position, out target))
            {
                return false;
            }

            if (!navigationAgent.TryMoveTo(target))
            {
                return false;
            }

            currentState = focused ? CatBehaviorState.FocusedRoam : CatBehaviorState.Roam;
            actionHoldUntil = 0f;
            if (animationController != null)
            {
                animationController.ForceLocomotion(true);
            }

            QueueSceneArrivalAction(point, focused);
            return true;
        }

        private void QueueSceneArrivalAction(SceneInteractionPoint point, bool focused)
        {
            if (actionRouter == null || point == null)
            {
                return;
            }

            CatBehaviorState arrivalState = point.PreferredCatState;
            if (arrivalState == CatBehaviorState.None ||
                arrivalState == CatBehaviorState.Roam ||
                arrivalState == CatBehaviorState.FocusedRoam)
            {
                arrivalState = focused ? CatBehaviorState.AlertLook : CatBehaviorState.CuriousSniff;
            }

            sceneInteractionMemory.RecordArrival(point.Id, point.PreferredAnimationTag, Time.time);
            CatActionRequest arrivalRequest = CatActionRequest.Create(
                arrivalState,
                CatActionSource.User,
                "scene_interaction_arrival",
                Mathf.Clamp(point.Priority, 40, 90),
                point.CooldownSeconds,
                3f,
                CatActionInterruptPolicy.QueueIfMoving,
                false);
            CatActionRequest playableRequest;
            actionRouter.TryRoute(arrivalRequest, true, false, out playableRequest);
        }

        private void TryPlayQueuedAction()
        {
            if (actionRouter == null)
            {
                return;
            }

            CatActionRequest playableRequest;
            if (actionRouter.TryPopReady(
                    navigationAgent != null && navigationAgent.IsMoving,
                    Time.time < actionHoldUntil,
                    out playableRequest))
            {
                PlayRoutedAction(playableRequest);
            }
        }

        private void RouteAction(CatActionRequest request)
        {
            if (actionRouter == null)
            {
                PlayRoutedAction(request);
                return;
            }

            CatActionRequest playableRequest;
            if (actionRouter.TryRoute(
                    request,
                    navigationAgent != null && navigationAgent.IsMoving,
                    Time.time < actionHoldUntil,
                    out playableRequest))
            {
                PlayRoutedAction(playableRequest);
            }
        }

        private void PlayRoutedAction(CatActionRequest request)
        {
            currentState = request.state;
            if (navigationAgent != null)
            {
                navigationAgent.StopSoft();
            }

            float holdSeconds = GetHoldSeconds(request.state, snapshot.IsFocused);
            actionHoldUntil = Time.time + holdSeconds;
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordState(request.state, Time.time, holdSeconds);
                behaviorMemory.SetCooldown(request.state, Time.time + request.cooldownSeconds);
            }

            if (animationController != null)
            {
                animationController.PlayAction(request.state, holdSeconds, request.canInterruptByMove);
            }
        }

        private void PlayIdleFallback()
        {
            currentState = CatBehaviorState.IdleBreath;
            actionHoldUntil = Time.time + shortActionSeconds;
            if (behaviorMemory != null)
            {
                behaviorMemory.RecordState(CatBehaviorState.IdleBreath, Time.time, shortActionSeconds);
                behaviorMemory.SetCooldown(CatBehaviorState.IdleBreath, Time.time + 1f);
            }

            if (animationController != null)
            {
                animationController.PlayAction(CatBehaviorState.IdleBreath, shortActionSeconds, true);
            }
        }

        private CatBehaviorState WeightedInteractionPick()
        {
            float roll = Random.value;
            if (roll < 0.75f) return CatBehaviorState.PawWave;
            if (roll < 0.9f) return CatBehaviorState.TailWagHappy;
            return CatBehaviorState.HeadTiltListen;
        }

        private float GetHoldSeconds(CatBehaviorState state, bool focused)
        {
            switch (state)
            {
                case CatBehaviorState.StretchYawn:
                    return focused ? mediumActionSeconds : longActionSeconds;
                case CatBehaviorState.CuriousSniff:
                case CatBehaviorState.HeadTiltListen:
                case CatBehaviorState.TailWagHappy:
                    return mediumActionSeconds;
                default:
                    return focused ? shortActionSeconds : mediumActionSeconds;
            }
        }

        private LLMBehaviorSuggestion GetSuggestion()
        {
            return llmSuggestion ?? LLMBehaviorSuggestion.Default();
        }
    }
}
