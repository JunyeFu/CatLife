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
        if (!behaviorOwnsAnimator) catPresenter?.ShowPhase(phase, LatestRecognition);
    }

    public void RecordUiEvent(string eventName) => featureEngine?.RecordUiEvent(eventName);
    public void RecordUiTap(string eventName) => featureEngine?.RecordUiTap(eventName);
    public void RecordFocusTouch() => featureEngine?.RecordUiEvent("focus_touch");
    public void NudgeCat() => catPresenter?.Nudge();
    public void CelebrateReward() => catPresenter?.CelebrateReward();
    public void ReturnCatHome() => catPresenter?.ReturnHome();
}
