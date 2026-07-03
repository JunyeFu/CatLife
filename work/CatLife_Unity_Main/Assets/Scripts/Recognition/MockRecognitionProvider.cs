using UnityEngine;

namespace CatLife.Recognition
{
    public sealed class MockRecognitionProvider : MonoBehaviour, IRecognitionProvider
    {
        [Header("Polling")]
        [SerializeField] private float pollIntervalSeconds = 0.5f;

        [Header("Mock State")]
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
            if (cycleFocusStates)
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

        private void UpdateDerivedFields()
        {
            latest.realtimeSinceStartup = Time.realtimeSinceStartup;
            latest.focusConfidence = latest.IsFocused ? 0.88f : 0.55f;
            latest.companionshipNeed = latest.userIntent == UserIntent.NeedsComfort ? 0.8f : 0.25f;
            latest.userArousal = latest.interruptionRisk == InterruptionRisk.High ? 0.9f :
                latest.interruptionRisk == InterruptionRisk.Medium ? 0.55f : 0.2f;
            latest.secondsSinceCatTap = Time.unscaledTime - lastTapTime;
            latest.secondsSinceCatLongPress = Time.unscaledTime - lastLongPressTime;
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
