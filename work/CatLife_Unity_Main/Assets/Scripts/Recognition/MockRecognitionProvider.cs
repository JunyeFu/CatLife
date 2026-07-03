using UnityEngine;

namespace CatLife.Recognition
{
    public sealed class MockRecognitionProvider : MonoBehaviour, IRecognitionProvider
    {
        [Header("Polling")]
        [SerializeField] private float pollIntervalSeconds = 0.5f;
        [SerializeField] private RealtimeFeatureEngine featureEngine;

        [Header("Mock State")]
        [SerializeField] private bool useRealtimeFeatures = true;
        [SerializeField] private bool cycleFocusStates;
        [SerializeField] private float cycleDurationSeconds = 18f;
        [SerializeField] private FocusState startupFocusState = FocusState.NonFocus;
        [SerializeField] private UserIntent startupUserIntent = UserIntent.None;
        [SerializeField] private InterruptionRisk startupInterruptionRisk = InterruptionRisk.Low;

        private RecognitionSnapshot latest;
        private float elapsedSincePoll;
        private float lastTapTime = -999f;
        private float lastLongPressTime = -999f;

        public bool IsReady { get; private set; }
        public float PollIntervalSeconds { get { return pollIntervalSeconds; } }
        public RecognitionSnapshot Latest { get { return latest; } }

        private void Awake()
        {
            ResolveFeatureEngine();
            Initialize();
        }

        public void Initialize()
        {
            latest = RecognitionSnapshot.CreateDefault();
            latest.focusState = startupFocusState;
            latest.userIntent = startupUserIntent;
            latest.interruptionRisk = startupInterruptionRisk;
            UpdateDerivedFields();
            IsReady = true;
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!IsReady)
            {
                Initialize();
            }

            elapsedSincePoll += unscaledDeltaTime;
            if (elapsedSincePoll < Mathf.Max(0.05f, pollIntervalSeconds))
            {
                return;
            }

            elapsedSincePoll = 0f;
            ResolveFeatureEngine();
            if (useRealtimeFeatures && featureEngine != null)
            {
                featureEngine.Tick(unscaledDeltaTime);
                ApplyRealtimeFeatures(featureEngine.Latest);
            }
            else if (cycleFocusStates)
            {
                float phase = Mathf.Repeat(Time.unscaledTime, Mathf.Max(1f, cycleDurationSeconds)) / Mathf.Max(1f, cycleDurationSeconds);
                latest.focusState = phase < 0.5f ? FocusState.NonFocus : FocusState.Focused;
                latest.userIntent = phase < 0.5f ? UserIntent.WantsInteraction : UserIntent.Busy;
                latest.interruptionRisk = phase < 0.5f ? InterruptionRisk.Low : InterruptionRisk.Medium;
            }

            UpdateDerivedFields();
        }

        public void SetFocusState(FocusState focusState)
        {
            latest.focusState = focusState;
            UpdateDerivedFields();
        }

        public void SetUserIntent(UserIntent userIntent)
        {
            latest.userIntent = userIntent;
            UpdateDerivedFields();
        }

        public void SetInterruptionRisk(InterruptionRisk interruptionRisk)
        {
            latest.interruptionRisk = interruptionRisk;
            UpdateDerivedFields();
        }

        public void NotifyCatTapped()
        {
            lastTapTime = Time.unscaledTime;
            latest.userIntent = UserIntent.WantsInteraction;
            latest.interactionReadiness = 1f;
            UpdateDerivedFields();
        }

        public void NotifyCatLongPressed()
        {
            lastLongPressTime = Time.unscaledTime;
            latest.userIntent = latest.IsFocused ? UserIntent.WantsQuiet : UserIntent.WantsInteraction;
            latest.interactionReadiness = latest.IsFocused ? 0.35f : 0.9f;
            UpdateDerivedFields();
        }

        private void ResolveFeatureEngine()
        {
            if (featureEngine != null)
            {
                return;
            }

            featureEngine = GetComponent<RealtimeFeatureEngine>();
            if (featureEngine == null)
            {
                featureEngine = FindAnyObjectByType<RealtimeFeatureEngine>();
            }
        }

        private void ApplyRealtimeFeatures(RealtimeFeatureSnapshot features)
        {
            latest.focusState = ResolveFocusState(features);
            latest.userIntent = ResolveUserIntent(features);
            latest.interruptionRisk = ResolveInterruptionRisk(features);
            latest.focusConfidence = features.focusScore01;
            latest.userArousal = features.arousal01;
            latest.interactionReadiness = Mathf.Clamp01(0.25f + features.arousal01 * 0.55f + features.distraction01 * 0.2f);
            latest.companionshipNeed = latest.userIntent == UserIntent.NeedsComfort ? 0.8f :
                latest.userIntent == UserIntent.WantsInteraction ? 0.55f : 0.25f;
            latest.safeLocalSummary = "features: " + features.localEventSummary;
        }

        private static FocusState ResolveFocusState(RealtimeFeatureSnapshot features)
        {
            if (!features.isFocusSessionActive)
            {
                return FocusState.NonFocus;
            }

            if (features.secondsSinceLastFocusStart < 2f)
            {
                return FocusState.TransitioningIn;
            }

            return features.focusScore01 >= 0.52f ? FocusState.Focused : FocusState.TransitioningOut;
        }

        private static UserIntent ResolveUserIntent(RealtimeFeatureSnapshot features)
        {
            if (features.isFocusSessionActive)
            {
                return features.distraction01 > 0.65f ? UserIntent.NeedsComfort : UserIntent.Busy;
            }

            if (features.tapRate1s >= 2f || features.pageSwitches30s >= 3)
            {
                return UserIntent.WantsInteraction;
            }

            return UserIntent.ObserveCat;
        }

        private static InterruptionRisk ResolveInterruptionRisk(RealtimeFeatureSnapshot features)
        {
            if (features.distraction01 >= 0.72f || features.arousal01 >= 0.85f)
            {
                return InterruptionRisk.High;
            }

            if (features.distraction01 >= 0.36f || features.arousal01 >= 0.45f)
            {
                return InterruptionRisk.Medium;
            }

            return InterruptionRisk.Low;
        }

        private void UpdateDerivedFields()
        {
            latest.realtimeSinceStartup = Time.realtimeSinceStartup;
            if (!useRealtimeFeatures || featureEngine == null)
            {
                latest.focusConfidence = latest.IsFocused ? 0.88f : 0.55f;
                latest.companionshipNeed = latest.userIntent == UserIntent.NeedsComfort ? 0.8f : 0.25f;
                latest.userArousal = latest.interruptionRisk == InterruptionRisk.High ? 0.9f :
                    latest.interruptionRisk == InterruptionRisk.Medium ? 0.55f : 0.2f;
            }

            latest.secondsSinceCatTap = Time.unscaledTime - lastTapTime;
            latest.secondsSinceCatLongPress = Time.unscaledTime - lastLongPressTime;
            if (!useRealtimeFeatures || featureEngine == null)
            {
                latest.safeLocalSummary = string.Format(
                    "focus={0}; intent={1}; risk={2}; tapAgo={3:F1}; longPressAgo={4:F1}",
                    latest.focusState,
                    latest.userIntent,
                    latest.interruptionRisk,
                    latest.secondsSinceCatTap,
                    latest.secondsSinceCatLongPress);
            }
        }
    }
}
