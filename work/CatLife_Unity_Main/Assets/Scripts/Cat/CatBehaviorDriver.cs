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
        private readonly string[] recentEvents = new string[4];

        private void Reset()
        {
            navigationAgent = GetComponent<CatNavigationAgent>();
            animationController = GetComponent<CatAnimationController>();
            destinationPlanner = GetComponent<CatDestinationPlanner>();
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
            MockRecognitionProvider mock = recognitionProvider as MockRecognitionProvider;
            if (mock != null)
            {
                mock.NotifyCatTapped();
            }

            PlayImmediateInteraction(WeightedInteractionPick());
        }

        public void NotifyCatLongPressed()
        {
            recentEvents[0] = "cat_long_press";
            MockRecognitionProvider mock = recognitionProvider as MockRecognitionProvider;
            if (mock != null)
            {
                mock.NotifyCatLongPressed();
            }

            PlayImmediateInteraction(snapshot.IsFocused ? CatBehaviorState.HeadTiltListen : CatBehaviorState.TailWagHappy);
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
                navigationAgent.Configure(focused);
            }

            if (state == CatBehaviorState.Roam || state == CatBehaviorState.FocusedRoam)
            {
                TryStartMove(state);
                return;
            }

            if (navigationAgent != null)
            {
                navigationAgent.StopSoft();
            }

            float holdSeconds = GetHoldSeconds(state, focused);
            actionHoldUntil = Time.time + holdSeconds;
            if (animationController != null)
            {
                animationController.PlayAction(state, holdSeconds, false);
            }
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

        private void PlayImmediateInteraction(CatBehaviorState state)
        {
            currentState = state;
            if (navigationAgent != null)
            {
                navigationAgent.StopSoft();
            }

            float holdSeconds = GetHoldSeconds(state, snapshot.IsFocused);
            actionHoldUntil = Time.time + holdSeconds;
            if (animationController != null)
            {
                animationController.PlayAction(state, holdSeconds, false);
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
