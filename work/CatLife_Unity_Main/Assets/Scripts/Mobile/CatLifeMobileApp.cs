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
    private CatLifeAutoFocusPolicy autoFocusPolicy;
    private bool autoTransition;

    public string CurrentView { get; private set; }
    public CatLifeSessionPhase CurrentPhase => session.Phase;

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Screen.orientation = ScreenOrientation.Portrait;
        data = PlayerPrefs.HasKey(DataKey) ? CatLifeDataJson.Deserialize(PlayerPrefs.GetString(DataKey)) : new CatLifeAppData();
        session = new CatLifeSessionController(data);
        selectedMinutes = data.settings.defaultMinutes;
        autoFocusPolicy = new CatLifeAutoFocusPolicy(Mathf.Max(1, data.settings.autoFocusAdaptationSeconds), .68f);
        llm = GetComponent<MockCatLLMClient>();
        if (runtimeCoordinator == null) runtimeCoordinator = FindFirstObjectByType<CatLifeMobileRuntimeCoordinator>();
        if (cameraDirector == null) cameraDirector = FindFirstObjectByType<CatLifeCameraDirector>();
        BindView();
        if (session.Phase == CatLifeSessionPhase.Focus) ShowFocus(true);
        else if (session.Phase == CatLifeSessionPhase.Transition) EnterFocus();
        else ShowHome(true);
    }

    private void Update()
    {
        long now = Now();
        if (session.Phase == CatLifeSessionPhase.Normal && data.settings.localBehaviorStatsEnabled && data.settings.autoFocusAdaptationSeconds > 0)
            EvaluateAutoFocus(runtimeCoordinator != null ? runtimeCoordinator.LatestRecognition : RecognitionSnapshot.CreateDefault(), Time.unscaledDeltaTime);
        if (views.TryGetValue("DebugPanel", out GameObject debugPanel) && debugPanel.activeSelf) RefreshDebug();
        if (bubbleText != null && bubbleText.transform.parent.gameObject.activeSelf && Time.unscaledTime >= bubbleUntil) bubbleText.transform.parent.gameObject.SetActive(false);
        if (session.Phase == CatLifeSessionPhase.Transition && Time.unscaledTime >= transitionEndsAt) EnterFocus();
        else if (session.Phase == CatLifeSessionPhase.Focus)
        {
            timerText.text = FormatClock(session.RemainingSeconds(now));
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                session.RecordTouch();
                runtimeCoordinator?.RecordFocusTouch();
                focusTouches.Enqueue(Time.unscaledTime);
                while (focusTouches.Count > 0 && Time.unscaledTime - focusTouches.Peek() > 1f) focusTouches.Dequeue();
                if (focusTouches.Count >= 4) { runtimeCoordinator?.NudgeCat(); ShowBubble("我在，慢慢来。"); focusTouches.Clear(); }
                Save();
            }
            if (session.TryComplete(now, out CatLifeSessionRecord record)) Complete(record);
        }
    }

    private void OnApplicationPause(bool paused)
    {
        runtimeCoordinator?.RecordUiEvent(paused ? "app_pause" : "app_resume");
        if (session == null || session.Phase != CatLifeSessionPhase.Focus) return;
        if (paused) session.RecordBackground(Now()); else session.RecordForeground(Now());
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
        foreach (Transform child in viewRoot.GetComponentsInChildren<Transform>(true)) views[child.name] = child.gameObject;
        todayText = TextOf("TodayText"); timerText = TextOf("TimerText"); rewardText = TextOf("RewardText");
        recordsText = TextOf("RecordsText"); growthText = TextOf("GrowthText"); settingsText = TextOf("SettingsText");
        debugText = TextOf("DebugText"); bubbleText = TextOf("BubbleText"); customMinutes = views["CustomMinutes"].GetComponent<InputField>();
        Font font = Font.CreateDynamicFontFromOSFont(new[] { "Noto Sans CJK SC", "Noto Sans SC", "Microsoft YaHei" }, 36);
        foreach (Text text in viewRoot.GetComponentsInChildren<Text>(true)) text.font = font;
        Bind("TitleButton", TapTitle); Bind("StartButton", ShowSetup); Bind("GrowthButton", ShowGrowth); Bind("RecordsButton", ShowRecords); Bind("SettingsButton", ShowSettings);
        Bind("Minutes15", () => SelectMinutes(15)); Bind("Minutes25", () => SelectMinutes(25)); Bind("Minutes45", () => SelectMinutes(45)); Bind("CustomApply", ApplyCustomMinutes);
        Bind("ReminderButton", ToggleReminder); Bind("AiButton", ToggleAi); Bind("SetupCancel", ShowHome); Bind("SetupStart", BeginTransition);
        Bind("InterruptConfirm", ConfirmInterrupt); Bind("InterruptCancel", () => views["ExitConfirm"].SetActive(false));
        Bind("RewardHome", ReturnToTown); Bind("RewardAgain", ShowSetup);
        Bind("RecordsBack", ShowHome); Bind("GrowthBack", ShowHome); Bind("SettingsBack", ShowHome);
        Bind("SettingsDuration", CycleDefaultDuration); Bind("SettingsReminder", ToggleReminder); Bind("SettingsBehavior", ToggleBehaviorStats); Bind("SettingsAi", ToggleAi); Bind("SettingsClear", ClearData);
        Bind("AiConsentAccept", AcceptAiConsent); Bind("AiConsentCancel", CancelAiConsent); Bind("DebugClose", () => views["DebugPanel"].SetActive(false)); Bind("ReviewerMinute", BeginReviewerMinute);
        Bind("AutoFocusCancel", CancelAutoFocus); Bind("SettingsAutoFocus", CycleAutoFocusAdaptation);
        CatLifeSwipeToEnd swipe = views["SwipeTrack"].GetComponent<CatLifeSwipeToEnd>();
        swipe.ConfirmRequested += () => views["ExitConfirm"].SetActive(true);
        swipe.InteractionRecorded += () => runtimeCoordinator?.RecordUiEvent("ui_scroll");
    }

    private void ShowHome(bool immediate = false)
    {
        ShowOnly("HomeHudLayer");
        session.ReturnToTown();
        cameraDirector?.Show(CatLifeSessionPhase.Normal, immediate);
        runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Normal);
        RefreshHome();
        if (data.records.Count == 0) ShowBubble("先不用急，我在这里。");
    }
    private void ShowHome() { ShowHome(false); }
    private void ShowSetup()
    {
        ShowOnly("SetupPanel");
        cameraDirector?.Show(CatLifeSessionPhase.Normal);
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
        Save(); ShowOnly("TransitionPanel"); transitionEndsAt = Time.unscaledTime + 2f;
        views["AutoFocusCancel"].SetActive(autoTransition);
        TextOf("TransitionText").text = autoTransition ? "检测到你逐渐安静，准备进入专注……" : "慢慢趴好，准备开始……";
        cameraDirector?.Show(CatLifeSessionPhase.Transition); runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Transition);
    }
    private void BeginReviewerMinute() { selectedMinutes = 1; views["DebugPanel"].SetActive(false); BeginTransition(); }
    private void EnterFocus()
    {
        string source = autoTransition ? "auto" : "manual";
        session.EnterFocus(Now()); Save(); ShowFocus(false);
        Debug.Log($"[CatLifeRecognition] focus_enter source={source}");
        autoTransition = false;
    }
    public void EvaluateAutoFocus(RecognitionSnapshot recognition, float deltaSeconds)
    {
        if (autoFocusPolicy == null || !autoFocusPolicy.ShouldStart(session.Phase, recognition.attentionBand == AttentionBand.Stable, recognition.focusConfidence, deltaSeconds)) return;
        autoTransition = true;
        selectedMinutes = data.settings.defaultMinutes;
        Debug.Log($"[CatLifeRecognition] auto_transition band={recognition.attentionBand} confidence={recognition.focusConfidence:F2}");
        StartTransition();
    }
    private void CancelAutoFocus()
    {
        if (!autoTransition || session.Phase != CatLifeSessionPhase.Transition) return;
        autoTransition = false;
        session.CancelTransition();
        Debug.Log("[CatLifeRecognition] auto_transition_cancelled");
        Save(); ShowHome();
    }
    private void ShowFocus(bool immediate)
    {
        ShowOnly("FocusPanel"); timerText.text = FormatClock(session.RemainingSeconds(Now()));
        cameraDirector?.Show(CatLifeSessionPhase.Focus, immediate); runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Focus);
    }
    private void Complete(CatLifeSessionRecord record)
    {
        record.localInsight = CatLifeInsightEngine.Create(record); currentReward = record; Save(); ShowOnly("RewardPanel");
        cameraDirector?.Show(CatLifeSessionPhase.Reward); runtimeCoordinator?.ApplyPhase(CatLifeSessionPhase.Reward); RenderReward(record); RequestAiOnce(record);
        Invoke(nameof(PlayRewardCelebration), 1.2f);
    }
    private void PlayRewardCelebration() { runtimeCoordinator?.CelebrateReward(); }
    private void ConfirmInterrupt()
    {
        CatLifeSessionRecord record = session.Interrupt(Now()); record.localInsight = CatLifeInsightEngine.Create(record); Save(); views["ExitConfirm"].SetActive(false); ShowHome();
    }
    private void ReturnToTown() { runtimeCoordinator?.ReturnCatHome(); ShowHome(); }

    private void ShowRecords()
    {
        ShowOnly("RecordsPanel"); cameraDirector?.Show(CatLifeSessionPhase.Normal); CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        string bars = string.Join("  ", stats.SevenDayMinutesByDay.Select(value => value == 0 ? "·" : new string('▮', Mathf.Clamp(value / 5 + 1, 1, 8))));
        recordsText.text = $"今日 {stats.TodayMinutes} 分钟  ·  完成 {stats.TodayCompletedCount} 次\n最长稳定 {FormatClock(stats.TodayLongestStableSeconds)}\n\n近七日\n{bars}\n\n最近会话\n{RecentRecords()}";
    }
    private void ShowGrowth()
    {
        ShowOnly("GrowthPanel"); cameraDirector?.Show(CatLifeSessionPhase.Normal); CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        string actions = string.Join("、", CatLifeUnlocks.GetUnlockedReactions(stats.Paws));
        growthText.text = $"Lv.{stats.Level}\n成长 {stats.LevelProgress}/100\n再获得 {stats.GrowthToNextLevel} 成长可升级\n爪印 {stats.Paws}\n\n已解锁动作\n{(string.IsNullOrEmpty(actions) ? "完成一次专注即可解锁挥爪" : actions)}";
    }
    private void ShowSettings() { ShowOnly("SettingsPanel"); cameraDirector?.Show(CatLifeSessionPhase.Normal); RefreshSettings(); }

    private void SelectMinutes(int minutes) { selectedMinutes = minutes; RefreshSetup(); }
    private void ApplyCustomMinutes() { if (int.TryParse(customMinutes.text, out int value)) SelectMinutes(Mathf.Clamp(value, 1, 180)); }
    private void CycleDefaultDuration() { data.settings.defaultMinutes = data.settings.defaultMinutes == 15 ? 25 : data.settings.defaultMinutes == 25 ? 45 : 15; selectedMinutes = data.settings.defaultMinutes; Save(); RefreshSettings(); }
    private void ToggleReminder() { data.settings.reminderMode = data.settings.reminderMode == "quiet" ? "gentle" : "quiet"; Save(); RefreshSetup(); RefreshSettings(); }
    private void ToggleBehaviorStats() { data.settings.localBehaviorStatsEnabled = !data.settings.localBehaviorStatsEnabled; Save(); RefreshSettings(); }
    private void CycleAutoFocusAdaptation()
    {
        int current = data.settings.autoFocusAdaptationSeconds;
        data.settings.autoFocusAdaptationSeconds = current == 0 ? 12 : current == 12 ? 30 : current == 30 ? 60 : 0;
        autoFocusPolicy = new CatLifeAutoFocusPolicy(Mathf.Max(1, data.settings.autoFocusAdaptationSeconds), .68f);
        Save(); RefreshSettings();
    }
    private void ToggleAi()
    {
        if (!data.settings.aiEnabled && !data.settings.aiConsent) { views["AiConsentPanel"].SetActive(true); return; }
        data.settings.aiEnabled = !data.settings.aiEnabled; Save(); RefreshSetup(); RefreshSettings();
    }
    private void AcceptAiConsent() { data.settings.aiConsent = true; data.settings.aiEnabled = true; views["AiConsentPanel"].SetActive(false); Save(); RefreshSetup(); RefreshSettings(); }
    private void CancelAiConsent() { views["AiConsentPanel"].SetActive(false); }
    private void ClearData()
    {
        if (!clearArmed) { clearArmed = true; RefreshSettings(); return; }
        PlayerPrefs.DeleteKey(DataKey); data = new CatLifeAppData(); session = new CatLifeSessionController(data); selectedMinutes = data.settings.defaultMinutes; clearArmed = false; ShowHome();
    }
    private void TapTitle()
    {
        if (Time.unscaledTime > titleTapWindow) titleTapCount = 0;
        titleTapWindow = Time.unscaledTime + 2f;
        if (++titleTapCount < 5) return;
        titleTapCount = 0; CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        RefreshDebug();
        views["DebugPanel"].SetActive(true);
    }
    private void RefreshDebug()
    {
        RecognitionSnapshot recognition = runtimeCoordinator != null ? runtimeCoordinator.LatestRecognition : RecognitionSnapshot.CreateDefault();
        CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now());
        debugText.text = $"会话 {session.Phase}\n注意 {recognition.attentionBand} / {recognition.attentionTrend}\n专注 {recognition.focusConfidence:0.00}  唤醒 {recognition.userArousal:0.00}\n风险 {recognition.interruptionRisk}\n事件 {recognition.safeLocalSummary}\n今日 {stats.TodayMinutes}m / 完成 {stats.TodayCompletedCount}\nAI {LatestAiSource()}\n构建 0.3.0 (3)";
    }

    private void RequestAiOnce(CatLifeSessionRecord record)
    {
        if (!data.settings.aiEnabled || !data.settings.aiConsent || !session.TryMarkAiRequestAttempted(record, "mimo_cloud_pending")) return;
        Save();
        CatPromptContext context = new CatPromptContext { focusSessionActive = false, focusState = "Reward", focusScore01 = record.StabilityPercent / 100f, distraction01 = Mathf.Clamp01(record.touchCount / 10f), arousal01 = 0.2f, userIntent = "PostSession" };
        llm.RequestSuggestion(context, new CatPromptBuilder(), suggestion =>
        {
            record.aiSource = llm.LastSource;
            if (llm.LastSource == "mimo_cloud") { record.aiAdvice = suggestion.suggestedLine; record.aiReaction = CatLifeUnlocks.SelectUnlockedReaction(suggestion.recommendedLocalAction, CatLifeStats.Derive(data, Now()).Paws); }
            Save(); if (currentReward == record) RenderReward(record);
        }, error => { record.aiSource = "failed"; Save(); if (currentReward == record) RenderReward(record); });
    }

    private void RefreshHome() { CatLifeDerivedStats stats = CatLifeStats.Derive(data, Now()); todayText.text = $"今日专注 {stats.TodayMinutes} 分钟  ·  爪印 {stats.Paws}"; }
    private void RefreshSetup()
    {
        TextOf("SelectedMinutes").text = $"本次 {selectedMinutes} 分钟";
        TextOf("ReminderButtonText").text = data.settings.reminderMode == "quiet" ? "安静陪伴" : "轻提醒";
        TextOf("AiButtonText").text = data.settings.aiEnabled ? "AI 建议：开" : "AI 建议：关";
    }
    private void RefreshSettings()
    {
        string autoFocus = data.settings.autoFocusAdaptationSeconds > 0 ? data.settings.autoFocusAdaptationSeconds + " 秒" : "关闭";
        settingsText.text = $"默认时长  {data.settings.defaultMinutes} 分钟\n提醒模式  {(data.settings.reminderMode == "quiet" ? "安静陪伴" : "轻提醒")}\n本地行为统计  {(data.settings.localBehaviorStatsEnabled ? "开启" : "关闭")}\n自动专注适应  {autoFocus}\nAI 会后建议  {(data.settings.aiEnabled ? "开启" : "关闭")}\n\n仅使用 App 内聚合事件；不采集文字、触点、屏幕内容或其他应用。";
        TextOf("SettingsClearText").text = clearArmed ? "再次点击确认清除" : "清除本地数据";
    }
    private void RenderReward(CatLifeSessionRecord record)
    {
        string ai = record.aiRequestAttempted ? $"\nAI 来源：{record.aiSource}\n{record.aiAdvice}" : "\n来源：本地洞察";
        rewardText.text = $"完成 {record.ActualSeconds / 60} 分钟\n后台返回 {record.BackgroundCount} 次  ·  稳定度 {record.StabilityPercent}%\n成长 +{record.GrowthAwarded}  ·  爪印 +{record.PawsAwarded}\n\n{record.localInsight}{ai}";
    }
    private string RecentRecords()
    {
        if (data.records.Count == 0) return "还没有记录";
        return string.Join("\n", data.records.AsEnumerable().Reverse().Take(6).Select(r => $"{DateTimeOffset.FromUnixTimeSeconds(r.endedAt).ToLocalTime():MM-dd HH:mm}  {r.actualSeconds / 60} 分钟  {(r.completed ? "完成" : "中断")}"));
    }
    private string LatestAiSource() { return data.records.Count == 0 ? "not_requested" : data.records[data.records.Count - 1].aiSource ?? "not_requested"; }
    private void ShowBubble(string value)
    {
        if (Time.unscaledTime < bubbleReadyAt) return;
        bubbleText.text = value; bubbleText.gameObject.SetActive(true); bubbleText.transform.parent.gameObject.SetActive(true); bubbleUntil = Time.unscaledTime + 4.5f; bubbleReadyAt = Time.unscaledTime + 8f;
    }
    private void ShowOnly(string name)
    {
        foreach (string layer in new[] { "HomeHudLayer", "SetupPanel", "TransitionPanel", "FocusPanel", "RewardPanel", "RecordsPanel", "GrowthPanel", "SettingsPanel" }) views[layer].SetActive(layer == name);
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
    private void Save() { PlayerPrefs.SetString(DataKey, CatLifeDataJson.Serialize(data)); PlayerPrefs.Save(); }
    private static long Now() { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }
    private static string FormatClock(int seconds) { return (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00"); }
}
