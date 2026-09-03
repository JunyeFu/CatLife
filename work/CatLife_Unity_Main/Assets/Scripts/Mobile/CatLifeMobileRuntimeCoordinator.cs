using CatLife.Mobile;
using CatLife.Recognition;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CatLifeMobileRuntimeCoordinator : MonoBehaviour
{
    [SerializeField] private RealtimeFeatureEngine featureEngine;
    [SerializeField] private MockRecognitionProvider recognitionProvider;
    [SerializeField] private CatLifeMobileCatPresenter catPresenter;

    private bool hasAppliedPhase;
    private CatLifeSessionPhase appliedPhase;

    public RecognitionSnapshot LatestRecognition => recognitionProvider != null
        ? recognitionProvider.Latest
        : RecognitionSnapshot.CreateDefault();

    public void Configure(
        RealtimeFeatureEngine features,
        MockRecognitionProvider recognition,
        CatLifeMobileCatPresenter presenter)
    {
        featureEngine = features;
        recognitionProvider = recognition;
        catPresenter = presenter;
    }

    private void Update()
    {
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

        catPresenter?.ShowPhase(phase, LatestRecognition);
    }

    public void RecordUiEvent(string eventName) => featureEngine?.RecordUiEvent(eventName);
    public void RecordFocusTouch() => featureEngine?.RecordUiEvent("focus_touch");
    public void NudgeCat() => catPresenter?.Nudge();
    public void CelebrateReward() => catPresenter?.CelebrateReward();
    public void ReturnCatHome() => catPresenter?.ReturnHome();
}
