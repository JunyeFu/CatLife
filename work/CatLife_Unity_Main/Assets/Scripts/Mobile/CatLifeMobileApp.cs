using System;
using System.Collections.Generic;
using System.Linq;
using CatLife.LLM;
using CatLife.Mobile;
using CatLife.Recognition;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class CatLifeMobileApp : MonoBehaviour
{
    private const string DataKey = "CatLife.Mobile.Data.v1";
    [SerializeField] private GameObject viewRoot;
    [SerializeField] private CatLifeCameraDirector cameraDirector;
    [SerializeField] private CatLifeMobileRuntimeCoordinator runtimeCoordinator;

    private CatLifeAppData data;
    private CatLifeSessionController session;
    private MockCatLLMClient llm;
    private readonly Dictionary<string, GameObject> views = new Dictionary<string, GameObject>();
    private Text todayText;
    private Text timerText;
    private Text rewardText;
    private Text recordsText;
    private Text growthText;
    private Text settingsText;
    private Text exitInterventionValue;
    private Text debugText;
    private Text bubbleText;
    private InputField customMinutes;
    private int selectedMinutes;
    private float transitionEndsAt;
    private float bubbleUntil;
    private float bubbleReadyAt;
    private readonly Queue<float> focusTouches = new Queue<float>();
    private int titleTapCount;
    private float titleTapWindow;
    private bool clearArmed;
    private CatLifeSessionRecord currentReward;
    private bool rewardPresentationReady;
    private bool localActionReview;
    private CatLifeAutoFocusPolicy autoFocusPolicy;
    private bool autoTransition;
    private bool transitionCameraStarted;
    private Sprite roundedSolidSprite;
    private Sprite roundedOutlineSprite;
    private CatLifeMobileUiPresentation uiPresentation;
    private bool saveRecovery;
    private bool recoveryClearArmed;
    private bool clockRecovery;
    private readonly CatLifeSessionClock sessionClock = new CatLifeSessionClock();

    public string CurrentView { get; private set; }
    public CatLifeSessionPhase CurrentPhase => session.Phase;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        if (PlayerPrefs.HasKey(DataKey)) saveRecovery = !CatLifeDataJson.TryDeserialize(PlayerPrefs.GetString(DataKey), out data);
        if (data == null || saveRecovery) data = new CatLifeAppData();
        session = new CatLifeSessionController(data);
        long utcNow = Now();
        sessionClock.Anchor(utcNow, Time.realtimeSinceStartupAsDouble);
        clockRecovery = session.Phase == CatLifeSessionPhase.Focus && data.activeSession.targetEndAt - utcNow > data.activeSession.plannedSeconds;
        bool interruptedAi = false;
        foreach (CatLifeSessionRecord record in data.records)
            if (record.aiRequestStatus == "requesting") { record.aiRequestStatus = "failed"; interruptedAi = true; }
        if (interruptedAi) Save();
        selectedMinutes = data.settings.defaultMinutes;
        autoFocusPolicy = new CatLifeAutoFocusPolicy(Mathf.Max(1, data.settings.autoFocusAdaptationSeconds), .68f);
        llm = GetComponent<MockCatLLMClient>();
        if (runtimeCoordinator == null) runtimeCoordinator = FindFirstObjectByType<CatLifeMobileRuntimeCoordinator>();
        if (cameraDirector == null) cameraDirector = FindFirstObjectByType<CatLifeCameraDirector>();
        BindView();
    }

    private void Start()
    {
        if (saveRecovery) { runtimeCoordinator.SetBehaviorStatistics(false); ShowSaveRecovery(); return; }
        runtimeCoordinator.SetBehaviorStatistics(data.settings.localBehaviorStatsEnabled);
        if (clockRecovery || session.Phase == CatLifeSessionPhase.Focus && data.activeSession.targetEndAt - Now() > data.activeSession.plannedSeconds)
        { ShowClockRecovery(); return; }
        if (session.Phase == CatLifeSessionPhase.Focus) ShowFocus(true);
        else if (session.Phase == CatLifeSessionPhase.Transition) ShowTransition();
        else ShowHome(true);
        views["AdoptionPanel"].SetActive(!data.settings.catAdopted && session.Phase == CatLifeSessionPhase.Normal);
        RefreshAdoption();
    }

    private void Update()
    {
        if (saveRecovery || clockRecovery) return;
        long now = SessionNow();
        ObserveHomePointerActivity(Pointer.current != null &&
            (Pointer.current.press.isPressed || Pointer.current.press.wasReleasedThisFrame));
        EvaluateAutoFocus(runtimeCoordinator != null ? runtimeCoordinator.LatestRecognition : RecognitionSnapshot.CreateDefault(), Time.unscaledDeltaTime);
        if (views.TryGetValue("DebugPanel", out GameObject debugPanel) && debugPanel.activeSelf) RefreshDebug();
        if (bubbleText != null && bubbleText.transform.parent.gameObject.activeSelf && Time.unscaledTime >= bubbleUntil) bubbleText.transform.parent.gameObject.SetActive(false);
        if (session.Phase == CatLifeSessionPhase.Transition && !transitionCameraStarted && runtimeCoordinator.SessionPoseReady)
        {
            transitionCameraStarted = true;
            transitionEndsAt = Time.unscaledTime + CatLifeMobileCatPresenter.LieDownSeconds + .18f;
            cameraDirector?.Show(CatLifeSessionPhase.Transition);
        }
        if (session.Phase == CatLifeSessionPhase.Transition && runtimeCoordinator.SessionPreparationFailed)
        {
            CancelFocusPreparation();
            bubbleReadyAt = 0;
            ShowBubble("这里暂时不好落脚，稍后再试。");
        }
        else if (session.Phase == CatLifeSessionPhase.Transition && Time.unscaledTime >= transitionEndsAt && runtimeCoordinator.TransitionAnimationComplete) EnterFocus();
        else if (session.Phase == CatLifeSessionPhase.Focus)
        {
            timerText.text = FormatClock(session.RemainingSeconds(now));
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                if (data.settings.localBehaviorStatsEnabled) session.RecordTouch();
                runtimeCoordinator?.RecordFocusTouch();
                focusTouches.Enqueue(Time.unscaledTime);
                while (focusTouches.Count > 0 && Time.unscaledTime - focusTouches.Peek() > 1f) focusTouches.Dequeue();
                if (focusTouches.Count >= 4) { runtimeCoordinator?.NudgeCat(); ShowBubble("我在，慢慢来。"); focusTouches.Clear(); }
                Save();
            }
            if (session.TryComplete(now, out CatLifeSessionRecord record)) Complete(record);
        }
    }

    private void ObserveHomePointerActivity(bool active)
    {
        if (!active || session.Phase != CatLifeSessionPhase.Normal || CurrentView != "HomeHudLayer" ||
            !data.settings.localBehaviorStatsEnabled) return;
        autoFocusPolicy.ResetDwell();
        runtimeCoordinator.RecordUiEvent("home_pointer_activity");
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) uiPresentation?.insightReveal.StopForBackground();
        autoFocusPolicy?.ResetDwell();
        runtimeCoordinator?.RecordUiEvent(paused ? "app_pause" : "app_resume");
        if (saveRecovery || clockRecovery || session == null || session.Phase != CatLifeSessionPhase.Focus) return;
        if (paused) session.RecordBackground(SessionNow()); else session.RecordForeground(SessionNow());
        Save();
    }

    public void Configure(GameObject root, CatLifeCameraDirector director, CatLifeMobileRuntimeCoordinator runtime)
    {
        viewRoot = root;
        cameraDirector = director;
        runtimeCoordinator = runtime;
    }

    private void BindView()
    {
        uiPresentation = viewRoot.GetComponent<CatLifeMobileUiPresentation>();
        foreach (Transform child in viewRoot.GetComponentsInChildren<Transform>(true)) views[child.name] = child.gameObject;
        todayText = TextOf("TodayText"); timerText = TextOf("TimerText"); rewardText = TextOf("RewardText");
        recordsText = TextOf("RecordsText"); growthText = TextOf("GrowthText"); settingsText = TextOf("SettingsText");
        debugText = TextOf("DebugText"); bubbleText = TextOf("BubbleText"); customMinutes = views["CustomMinutes"].GetComponent<InputField>();
        roundedSolidSprite = views["SetupCard"].GetComponent<Image>().sprite;
        roundedOutlineSprite = views["ReminderButton"].GetComponent<Image>().sprite;
        Font font = todayText.font;
        if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans CJK SC", "Noto Sans SC", "Microsoft YaHei" }, 36);
        foreach (Text text in viewRoot.GetComponentsInChildren<Text>(true)) text.font = font;
        uiPresentation?.ApplyTypography();
        Text consentText = TextOf("AiConsentPanelText");
        consentText.resizeTextForBestFit = false;
        consentText.fontSize = 30;
        consentText.horizontalOverflow = HorizontalWrapMode.Wrap;
        consentText.text = "开启 AI 会后建议？\n\n仅发送本次时长、触控次数、\n后台次数和稳定度等聚合值。\n\n不会发送输入文字、触控轨迹、\n屏幕或其他应用内容。";
        Bind("TitleButton", TapTitle); Bind("StartButton", ShowSetup); Bind("GrowthButton", ShowGrowth); Bind("RecordsButton", ShowRecords); Bind("SettingsButton", ShowSettings);
        Bind("Minutes15", () => SelectMinutes(15)); Bind("Minutes25", () => SelectMinutes(25)); Bind("Minutes45", () => SelectMinutes(45));
        customMinutes.onEndEdit.AddListener(_ => ApplyCustomMinutes());
        Bind("ReminderButton", ToggleReminder); Bind("ReminderGentleButton", ToggleReminder); Bind("AiButton", ToggleAi); Bind("SetupCancel", ShowHome); Bind("SetupStart", BeginTransition);
        Bind("InterruptConfirm", ConfirmInterrupt); Bind("InterruptCancel", () => views["ExitConfirm"].SetActive(false));
        Bind("RewardHome", ReturnToTown); Bind("RewardAgain", ShowSetup);
        Bind("RecordsBack", ShowHome); Bind("GrowthBack", ShowHome); Bind("SettingsBack", ShowHome);
        Bind("SettingsDuration", CycleDefaultDuration); Bind("SettingsReminder", ToggleReminder); Bind("SettingsBehavior", ToggleBehaviorStats); Bind("SettingsAi", ToggleAi); Bind("SettingsRecords", ShowRecords); Bind("SettingsClear", ClearData);
        Bind("AiConsentAccept", AcceptAiConsent); Bind("AiConsentCancel", CancelAiConsent); Bind("DebugClose", () => views["DebugPanel"].SetActive(false)); Bind("ReviewerMinute", BeginReviewerMinute);
        Bind("ReviewTail", () => StartCoroutine(ReviewLocalAction("tail_wag")));
        Bind("ReviewWave", () => StartCoroutine(ReviewLocalAction("paw_wave")));
        Bind("ReviewRest", () => StartCoroutine(ReviewLocalAction("focus_rest")));
        Bind("ReviewStretch", () => StartCoroutine(ReviewLocalAction("stretch")));
        Bind("AutoFocusCancel", CancelAutoFocus); Bind("SettingsAutoFocus", CycleAutoFocusAdaptation);
        Bind("AdoptionConfirm", AdoptCat);
        Bind("RecoveryRetry", RetrySavedData);
        Bind("RecoveryExport", ExportSavedData);
        Bind("RecoveryClear", ConfirmRecoveryClear);
        Bind("ClockContinue", ContinueAfterClockChange);
        Bind("ClockInterrupt", InterruptAfterClockChange);
        Bind("AdoptionDuration", () => { CycleDefaultDuration(); RefreshAdoption(); });
        Bind("VisitFishShop", () => VisitLandmark(CatLifeLandmarkAction.FishShop));
        Bind("VisitRewardTree", () => VisitLandmark(CatLifeLandmarkAction.RewardTree));
        Bind("VisitPlaza", () => VisitLandmark(CatLifeLandmarkAction.Plaza));
        CatLifeSwipeToEnd swipe = views["SwipeTrack"].GetComponent<CatLifeSwipeToEnd>();
        var contactView = swipe.GetComponent<CatLifePawContactView>();
        exitInterventionValue = contactView != null ? TextOf("SettingsExitInterventionValue") : null;
        if (contactView != null)
        {
            runtimeCoordinator.BindExitContact(contactView, Camera.main);
            Bind("SettingsExitIntervention", ToggleExitIntervention);
        }
        if (uiPresentation != null)
        {
            if (contactView == null) Bind("SettingsExitIntervention", ToggleExitIntervention);
            Bind("SettingsPrivacyDetails", () => views["FigmaPrivacyPanel"].SetActive(true));
            Bind("FigmaPrivacyClose", () => views["FigmaPrivacyPanel"].SetActive(false));
        }
        swipe.ConfirmRequested += () => { if (session.Phase == CatLifeSessionPhase.Focus) views["ExitConfirm"].SetActive(true); };
        swipe.InteractionRecorded += () => runtimeCoordinator?.RecordUiEvent("ui_scroll");
        // The current production model has no exit clips, so it keeps direct sliding.
        swipe.ConfigureIntervention(() => session.CanShowExitIntervention && runtimeCoordinator.CanBeginExitGesture);
        swipe.InterventionStarted += () => { if (session.MarkExitInterventionShown()) Save(); };
        swipe.GestureChanged += (phase, progress) =>
        {
            if (session.Phase == CatLifeSessionPhase.Focus) runtimeCoordinator.UpdateExitGesture(phase, progress);
        };
        swipe.GestureCancelled += () => runtimeCoordinator.CancelExitGesture(session.Phase == CatLifeSessionPhase.Focus);
    }

    private void ShowHome(bool immediate = false)
    {
        localActionReview = false;
        TextOf("RewardTitle").text = "专注完成";
        views["RewardAgain"].SetActive(true);
        clearArmed = false;
        ShowOnly("HomeHudLayer");
        session.ReturnToTown();
        cameraDirector?.Show(CatLifeSessionPhase.Normal, immediate);
        runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Normal);
        RefreshHome();
        views["AdoptionPanel"].SetActive(!data.settings.catAdopted);
        if (data.records.Count == 0) ShowBubble("先不用急，我在这里。");
    }
    private void ShowHome() { ShowHome(false); }
    private void RefreshAdoption()
    {
        if (!views["CatNameInput"].GetComponent<InputField>().isFocused && string.IsNullOrEmpty(views["CatNameInput"].GetComponent<InputField>().text))
            views["CatNameInput"].GetComponent<InputField>().text = data.settings.catName;
        TextOf("AdoptionDurationText").text = $"默认专注 {data.settings.defaultMinutes} 分钟 · 点此切换";
    }
    private void AdoptCat()
    {
        string name = views["CatNameInput"].GetComponent<InputField>().text.Trim();
        if (name.Length == 0) { TextOf("AdoptionCopy").text = "先给猫咪取一个名字。"; return; }
        data.settings.catName = name;
        data.settings.catAdopted = true;
        Save(); views["AdoptionPanel"].SetActive(false);
        bubbleReadyAt = 0; ShowBubble($"我是{name}，陪你慢慢开始。");
    }

    public void OpenLandmark(CatLifeLandmarkAction action)
    {
        if (CurrentView != "HomeHudLayer" || !data.settings.catAdopted) return;
        if (action == CatLifeLandmarkAction.Records) { ShowRecords(); return; }
        if (action == CatLifeLandmarkAction.Growth) { ShowGrowth(); return; }
        runtimeCoordinator.RespondToLandmark(action);
        bubbleReadyAt = 0;
        ShowBubble(action == CatLifeLandmarkAction.FishShop ? "闻到小鱼的香味了。" : action == CatLifeLandmarkAction.RewardTree ? "每次专注，都是一点成长。" : "在广场上伸个懒腰。" );
    }
    private void VisitLandmark(CatLifeLandmarkAction action) { ShowHome(); OpenLandmark(action); }
    private void ShowSetup()
    {
        ShowOnly("SetupPanel");
        cameraDirector?.Show(CatLifeSessionPhase.Normal);
        cameraDirector?.SetBottomSheetCoverage(.46f);
        runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Normal);
        RefreshSetup();
    }
    private void BeginTransition()
    {
        autoTransition = false;
        StartTransition();
    }
    private void StartTransition()
    {
        data.settings.defaultMinutes = selectedMinutes;
        session.BeginTransition(selectedMinutes * 60, Now());
        Save(); ShowTransition();
    }
    private void ShowTransition()
    {
        ShowOnly("TransitionPanel"); transitionEndsAt = float.PositiveInfinity;
        transitionCameraStarted = false;
        views["AutoFocusCancel"].SetActive(autoTransition);
        TextOf("TransitionText").text = autoTransition ? "检测到你逐渐安静，准备进入专注……" : "慢慢趴好，准备开始……";
        cameraDirector?.Show(CatLifeSessionPhase.Normal); runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Transition);
    }
    private void BeginReviewerMinute() { selectedMinutes = 1; views["DebugPanel"].SetActive(false); BeginTransition(); }
    private System.Collections.IEnumerator ReviewLocalAction(string action)
    {
        if (session.Phase != CatLifeSessionPhase.Normal || CurrentView != "HomeHudLayer") yield break;
        localActionReview = true;
        currentReward = null;
        views["DebugPanel"].SetActive(false);
        ShowOnly("RewardPanel");
        views["RewardCard"].SetActive(true);
        views["RewardAgain"].SetActive(false);
        TextOf("RewardTitle").text = "本地动作预览";
        rewardText.text = CatLifeUnlocks.ReactionLabel(action) + "\n固定测试输入 · 非云端回应\n不发请求，不增加记录或奖励";
        uiPresentation?.ShowLocalAction();
        cameraDirector.Show(CatLifeSessionPhase.Reward);
        runtimeCoordinator.ApplyPhase(CatLifeSessionPhase.Reward);
        while (localActionReview && !runtimeCoordinator.WakeUpAnimationComplete) yield return null;
        if (!localActionReview) yield break;
        runtimeCoordinator.PlayAiReaction(action);
    }
    private void EnterFocus()
    {
        string source = autoTransition ? "auto" : "manual";
        session.EnterFocus(Now()); Save(); ShowFocus(false);
        Debug.Log($"[CatLifeRecognition] focus_enter source={source}");
        autoTransition = false;
    }
    public void EvaluateAutoFocus(RecognitionSnapshot recognition, float deltaSeconds)
    {
        if (session.Phase != CatLifeSessionPhase.Normal || CurrentView != "HomeHudLayer" || !data.settings.catAdopted || !data.settings.localBehaviorStatsEnabled || data.settings.autoFocusAdaptationSeconds <= 0)
        {
            autoFocusPolicy.ResetDwell();
            return;
        }
        if (autoFocusPolicy == null || !autoFocusPolicy.ShouldStart(session.Phase, recognition.attentionBand == AttentionBand.Stable, recognition.focusConfidence, deltaSeconds)) return;
        autoTransition = true;
        selectedMinutes = data.settings.defaultMinutes;
        Debug.Log($"[CatLifeRecognition] auto_transition band={recognition.attentionBand} confidence={recognition.focusConfidence:F2}");
        StartTransition();
    }
    private void CancelAutoFocus()
    {
        if (!autoTransition || session.Phase != CatLifeSessionPhase.Transition) return;
        Debug.Log("[CatLifeRecognition] auto_transition_cancelled");
        CancelFocusPreparation();
    }
    private void CancelFocusPreparation()
    {
        autoTransition = false;
        session.CancelTransition();
        Save(); ShowHome();
    }
    private void ShowFocus(bool immediate)
    {
        sessionClock.Anchor(Now(), Time.realtimeSinceStartupAsDouble);
        ShowOnly("FocusPanel"); timerText.text = FormatClock(session.RemainingSeconds(SessionNow()));
        cameraDirector?.Show(CatLifeSessionPhase.Focus, immediate); runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Focus);
    }
    private void Complete(CatLifeSessionRecord record)
    {
        record.endedAt = Now();
        views["ExitConfirm"].SetActive(false);
        record.localInsight = CatLifeInsightEngine.Create(record); currentReward = record; Save(); ShowOnly("RewardPanel");
        rewardPresentationReady = false;
        views["RewardCard"].SetActive(false);
        cameraDirector?.Show(CatLifeSessionPhase.Reward); runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Reward); RenderReward(record); RequestAiOnce(record);
        ShowBubble("专注完成，慢慢起身。");
        StartCoroutine(RevealRewardWhenReady(record));
    }
    private void PlayRewardCelebration() { runtimeCoordinator?.CelebrateReward(); }
    private System.Collections.IEnumerator RevealRewardWhenReady(CatLifeSessionRecord record)
    {
        while (currentReward == record && session.Phase == CatLifeSessionPhase.Reward && CurrentView == "RewardPanel" && !runtimeCoordinator.WakeUpAnimationComplete) yield return null;
        if (currentReward != record || session.Phase != CatLifeSessionPhase.Reward || CurrentView != "RewardPanel") yield break;
        rewardPresentationReady = true;
        views["RewardCard"].SetActive(true);
        RenderReward(record);
        bubbleText.transform.parent.gameObject.SetActive(false);
        if (!record.aiRequestAttempted) PlayRewardCelebration();
    }
    private void ConfirmInterrupt()
    {
        views["ExitConfirm"].SetActive(false);
        if (session.Phase != CatLifeSessionPhase.Focus) return;
        long now = SessionNow();
        if (session.TryComplete(now, out CatLifeSessionRecord completed))
        {
            Complete(completed);
            return;
        }
        CatLifeSessionRecord record = session.Interrupt(now); record.endedAt = Now(); record.localInsight = CatLifeInsightEngine.Create(record); Save(); ShowHome();
    }
    private void ReturnToTown() { ShowHome(); runtimeCoordinator?.ReturnCatHome(); }

    private void ShowRecords()
    {
        ShowOnly("RecordsPanel"); cameraDirector?.Show(CatLifeSessionPhase.Normal); CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        recordsText.supportRichText = true;
        recordsText.text = $"今日 <color=#F47D14>{stats.TodayMinutes} 分钟</color>  ·  完成 {stats.TodayCompletedCount} 次\n最长稳定 {(stats.TodayHasBehaviorStats ? FormatClock(stats.TodayLongestStableSeconds) : "暂无统计")}";
        TextOf("RecordsWeekTotal").text = $"近七日 {stats.SevenDayMinutes} 分钟 · 累计秒数汇总";
        TextOf("RecordsListText").text = RecentRecords();
        RenderSevenDayChart(stats.SevenDayMinutesByDay);
        uiPresentation?.RenderRecords(RecentRecords());
    }
    private void ShowGrowth()
    {
        ShowOnly("GrowthPanel"); cameraDirector?.Show(CatLifeSessionPhase.Normal); CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        growthText.supportRichText = true;
        TextOf("GrowthNameText").text = $"{data.settings.catName} · {runtimeCoordinator.CompanionStatus}";
        growthText.text = $"Lv.{stats.Level}   成长 <color=#F47D14>{stats.LevelProgress}/100</color>  ·  爪印 {stats.Paws}";
        string[] cards = { "GrowthActionWave", "GrowthActionTail", "GrowthActionLie", "GrowthActionStretch" };
        string[] labels = { "挥爪", CatLifeUnlocks.ReactionLabel("tail_wag"), "安静休息", "伸懒腰" };
        int[] required = { 1, 3, 5, 8 };
        for (int i = 0; i < cards.Length; i++)
        {
            bool unlocked = stats.Paws >= required[i];
            views[cards[i]].GetComponent<Image>().color = unlocked ? Color.white : new Color(.88f, .86f, .81f);
            views[cards[i] + "Icon"].GetComponent<Image>().color = unlocked ? Color.white : new Color(.48f, .47f, .44f, .55f);
            TextOf(cards[i] + "Text").text = labels[i] + (unlocked ? "\n已解锁" : $"\n{required[i]} 枚爪印解锁");
        }
        uiPresentation?.RenderGrowth(stats);
    }
    private void ShowSettings() { ShowOnly("SettingsPanel"); cameraDirector?.Show(CatLifeSessionPhase.Normal); RefreshSettings(); }

    private void SelectMinutes(int minutes) { selectedMinutes = minutes; RefreshSetup(); }
    private void ApplyCustomMinutes() { if (int.TryParse(customMinutes.text, out int value)) SelectMinutes(Mathf.Clamp(value, 1, 180)); }
    private void CycleDefaultDuration() { data.settings.defaultMinutes = data.settings.defaultMinutes == 15 ? 25 : data.settings.defaultMinutes == 25 ? 45 : 15; selectedMinutes = data.settings.defaultMinutes; Save(); RefreshSettings(); }
    private void ToggleReminder() { data.settings.reminderMode = data.settings.reminderMode == "quiet" ? "gentle" : "quiet"; Save(); RefreshSetup(); RefreshSettings(); }
    private void ToggleExitIntervention() { data.settings.catExitInterventionEnabled = !data.settings.catExitInterventionEnabled; Save(); RefreshSettings(); }
    private void ToggleBehaviorStats() { data.settings.localBehaviorStatsEnabled = !data.settings.localBehaviorStatsEnabled; autoFocusPolicy.ResetDwell(); runtimeCoordinator.SetBehaviorStatistics(data.settings.localBehaviorStatsEnabled); Save(); RefreshSettings(); }
    private void CycleAutoFocusAdaptation()
    {
        int current = data.settings.autoFocusAdaptationSeconds;
        data.settings.autoFocusAdaptationSeconds = current == 0 ? 30 : current == 30 ? 60 : current == 60 ? 120 : 0;
        autoFocusPolicy = new CatLifeAutoFocusPolicy(Mathf.Max(1, data.settings.autoFocusAdaptationSeconds), .68f);
        Save(); RefreshSettings();
    }
    private void ToggleAi()
    {
        if (!data.settings.aiEnabled && !data.settings.aiConsent) { views["AiConsentPanel"].SetActive(true); return; }
        data.settings.aiEnabled = !data.settings.aiEnabled; Save(); RefreshSetup(); RefreshSettings();
        if (!data.settings.aiEnabled) CancelAiRequests();
    }
    private void AcceptAiConsent() { data.settings.aiConsent = true; data.settings.aiEnabled = true; views["AiConsentPanel"].SetActive(false); Save(); RefreshSetup(); RefreshSettings(); }
    private void CancelAiConsent() { views["AiConsentPanel"].SetActive(false); }
    private void ClearData()
    {
        if (!clearArmed) { clearArmed = true; RefreshSettings(); return; }
        PlayerPrefs.DeleteKey(DataKey); data = new CatLifeAppData(); llm.CancelPending(); session = new CatLifeSessionController(data); selectedMinutes = data.settings.defaultMinutes; clearArmed = false;
        autoFocusPolicy = new CatLifeAutoFocusPolicy(1, .68f);
        runtimeCoordinator.SetBehaviorStatistics(true);
        currentReward = null; Save(); RefreshAdoption(); ShowHome();
    }
    private void TapTitle()
    {
        if (Time.unscaledTime > titleTapWindow) titleTapCount = 0;
        titleTapWindow = Time.unscaledTime + 2f;
        if (++titleTapCount < 5) return;
        titleTapCount = 0; CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        RefreshDebug();
        views["DebugPanel"].SetActive(true);
        runtimeCoordinator.LogNavigationSnapshot();
    }
    private void RefreshDebug()
    {
        RecognitionSnapshot recognition = runtimeCoordinator != null ? runtimeCoordinator.LatestRecognition : RecognitionSnapshot.CreateDefault();
        CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        debugText.text = $"会话 {session.Phase}\n注意 {recognition.attentionBand} / {recognition.attentionTrend}\n专注 {recognition.focusConfidence:0.00}  唤醒 {recognition.userArousal:0.00}\n风险 {recognition.interruptionRisk}\n事件 {recognition.safeLocalSummary}\n今日 {stats.TodayMinutes}m / 完成 {stats.TodayCompletedCount}\nAI {LatestAiSource()}\n构建 0.3.0 (3)";
        CatLifeSessionRecord latest = data.records.LastOrDefault();
        if (latest != null && latest.aiRequestAttempted)
        {
            debugText.fontSize = 23;
            debugText.resizeTextForBestFit = false;
            debugText.supportRichText = false;
            debugText.text += $"\n请求 {latest.aiRequestStatus} / 次数 {latest.aiRequestCount}\nHTTP {latest.aiStatusCode} / ID {latest.aiRequestId}\n{latest.aiAggregateSummary}\n推荐 {latest.aiRequestedAction} → {latest.aiReaction}\n动作 {latest.aiActionStatus}";
        }
    }

    private void RequestAiOnce(CatLifeSessionRecord record)
    {
        if (!data.settings.aiEnabled || !data.settings.aiConsent || !session.TryMarkAiRequestAttempted(record, "mimo_cloud_pending")) return;
        record.aiAggregateSummary = record.behaviorStatsDisabled
            ? $"时长={record.actualSeconds}s;行为统计未采集"
            : $"时长={record.actualSeconds}s;触控={record.touchCount};后台={record.backgroundCount};稳定={record.StabilityPercent}%";
        Save();
        RenderReward(record);
        CatLifeAppData requestData = data;
        CatPromptContext context = new CatPromptContext { behaviorStatsDisabled = record.behaviorStatsDisabled, focusSessionActive = false, focusState = "Reward", focusScore01 = record.StabilityPercent / 100f, distraction01 = Mathf.Clamp01(record.touchCount / 10f), userIntent = "PostSession", safeLocalSummary = record.aiAggregateSummary, privacyModeEnabled = true };
        llm.RequestSuggestion(context, new CatPromptBuilder(), suggestion => ApplyAiSuggestion(requestData, record, suggestion),
            error => { if (data != requestData || record.aiRequestStatus == "cancelled") return; record.aiSource = "local_template"; record.aiRequestStatus = error == "cancelled" ? "cancelled" : "failed"; Save(); if (currentReward == record) RenderReward(record); });
    }

    private void ApplyAiSuggestion(CatLifeAppData requestData, CatLifeSessionRecord record, LLMBehaviorSuggestion suggestion)
    {
            if (data != requestData || !data.settings.aiEnabled || !data.settings.aiConsent || record.aiRequestStatus == "cancelled") return;
            record.aiSource = llm.LastSource;
            record.aiRequestId = llm.LastCloudRequestId;
            record.aiStatusCode = llm.LastCloudStatusCode;
            bool complete = !string.IsNullOrWhiteSpace(suggestion.gentleReminder) && !string.IsNullOrWhiteSpace(suggestion.returnSuggestion) && !string.IsNullOrWhiteSpace(suggestion.catReactionReason);
            record.aiRequestStatus = llm.LastSource == "mimo_cloud" && complete ? "succeeded" : "local_fallback";
            if (record.aiRequestStatus == "succeeded")
            {
                record.aiAdvice = suggestion.suggestedLine;
                record.aiGentleReminder = suggestion.gentleReminder;
                record.aiReturnSuggestion = suggestion.returnSuggestion;
                record.aiReactionReason = suggestion.catReactionReason;
                record.aiRequestedAction = suggestion.recommendedLocalAction;
                record.aiReaction = CatLifeUnlocks.ResolveAiReaction(suggestion.recommendedLocalAction, CatLifeStats.Derive(data, Now()).Paws);
                record.aiActionStatus = string.IsNullOrEmpty(record.aiReaction) ? "no_action" : "selected";
                if (record.aiActionStatus == "selected") StartCoroutine(PlayAiRewardReaction(record));
            }
            Save(); if (currentReward == record) RenderReward(record);
    }

    private System.Collections.IEnumerator PlayAiRewardReaction(CatLifeSessionRecord record)
    {
        yield return new WaitForSecondsRealtime(CatLifeMobileCatPresenter.WakeUpSeconds + .18f);
        while (currentReward == record && session.Phase == CatLifeSessionPhase.Reward && CurrentView == "RewardPanel" && !rewardPresentationReady) yield return null;
        bool visible = currentReward == record && CurrentView == "RewardPanel" && session.Phase == CatLifeSessionPhase.Reward;
        record.aiActionStatus = !visible ? "skipped_page_left" : runtimeCoordinator != null && runtimeCoordinator.PlayAiReaction(record.aiReaction) ? "dispatched" : "failed";
        Save();
        if (record.aiActionStatus == "dispatched")
        {
            yield return new WaitForSecondsRealtime(.5f);
            while (currentReward == record && CurrentView == "RewardPanel" && runtimeCoordinator.IsAiReactionPlaying) yield return null;
            visible = currentReward == record && CurrentView == "RewardPanel" && session.Phase == CatLifeSessionPhase.Reward;
            var presenter = FindAnyObjectByType<CatLifeMobileCatPresenter>();
            record.aiActionStatus = !visible ? "skipped_page_left" : presenter.AiReactionCompleted ? "played" : "failed";
            Save();
        }
        Debug.Log($"[CatLife] ai_action request_id={record.aiRequestId} requested={record.aiRequestedAction} local={record.aiReaction} status={record.aiActionStatus}");
        if (visible) RenderReward(record);
    }

    private void CancelAiRequests()
    {
        foreach (var record in data.records)
            if (record.aiRequestStatus == "requesting") { record.aiRequestStatus = "cancelled"; record.aiSource = "local_template"; }
        llm.CancelPending();
        Save();
        if (currentReward != null) RenderReward(currentReward);
    }

    private void RefreshHome()
    {
        CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        todayText.supportRichText = true;
        todayText.text = $"今日专注 <color=#FF9A18>{stats.TodayMinutes}</color> 分钟";
    }
    private void RefreshSetup()
    {
        TextOf("SelectedMinutes").text = $"本次 {selectedMinutes} 分钟";
        TextOf("SetupStartText").text = $"开始 {selectedMinutes} 分钟";
        StyleMinuteChoice("Minutes15", 15);
        StyleMinuteChoice("Minutes25", 25);
        StyleMinuteChoice("Minutes45", 45);
        bool quiet = data.settings.reminderMode == "quiet";
        views["ReminderButton"].GetComponent<Image>().sprite = quiet ? roundedOutlineSprite : roundedSolidSprite;
        views["ReminderGentleButton"].GetComponent<Image>().sprite = quiet ? roundedSolidSprite : roundedOutlineSprite;
        TextOf("ReminderButtonText").color = quiet ? new Color(.86f, .39f, .05f) : new Color(.32f, .29f, .24f);
        TextOf("ReminderGentleButtonText").color = quiet ? new Color(.32f, .29f, .24f) : new Color(.86f, .39f, .05f);
        TextOf("ReminderButtonText").text = "安静陪伴";
        TextOf("AiButtonText").text = data.settings.aiEnabled ? "开" : "关";
        views["AiToggleTrack"].GetComponent<Image>().color = data.settings.aiEnabled ? new Color(.96f, .49f, .08f) : new Color(.72f, .70f, .66f);
        RectTransform knob = views["AiToggleKnob"].GetComponent<RectTransform>();
        knob.anchorMin = new Vector2(data.settings.aiEnabled ? .56f : .04f, .10f);
        knob.anchorMax = new Vector2(data.settings.aiEnabled ? .96f : .44f, .90f);
        uiPresentation?.RenderSetup(selectedMinutes, quiet, data.settings.aiEnabled, todayText.text);
    }

    private void StyleMinuteChoice(string name, int minutes)
    {
        bool selected = selectedMinutes == minutes;
        views[name].GetComponent<Image>().color = selected ? new Color(.96f, .49f, .08f) : Color.white;
        TextOf(name + "Text").color = selected ? Color.white : new Color(.32f, .29f, .24f);
    }
    private void RefreshSettings()
    {
        string autoFocus = data.settings.autoFocusAdaptationSeconds > 0 ? data.settings.autoFocusAdaptationSeconds + " 秒" : "关闭";
        settingsText.text = data.settings.aiEnabled ? "AI 会后建议已开启 · 仅发送会话聚合值" : "当前为本地模式 · 数据留在设备";
        TextOf("SettingsDurationText").text = $"默认时长                                      {data.settings.defaultMinutes} 分钟  ›";
        TextOf("SettingsReminderText").text = $"提醒模式                              {(data.settings.reminderMode == "quiet" ? "安静陪伴" : "轻提醒")}  ›";
        TextOf("SettingsBehaviorText").text = $"行为数据                              {(data.settings.localBehaviorStatsEnabled ? "仅本地统计" : "关闭")}  ›";
        TextOf("SettingsAutoFocusText").text = $"自动专注                                      {autoFocus}  ›";
        TextOf("SettingsAiText").text = $"AI 会后建议                                  {(data.settings.aiEnabled ? "开启" : "关闭")}  ›";
        TextOf("SettingsRecordsText").text = $"专注记录                                      {data.records.Count} 条  ›";
        TextOf("SettingsClearText").text = clearArmed ? "再次点击确认清除" : "清除本地数据";
        if (exitInterventionValue != null) exitInterventionValue.text = data.settings.catExitInterventionEnabled ? "开启" : "关闭";
        uiPresentation?.RenderSettings(data, clearArmed);
    }

    private void RenderSevenDayChart(int[] minutes)
    {
        int maximum = Mathf.Max(45, Mathf.CeilToInt(minutes.Max() / 15f) * 15);
        for (int line = 0; line < 4; line++)
            TextOf("ChartAxis" + line).text = (maximum * line / 3) + " 分";
        for (int i = 0; i < 7; i++)
        {
            TextOf("DayLabel" + i).text = DateTime.Now.Date.AddDays(i - 6).ToString("M/d");
            RectTransform bar = views["DayBar" + i].GetComponent<RectTransform>();
            float x = .18f + i * .11f;
            bar.gameObject.SetActive(minutes[i] > 0);
            bar.GetComponent<Image>().sprite = null;
            bar.anchorMin = new Vector2(x, .24f);
            bar.anchorMax = new Vector2(x + .06f, .24f + .48f * minutes[i] / maximum);
            RectTransform label = views["DayLabel" + i].GetComponent<RectTransform>();
            label.anchorMin = new Vector2(x - .025f, .04f);
            label.anchorMax = new Vector2(x + .085f, .17f);
        }
        uiPresentation?.RenderChart(minutes, maximum);
    }
    private void RenderReward(CatLifeSessionRecord record)
    {
        string insight = record.localInsight + "\n来源：本地洞察";
        if (record.aiRequestStatus == "requesting") insight = "正在生成陪伴建议……\n仅发送本次会话聚合值\n你可以随时返回小镇";
        else if (record.aiRequestStatus == "succeeded") insight = $"MiMo 生成 · 仅使用会话聚合值\n温和提醒：{record.aiGentleReminder}\n下一小步：{record.aiReturnSuggestion}\n猫咪回应：{CatLifeUnlocks.ReactionLabel(record.aiReaction)}\n{record.aiReactionReason}";
        else if (record.aiRequestAttempted) insight = record.localInsight + (record.aiRequestStatus == "cancelled" ? "\n本地洞察 · 本次 AI 建议已取消" : "\n本地洞察 · 本次 AI 建议未生成");
        if (uiPresentation != null)
        {
            uiPresentation.RenderReward(record, insight, CatLifeStats.Derive(data, Now()));
            return;
        }
        rewardText.fontSize = 30;
        rewardText.resizeTextForBestFit = false;
        rewardText.horizontalOverflow = HorizontalWrapMode.Wrap;
        rewardText.supportRichText = false;
        string behaviorSummary = record.behaviorStatsDisabled ? "行为统计未采集" : $"后台返回 {record.BackgroundCount} 次  ·  稳定度 {record.StabilityPercent}%";
        rewardText.text = $"完成 {record.ActualSeconds / 60} 分钟\n{behaviorSummary}\n成长 +{record.GrowthAwarded}  ·  爪印 +{record.PawsAwarded}\n\n{insight}";
    }
    private string RecentRecords()
    {
        if (data.records.Count == 0) return "还没有记录";
        return string.Join("\n", data.records.AsEnumerable().Reverse().Take(6).Select(r => $"{DateTimeOffset.FromUnixTimeSeconds(r.endedAt).ToLocalTime():MM-dd HH:mm}  {FormatClock(r.actualSeconds)}  {(r.completed ? "完成" : "中断")}"));
    }
    private string LatestAiSource() { return data.records.Count == 0 ? "not_requested" : data.records[data.records.Count - 1].aiSource ?? "not_requested"; }
    private void ShowBubble(string value)
    {
        if (Time.unscaledTime < bubbleReadyAt) return;
        bubbleText.supportRichText = false;
        bubbleText.text = value; bubbleText.gameObject.SetActive(true); bubbleText.transform.parent.gameObject.SetActive(true); bubbleUntil = Time.unscaledTime + 4.5f; bubbleReadyAt = Time.unscaledTime + 8f;
    }
    private void ShowOnly(string name)
    {
        if (CurrentView != name) autoFocusPolicy?.ResetDwell();
        foreach (string layer in new[] { "HomeHudLayer", "SetupPanel", "TransitionPanel", "FocusPanel", "RewardPanel", "RecordsPanel", "GrowthPanel", "SettingsPanel" }) views[layer].SetActive(layer == name);
        CatLife.Cat.CatUiInteractionBridge catBridge = viewRoot.GetComponentInChildren<CatLife.Cat.CatUiInteractionBridge>(true);
        if (catBridge != null) catBridge.enabled = name == "HomeHudLayer";
        if (name != "HomeHudLayer")
        {
            CatLife.UI.CatBubblePresenter presenter = viewRoot.GetComponentInChildren<CatLife.UI.CatBubblePresenter>(true);
            presenter?.Hide();
            if (bubbleText != null && bubbleText.transform.parent.gameObject.activeSelf) bubbleText.transform.parent.gameObject.SetActive(false);
        }
        CurrentView = name;
        runtimeCoordinator?.RecordUiEvent("page_enter_" + name);
    }
    private void Bind(string name, UnityEngine.Events.UnityAction action)
    {
        Button button = views[name].GetComponent<Button>();
        button.onClick.AddListener(() => runtimeCoordinator?.RecordUiTap("tap_" + name));
        button.onClick.AddListener(action);
    }
    private Text TextOf(string name) { return views[name].GetComponent<Text>(); }
    private void ShowSaveRecovery()
    {
        ShowOnly("SaveRecoveryPanel");
        views["AdoptionPanel"].SetActive(false);
        views["SaveRecoveryPanel"].SetActive(true);
        TextOf("SaveRecoveryText").text = "本地记录暂时无法读取\n原始数据仍保留，请选择重新读取或导出。\n清除需要再次确认。";
    }
    private void RetrySavedData()
    {
        recoveryClearArmed = false;
        if (!CatLifeDataJson.TryDeserialize(PlayerPrefs.GetString(DataKey), out CatLifeAppData loaded)) { ShowSaveRecovery(); return; }
        data = loaded; session = new CatLifeSessionController(data);
        long utcNow = Now(); sessionClock.Anchor(utcNow, Time.realtimeSinceStartupAsDouble);
        clockRecovery = session.Phase == CatLifeSessionPhase.Focus && data.activeSession.targetEndAt - utcNow > data.activeSession.plannedSeconds;
        foreach (var record in data.records) if (record.aiRequestStatus == "requesting") record.aiRequestStatus = "failed";
        selectedMinutes = data.settings.defaultMinutes;
        autoFocusPolicy = new CatLifeAutoFocusPolicy(Mathf.Max(1, data.settings.autoFocusAdaptationSeconds), .68f);
        saveRecovery = false; views["SaveRecoveryPanel"].SetActive(false); Save(); Start();
    }
    private void ExportSavedData()
    {
        recoveryClearArmed = false;
        string path = System.IO.Path.Combine(Application.persistentDataPath, "CatLife-recovery-original.txt");
        System.IO.File.WriteAllText(path, PlayerPrefs.GetString(DataKey));
        TextOf("SaveRecoveryText").text = "原始数据已导出到本机：\n" + path + "\n原存档未修改。";
    }
    private void ConfirmRecoveryClear()
    {
        if (!recoveryClearArmed) { recoveryClearArmed = true; TextOf("SaveRecoveryText").text = "清除后无法在应用内恢复。\n再次点击「确认清除」才会清除；\n点重新读取或导出可取消。"; return; }
        saveRecovery = false; views["SaveRecoveryPanel"].SetActive(false); clearArmed = true; ClearData();
    }
    private void ShowClockRecovery()
    {
        clockRecovery = true; ShowOnly("ClockRecoveryPanel"); views["ClockRecoveryPanel"].SetActive(true);
        views["AdoptionPanel"].SetActive(false);
    }
    private void ContinueAfterClockChange()
    {
        long now = Now(); var active = data.activeSession;
        active.focusStartedAt = now; active.targetEndAt = now + active.plannedSeconds; active.stableSegmentStartedAt = now;
        clockRecovery = false; views["ClockRecoveryPanel"].SetActive(false); Save(); ShowFocus(true);
    }
    private void InterruptAfterClockChange()
    {
        clockRecovery = false; views["ClockRecoveryPanel"].SetActive(false);
        var record = session.Interrupt(data.activeSession.focusStartedAt); record.endedAt = Now();
        record.localInsight = CatLifeInsightEngine.Create(record); Save(); ShowHome();
    }
    private long SessionNow() => session.Phase == CatLifeSessionPhase.Focus ? sessionClock.Now(Time.realtimeSinceStartupAsDouble) : Now();
    private void Save() { if (saveRecovery || clockRecovery) return; PlayerPrefs.SetString(DataKey, CatLifeDataJson.Serialize(data)); PlayerPrefs.Save(); }
    private static long Now() { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }
    private static string FormatClock(int seconds) { return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"); }
}
