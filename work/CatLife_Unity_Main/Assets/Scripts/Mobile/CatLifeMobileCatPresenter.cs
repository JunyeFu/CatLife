using CatLife.Mobile;
using CatLife.Recognition;
using UnityEngine;

public sealed class CatLifeMobileCatPresenter : MonoBehaviour
{
    private const string SitDown = "CL_CAT_SitDownTransition_v01_72f";
    private const string SitIdle = "CL_CAT_SitIdle_v01_loop_96f";
    private const string LieDown = "CL_CAT_LieDownTransition_v01_120f";
    private const string FocusRest = "CL_CAT_FocusRest_v01_loop_96f";
    private const string FocusAttention = "CL_CAT_FocusAttention_v01_48f";
    private const string WakeUp = "CL_CAT_WakeUpTransition_v01_72f";
    private const string Celebrate = "CL_CAT_TailWagHappy_v01_loop_96f";
    private Animator animator;
    public RecognitionSnapshot LatestRecognition { get; private set; }

    private void Awake() { animator = GetComponentInChildren<Animator>(); }
    public void ShowPhase(CatLifeSessionPhase phase)
    {
        ShowPhase(phase, LatestRecognition);
    }
    public void ShowPhase(CatLifeSessionPhase phase, RecognitionSnapshot recognition)
    {
        LatestRecognition = recognition;
        string state = phase == CatLifeSessionPhase.Transition ? LieDown : phase == CatLifeSessionPhase.Focus ? FocusRest : phase == CatLifeSessionPhase.Reward ? WakeUp : SitIdle;
        CrossFade(state, .18f);
    }
    public void UpdateRecognition(RecognitionSnapshot recognition) { LatestRecognition = recognition; }
    public void Nudge() { CrossFade(FocusAttention, .08f); }
    public void ReturnHome() { CrossFade(SitDown, .12f); Invoke(nameof(PlaySitIdle), 1.2f); }
    public void CelebrateReward() { CrossFade(Celebrate, .12f); }
    private void PlaySitIdle() { CrossFade(SitIdle, .12f); }
    private void CrossFade(string state, float duration) { if (animator != null) animator.CrossFade(state, duration, 0); }
}
