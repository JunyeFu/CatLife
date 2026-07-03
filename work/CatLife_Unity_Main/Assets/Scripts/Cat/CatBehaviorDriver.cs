using CatLife.LLM;
using CatLife.Recognition;
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

        private void Reset()
        {
            navigationAgent = GetComponent<CatNavigationAgent>();
            animationController = GetComponent<CatAnimationController>();
            destinationPlanner = GetComponent<CatDestinationPlanner>();
            actionRouter = GetComponent<CatActionRouter>();
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

            ResolveFeatureEngine();
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

            TickLlm();
            TickAnimation();
            TryPlayQueuedAction();

            if (Time.time < nextDecisionTime)
            {
                return;
            }

            nextDecisionTime = Time.time + Mathf.Max(0.1f, decisionInterval);
            Decide();
        }

        public void NotifyCatTapped()
        {
            recentEvents[0] = "cat_tap";
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
                return;
            }

            snapshot.focusState = focused ? FocusState.Focused : FocusState.NonFocus;
        }

        public void SetContinuousWalking(bool enabled, float speedMultiplier)
        {
            walkingEnabled = enabled;
            navigationSpeedMultiplier = Mathf.Clamp(speedMultiplier, 0.1f, 3f);

            if (navigationAgent != null)
            {
                navigationAgent.SetSpeedMultiplier(navigationSpeedMultiplier);
                if (!walkingEnabled)
                {
                    navigationAgent.StopSoft();
                }
            }
        }

        public void NotifyCatLongPressed()
        {
            recentEvents[0] = "cat_long_press";
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

        public void NotifyUiAction(CatBehaviorState state, string reason)
        {
            if (state == CatBehaviorState.None)
            {
                return;
            }

            recentEvents[0] = reason;
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
            recentEvents[0] = "focus_started";
            if (featureEngine != null)
            {
                featureEngine.RecordFocusSessionStarted();
            }
        }

        public void NotifyFocusSessionEnded(bool completed)
        {
            recentEvents[0] = completed ? "focus_completed" : "focus_unlocked";
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

        private void TickLlm()
        {
            if (llmClient == null || !llmClient.Enabled || llmClient.IsBusy || Time.time < nextLlmTime)
            {
                return;
            }

            nextLlmTime = Time.time + Mathf.Max(5f, llmRefreshInterval);
            CatPromptContext context = CatPromptContext.Create(
                snapshot,
                currentState,
                navigationAgent != null ? navigationAgent.Speed01 : 0f,
                llmSuggestion != null ? llmSuggestion.moodBias : "calm",
                recentEvents);

            llmClient.RequestSuggestion(
                context,
                promptBuilder,
                suggestion => { llmSuggestion = LLMBehaviorSuggestion.ClampToWhitelist(suggestion); },
                error => { llmSuggestion = LLMBehaviorSuggestion.Default(); });
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

        private void TryStartMove(CatBehaviorState state)
        {
            if (navigationAgent == null || destinationPlanner == null)
            {
                PlayIdleFallback();
                return;
            }

            Vector3 target;
            if (!destinationPlanner.TryPlanNext(snapshot, state, transform.position, out target))
            {
                PlayIdleFallback();
                return;
            }

            if (!navigationAgent.TryMoveTo(target))
            {
                PlayIdleFallback();
                return;
            }

            actionHoldUntil = 0f;
            if (animationController != null)
            {
                animationController.ForceLocomotion(true);
            }
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
            if (animationController != null)
            {
                animationController.PlayAction(request.state, holdSeconds, request.canInterruptByMove);
            }
        }

        private void PlayIdleFallback()
        {
            currentState = CatBehaviorState.IdleBreath;
            actionHoldUntil = Time.time + shortActionSeconds;
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
