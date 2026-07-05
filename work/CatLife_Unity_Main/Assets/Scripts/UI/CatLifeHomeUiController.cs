using System;
using System.Globalization;
using System.Text;
using CatLife.Cat;
using CatLife.LLM;
using CatLife.Recognition;
using UnityEngine;
using UnityEngine.UI;

namespace CatLife.UI
{
    [DisallowMultipleComponent]
    public sealed class CatLifeHomeUiController : MonoBehaviour
    {
        private const string HighlightColor = "#FFD14A";
        private const string BrownColor = "#8F541C";
        private const string PrefPrefix = "CatLife.Home.";
        private const string LocalRecognitionKey = PrefPrefix + "LocalRecognitionEnabled";
        private const string SmartExplanationKey = PrefPrefix + "SmartExplanationEnabled";
        private const string FocusSessionSecondsKey = PrefPrefix + "FocusSessionSeconds";
        private const string AutoFocusDelaySecondsKey = PrefPrefix + "AutoFocusDelaySeconds";
        private const string SplashTextureResourcePath = "CatLifeSplash/CatLifeSplashLogo";
        private const int MinFocusSessionMinutes = 1;
        private const int MaxFocusSessionMinutes = 180;
        private const int MinAutoFocusDelaySeconds = 0;
        private const int MaxAutoFocusDelaySeconds = 3600;

        [SerializeField] private Text todayFocusText;
        [SerializeField] private Text focusPillText;
        [SerializeField] private Button startFocusButton;
        [SerializeField] private Button catButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private GameObject catButtonGroup;
        [SerializeField] private GameObject recordButtonGroup;
        [SerializeField] private GameObject settingsButtonGroup;
        [SerializeField] private GameObject rotateButtonGroup;
        [SerializeField] private Button closePlaceholderButton;
        [SerializeField] private GameObject placeholderOverlay;
        [SerializeField] private Text placeholderPageStatusText;
        [SerializeField] private Text placeholderTitleText;
        [SerializeField] private Text placeholderChipText;
        [SerializeField] private Text placeholderHeroText;
        [SerializeField] private Text placeholderBodyText;
        [SerializeField] private Image placeholderHeroIcon;
        [SerializeField] private Sprite catPageIcon;
        [SerializeField] private Sprite recordPageIcon;
        [SerializeField] private Sprite settingsPageIcon;
        [SerializeField] private CatBehaviorDriver catBehaviorDriver;
        [SerializeField] private CatTownWalker catWalker;
        [SerializeField] private FocusFeedbackProvider focusFeedbackProvider;
        [SerializeField] private CatBubblePresenter catBubblePresenter;
        [SerializeField] private GameObject startFocusButtonGroup;
        [SerializeField] private GameObject focusPillGroup;
        [SerializeField] private GameObject focusUnlockSliderGroup;
        [SerializeField] private FocusUnlockSlider focusUnlockSlider;
        [SerializeField] private int focusSessionSeconds = 1509;
        [SerializeField] private GameObject focusDurationSettingsRow;
        [SerializeField] private InputField focusDurationInput;
        [SerializeField] private Text focusDurationStatusText;
        [SerializeField] private GameObject autoFocusDelaySettingsRow;
        [SerializeField] private InputField autoFocusDelayInput;
        [SerializeField] private Text autoFocusDelayStatusText;
        [SerializeField] private float transitionSeconds = 6f;
        [SerializeField] private float rewardSeconds = 4f;
        [SerializeField] private float autoFocusDelaySeconds = 10f;
        [SerializeField] private bool autoEnterFocusAfterDelay = true;
        [SerializeField] private bool localRecognitionDefault = true;
        [SerializeField] private bool smartExplanationDefault;
        [SerializeField] private bool showSplashOnSceneStart = true;
        [SerializeField] private float splashHoldSeconds = 1.55f;
        [SerializeField] private float splashFadeSeconds = 0.45f;

        private int todayFocusMinutes;
        private int completedSessions;
        private int interruptionCount;
        private int longestStableSeconds;
        private int activeSessionSeconds;
        private float focusRemainingSeconds;
        private bool focusRunning;
        private bool autoFocusConsumed;
        private bool localRecognitionEnabled;
        private bool smartExplanationEnabled;
        private string currentDateKey;
        private float nextStatusRefreshTime;
        private float stateEnteredAt;
        private float playModeStartedAt;
        private bool listenersBound;
        private HomePage activePage;
        private FocusFlowState focusState = FocusFlowState.Normal;
        private string latestFocusFeedbackText = "完成第一段专注后生成猫咪反馈";
        private string latestFocusFeedbackSummary = "";
        private string latestFocusFeedbackSource = "local_template";
        private string latestFocusFeedbackTone = "warm";
        private string latestFocusFeedbackReaction = "idle_breath";
        private float latestFocusFeedbackConfidence = 1f;
        private string latestFocusFeedbackSafetyReason = "local";
        private bool latestFocusFeedbackDegraded = true;
        private GameObject splashOverlayRoot;
        private CanvasGroup splashCanvasGroup;
        private bool splashActive;
        private bool splashDismissed;
        private float splashStartedAt;
        private Sprite runtimeSplashSprite;

        private enum HomePage
        {
            None,
            Cat,
            Record,
            Settings
        }

        private enum FocusFlowState
        {
            Normal,
            Transition,
            Focus,
            Reward
        }

        private void Awake()
        {
            LoadRuntimeData();
            EnsureFocusUnlockSlider();
            EnsureCatBubblePresenter();
            activeSessionSeconds = Mathf.Max(1, focusSessionSeconds);
            focusRemainingSeconds = activeSessionSeconds;
            SetPlaceholderVisible(false);
            BeginSplashScreen();
            BeginRuntimeFocusDelay();
            UpdateStatusText(true);
            Debug.Log("[CatLife] startup package=com.catlife.mvp scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name +
                " focus_session_seconds=" + focusSessionSeconds +
                " today_focus_minutes=" + todayFocusMinutes);
        }

        private void OnEnable()
        {
            BindListeners();
            EnsureFocusUnlockSlider();
            EnsureCatBubblePresenter();
            BeginRuntimeFocusDelay();
            UpdateStatusText(true);
        }

        private void Start()
        {
            EnsureFocusUnlockSlider();
            EnsureCatBubblePresenter();
            BeginRuntimeFocusDelay();
            UpdateStatusText(true);
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        private void Update()
        {
            if (playModeStartedAt <= 0f)
            {
                BeginRuntimeFocusDelay();
            }

            UpdateAutoFocusDelay();
            UpdateFocusFlow();
            UpdateSplashScreen();

            if (!focusRunning)
            {
                if (Time.unscaledTime >= nextStatusRefreshTime)
                {
                    UpdateStatusText(false);
                }

                return;
            }

            focusRemainingSeconds = Mathf.Max(0f, focusRemainingSeconds - Time.unscaledDeltaTime);
            if (focusRemainingSeconds <= 0f)
            {
                CompleteFocusSession();
                return;
            }

            if (Time.unscaledTime >= nextStatusRefreshTime)
            {
                UpdateStatusText(false);
            }
        }

        public void StartFocusSession()
        {
            AndroidBehaviorEventBridge.RecordUnityEvent("UiButton", "start_focus");
            AndroidBehaviorEventBridge.RecordUnityEvent("FocusStart", "focus_session");
            Debug.Log("[CatLife] focus_start source=ui_or_auto focus_session_seconds=" + focusSessionSeconds +
                " today_focus_minutes=" + todayFocusMinutes);
            NotifyCatUiAction(CatBehaviorState.HeadTiltListen, "ui_start_focus");
            NotifyCatFocusSessionStarted();
            activeSessionSeconds = Mathf.Max(1, focusSessionSeconds);
            focusRemainingSeconds = activeSessionSeconds;
            focusRunning = true;
            autoFocusConsumed = true;
            SetPlaceholderVisible(false);
            ApplyFocusState(FocusFlowState.Focus, false);
            UpdateStatusText(true);
        }

        public void UnlockFocusSession()
        {
            if (focusState != FocusFlowState.Focus && !focusRunning)
            {
                ApplyFocusState(FocusFlowState.Normal, false);
                UpdateStatusText(true);
                return;
            }

            if (focusRunning)
            {
                interruptionCount += 1;
            }

            AndroidBehaviorEventBridge.RecordUnityEvent("Unlock", "focus_unlock");
            AndroidBehaviorEventBridge.RecordUnityEvent("FocusCancel", "focus_session");
            int interruptedSeconds = Mathf.Max(1, Mathf.RoundToInt(activeSessionSeconds - focusRemainingSeconds));
            Debug.Log("[CatLife] focus_unlocked elapsed_seconds=" + interruptedSeconds +
                " interruptions=" + interruptionCount +
                " llm_source=" + latestFocusFeedbackSource);
            focusRunning = false;
            NotifyCatFocusSessionEnded(false);
            autoFocusConsumed = true;
            focusRemainingSeconds = Mathf.Max(1, focusSessionSeconds);
            SaveRuntimeData();
            ApplyFocusState(FocusFlowState.Normal, false);
            UpdateStatusText(true);
            RefreshActivePage();
            RequestFocusFeedback(false, interruptedSeconds);
        }

        public void ShowCatPage()
        {
            ShowCatPage(true);
        }

        private void ShowCatPage(bool notifyCatAction)
        {
            activePage = HomePage.Cat;
            if (notifyCatAction)
            {
                AndroidBehaviorEventBridge.RecordUnityEvent("UiButton", "cat_button");
                AndroidBehaviorEventBridge.RecordUnityEvent("PageEnter", "cat_page");
                NotifyCatUiAction(CatBehaviorState.TailWagHappy, "ui_cat_page");
            }

            ShowPlaceholder(
                "猫咪",
                "猫咪状态",
                BuildCatHeroText(),
                catPageIcon,
                BuildCatPageBody());
        }

        public void ShowRecordPage()
        {
            ShowRecordPage(true);
        }

        private void ShowRecordPage(bool notifyCatAction)
        {
            activePage = HomePage.Record;
            if (notifyCatAction)
            {
                AndroidBehaviorEventBridge.RecordUnityEvent("UiButton", "record_button");
                AndroidBehaviorEventBridge.RecordUnityEvent("PageEnter", "record_page");
                NotifyCatUiAction(CatBehaviorState.HeadTiltListen, "ui_record_page");
            }

            ShowPlaceholder(
                "记录",
                "专注记录",
                "查看专注时长、奖励和最近 7 天趋势。",
                recordPageIcon,
                BuildRecordPageBody());
        }

        public void ShowSettingsPage()
        {
            ShowSettingsPage(true);
        }

        private void ShowSettingsPage(bool notifyCatAction)
        {
            activePage = HomePage.Settings;
            if (notifyCatAction)
            {
                AndroidBehaviorEventBridge.RecordUnityEvent("UiButton", "settings_button");
                AndroidBehaviorEventBridge.RecordUnityEvent("PageEnter", "settings_page");
                NotifyCatUiAction(CatBehaviorState.AlertLook, "ui_settings_page");
            }

            ShowPlaceholder(
                "设置",
                "识别与隐私",
                "管理本地识别、智能解释和数据操作边界。",
                settingsPageIcon,
                BuildSettingsPageBody());
            EnsureSettingsTimingRows();
            SyncSettingsTimingInputs();
            ApplySettingsPageLayout();
            SetGameObjectVisible(focusDurationSettingsRow, true);
            SetGameObjectVisible(autoFocusDelaySettingsRow, true);
        }

        public void HidePlaceholder()
        {
            AndroidBehaviorEventBridge.RecordUnityEvent("PageExit", activePage.ToString().ToLowerInvariant() + "_page");
            activePage = HomePage.None;
            SetGameObjectVisible(focusDurationSettingsRow, false);
            SetGameObjectVisible(autoFocusDelaySettingsRow, false);
            SetPlaceholderVisible(false);
        }

        private void CompleteFocusSession()
        {
            AndroidBehaviorEventBridge.RecordUnityEvent("FocusComplete", "focus_session");
            focusRunning = false;
            NotifyCatFocusSessionEnded(true);
            int finishedSeconds = Mathf.Max(1, activeSessionSeconds);
            int gainedMinutes = Mathf.Max(1, Mathf.RoundToInt(finishedSeconds / 60f));
            todayFocusMinutes += gainedMinutes;
            completedSessions += 1;
            longestStableSeconds = Mathf.Max(longestStableSeconds, finishedSeconds);
            focusRemainingSeconds = Mathf.Max(1, focusSessionSeconds);
            SaveRuntimeData();
            Debug.Log("[CatLife] focus_completed elapsed_seconds=" + finishedSeconds +
                " today_focus_minutes=" + todayFocusMinutes +
                " completed_sessions=" + completedSessions +
                " llm_source=" + latestFocusFeedbackSource);
            ApplyFocusState(FocusFlowState.Reward, false);
            NotifyCatUiAction(CatBehaviorState.PawWave, "session_completed");
            UpdateStatusText(true);
            RefreshActivePage();
            RequestFocusFeedback(true, finishedSeconds);
        }

        private void UpdateFocusFlow()
        {
            float elapsed = Time.unscaledTime - stateEnteredAt;
            if (focusState == FocusFlowState.Transition && elapsed >= Mathf.Max(0.1f, transitionSeconds))
            {
                ApplyFocusState(FocusFlowState.Focus, false);
            }
            else if (focusState == FocusFlowState.Reward && elapsed >= Mathf.Max(0.1f, rewardSeconds))
            {
                ApplyFocusState(FocusFlowState.Normal, false);
            }
        }

        private void UpdateAutoFocusDelay()
        {
            if (!autoEnterFocusAfterDelay || autoFocusConsumed || focusRunning || focusState != FocusFlowState.Normal)
            {
                return;
            }

            if (Time.realtimeSinceStartup - playModeStartedAt >= Mathf.Max(0f, autoFocusDelaySeconds))
            {
                StartFocusSession();
            }
        }

        private void BeginRuntimeFocusDelay()
        {
            playModeStartedAt = Time.realtimeSinceStartup;
            autoFocusConsumed = false;
            focusRunning = false;
            activeSessionSeconds = Mathf.Max(1, focusSessionSeconds);
            focusRemainingSeconds = activeSessionSeconds;
            ApplyFocusState(FocusFlowState.Normal, true);
        }

        private void BeginSplashScreen()
        {
            if (!showSplashOnSceneStart || splashDismissed)
            {
                return;
            }

            EnsureSplashOverlay();
            if (splashOverlayRoot == null || splashCanvasGroup == null)
            {
                splashDismissed = true;
                return;
            }

            splashStartedAt = Time.unscaledTime;
            splashActive = true;
            splashDismissed = false;
            splashCanvasGroup.alpha = 1f;
            splashCanvasGroup.blocksRaycasts = true;
            splashCanvasGroup.interactable = true;
            splashOverlayRoot.SetActive(true);
            splashOverlayRoot.transform.SetAsLastSibling();
        }

        private void UpdateSplashScreen()
        {
            if (!splashActive || splashCanvasGroup == null)
            {
                return;
            }

            float hold = Mathf.Clamp(splashHoldSeconds, 0.5f, 2.2f);
            float fade = Mathf.Clamp(splashFadeSeconds, 0.1f, 1f);
            float elapsed = Time.unscaledTime - splashStartedAt;
            if (elapsed <= hold)
            {
                splashCanvasGroup.alpha = 1f;
                return;
            }

            float fade01 = Mathf.Clamp01((elapsed - hold) / fade);
            splashCanvasGroup.alpha = 1f - fade01;
            if (fade01 >= 1f)
            {
                DismissSplashScreen();
            }
        }

        private void DismissSplashScreen()
        {
            splashActive = false;
            splashDismissed = true;
            if (splashCanvasGroup != null)
            {
                splashCanvasGroup.alpha = 0f;
                splashCanvasGroup.blocksRaycasts = false;
                splashCanvasGroup.interactable = false;
            }

            if (splashOverlayRoot != null)
            {
                splashOverlayRoot.SetActive(false);
            }
        }

        private void EnsureSplashOverlay()
        {
            if (splashOverlayRoot != null)
            {
                return;
            }

            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            GameObject root = new GameObject("CatLifeSplashOverlay", typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            root.transform.SetParent(canvasRect, false);
            RectTransform rootRect = root.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            Image background = root.GetComponent<Image>();
            background.color = Color.white;
            background.raycastTarget = true;

            Button skipButton = root.GetComponent<Button>();
            skipButton.transition = Selectable.Transition.None;
            skipButton.targetGraphic = background;
            skipButton.onClick.AddListener(DismissSplashScreen);

            splashCanvasGroup = root.GetComponent<CanvasGroup>();

            GameObject imageObject = new GameObject("CatLifeSplashImage", typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(root.transform, false);
            RectTransform imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;
            imageRect.pivot = new Vector2(0.5f, 0.5f);

            Image splashImage = imageObject.GetComponent<Image>();
            splashImage.sprite = LoadSplashSprite();
            splashImage.preserveAspect = true;
            splashImage.color = Color.white;
            splashImage.raycastTarget = false;

            splashOverlayRoot = root;
            splashOverlayRoot.SetActive(false);
        }

        private Sprite LoadSplashSprite()
        {
            if (runtimeSplashSprite != null)
            {
                return runtimeSplashSprite;
            }

            Texture2D texture = Resources.Load<Texture2D>(SplashTextureResourcePath);
            if (texture == null)
            {
                return null;
            }

            runtimeSplashSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            return runtimeSplashSprite;
        }

        private void ApplyFocusState(FocusFlowState nextState, bool force)
        {
            if (!force && focusState == nextState)
            {
                return;
            }

            focusState = nextState;
            stateEnteredAt = Time.unscaledTime;
            ApplyFocusStateUi();
            ApplyCatBehaviorForState();
            RefreshActivePage();
        }

        private void ApplyFocusStateUi()
        {
            EnsureFocusUnlockSlider();

            bool hideSideButtons = focusState == FocusFlowState.Focus;
            SetGameObjectVisible(ResolveMenuGroup(ref rotateButtonGroup, "MenuGroup_旋转", null), !hideSideButtons);
            SetGameObjectVisible(ResolveMenuGroup(ref startFocusButtonGroup, "StartFocusButton", startFocusButton), !hideSideButtons);
            SetGameObjectVisible(ResolveMenuGroup(ref focusPillGroup, "FocusPill", null), focusState == FocusFlowState.Focus);
            SetGameObjectVisible(ResolveMenuGroup(ref focusUnlockSliderGroup, "FocusUnlockSliderGroup", null), focusState == FocusFlowState.Focus);
            SetGameObjectVisible(ResolveMenuGroup(ref catButtonGroup, "MenuGroup_猫咪", catButton), !hideSideButtons);
            SetGameObjectVisible(ResolveMenuGroup(ref recordButtonGroup, "MenuGroup_记录", recordButton), !hideSideButtons);
            SetGameObjectVisible(ResolveMenuGroup(ref settingsButtonGroup, "MenuGroup_设置", settingsButton), !hideSideButtons);

            if (focusState == FocusFlowState.Focus && focusUnlockSliderGroup != null)
            {
                focusUnlockSliderGroup.transform.SetAsLastSibling();
            }
        }

        private void ApplyCatBehaviorForState()
        {
            float speedMultiplier = 1f;
            bool focused = false;

            switch (focusState)
            {
                case FocusFlowState.Transition:
                    speedMultiplier = 0.75f;
                    focused = true;
                    break;
                case FocusFlowState.Focus:
                    speedMultiplier = 0.5f;
                    focused = true;
                    break;
                case FocusFlowState.Reward:
                    speedMultiplier = 1f;
                    break;
            }

            CatBehaviorDriver behaviorDriver = ResolveCatBehaviorDriver();
            if (behaviorDriver != null && behaviorDriver.isActiveAndEnabled)
            {
                behaviorDriver.SetContinuousWalking(true, speedMultiplier);
                behaviorDriver.SetFocusMode(focused);
                return;
            }

            CatTownWalker walker = ResolveCatWalker();
            if (walker == null)
            {
                return;
            }

            walker.SetContinuousWalking(true, speedMultiplier);
        }

        private void LoadRuntimeData()
        {
            DateTime today = DateTime.Now.Date;
            currentDateKey = FormatDateKey(today);

            string todayMinutesKey = DailyMinutesKey(currentDateKey);
            todayFocusMinutes = PlayerPrefs.GetInt(todayMinutesKey, 0);
            completedSessions = PlayerPrefs.GetInt(DailySessionsKey(currentDateKey), EstimateCompletedSessions(todayFocusMinutes));
            interruptionCount = PlayerPrefs.GetInt(DailyInterruptionsKey(currentDateKey), 0);
            longestStableSeconds = PlayerPrefs.GetInt(DailyLongestKey(currentDateKey), EstimateLongestStableSeconds(todayFocusMinutes));
            localRecognitionEnabled = PlayerPrefs.GetInt(LocalRecognitionKey, localRecognitionDefault ? 1 : 0) == 1;
            smartExplanationEnabled = PlayerPrefs.GetInt(SmartExplanationKey, smartExplanationDefault ? 1 : 0) == 1;
            focusSessionSeconds = ClampFocusSessionSeconds(PlayerPrefs.GetInt(FocusSessionSecondsKey, focusSessionSeconds));
            int defaultAutoFocusDelay = autoEnterFocusAfterDelay ? Mathf.RoundToInt(autoFocusDelaySeconds) : 0;
            autoFocusDelaySeconds = ClampAutoFocusDelaySeconds(PlayerPrefs.GetInt(AutoFocusDelaySecondsKey, defaultAutoFocusDelay));
            autoEnterFocusAfterDelay = autoFocusDelaySeconds > 0.5f;
        }

        private void SaveRuntimeData()
        {
            if (string.IsNullOrEmpty(currentDateKey))
            {
                currentDateKey = FormatDateKey(DateTime.Now.Date);
            }

            PlayerPrefs.SetInt(DailyMinutesKey(currentDateKey), Mathf.Max(0, todayFocusMinutes));
            PlayerPrefs.SetInt(DailySessionsKey(currentDateKey), Mathf.Max(0, completedSessions));
            PlayerPrefs.SetInt(DailyInterruptionsKey(currentDateKey), Mathf.Max(0, interruptionCount));
            PlayerPrefs.SetInt(DailyLongestKey(currentDateKey), Mathf.Max(0, longestStableSeconds));
            PlayerPrefs.SetInt(LocalRecognitionKey, localRecognitionEnabled ? 1 : 0);
            PlayerPrefs.SetInt(SmartExplanationKey, smartExplanationEnabled ? 1 : 0);
            PlayerPrefs.SetInt(FocusSessionSecondsKey, ClampFocusSessionSeconds(focusSessionSeconds));
            PlayerPrefs.SetInt(AutoFocusDelaySecondsKey, Mathf.RoundToInt(ClampAutoFocusDelaySeconds(autoFocusDelaySeconds)));
            PlayerPrefs.Save();
        }

        private void UpdateStatusText(bool force)
        {
            if (!force && Time.unscaledTime < nextStatusRefreshTime)
            {
                return;
            }

            nextStatusRefreshTime = Time.unscaledTime + 0.25f;

            SetTodayFocusLabel(todayFocusText);
            SetTodayFocusLabel(placeholderPageStatusText);

            if (focusPillText != null)
            {
                focusPillText.supportRichText = true;
                string status = GetFocusStateLabel();
                focusPillText.text = status + " <color=" + HighlightColor + ">" + FormatClock(focusRemainingSeconds) + "</color>";
            }
        }

        private void RefreshActivePage()
        {
            if (activePage == HomePage.Cat)
            {
                ShowCatPage(false);
            }
            else if (activePage == HomePage.Record)
            {
                ShowRecordPage(false);
            }
            else if (activePage == HomePage.Settings)
            {
                ShowSettingsPage(false);
            }
        }

        private void SetTodayFocusLabel(Text label)
        {
            if (label == null)
            {
                return;
            }

            label.supportRichText = true;
            label.text = "今天已专注 <color=" + HighlightColor + ">" + todayFocusMinutes + "</color> 分钟";
        }

        private string BuildCatHeroText()
        {
            if (FindCatObject() == null)
            {
                return "未在当前场景找到猫咪模型，检查 CatCompanionModel 是否已加载。";
            }

            return focusState == FocusFlowState.Focus ? "专注中保持低打扰陪伴，猫咪会慢速巡游。" : "查看当前陪伴状态、成长值和已解锁动作。";
        }

        private string GetFocusStateLabel()
        {
            switch (focusState)
            {
                case FocusFlowState.Transition:
                    return "回收中";
                case FocusFlowState.Focus:
                    return "专注中";
                case FocusFlowState.Reward:
                    return "奖励中";
                default:
                    return "准备中";
            }
        }

        private string GetCatMoodText()
        {
            switch (focusState)
            {
                case FocusFlowState.Transition:
                    return "靠近陪伴";
                case FocusFlowState.Focus:
                    return "低打扰陪伴";
                case FocusFlowState.Reward:
                    return "开心反馈";
                default:
                    return "安静陪伴";
            }
        }

        private string BuildCatActionText()
        {
            switch (focusState)
            {
                case FocusFlowState.Transition:
                    return "慢速靠近 / Walk / 观察";
                case FocusFlowState.Focus:
                    return "低速巡游 / Walk / 呼吸 / 摆尾";
                case FocusFlowState.Reward:
                    return "快速巡游 / 开心反馈 / Walk";
                default:
                    return "连续行走 / Walk / 呼吸 / 摆尾";
            }
        }

        private string BuildCatPageBody()
        {
            int growthValue = Mathf.Clamp(todayFocusMinutes, 0, 100);
            int companionLevel = Mathf.Max(1, 1 + GetRecentSevenDayTotalMinutes() / 120);
            string catPosition = ResolveCatPositionText();
            string actionText = BuildCatActionText();
            string nextGoal = GetNextGoalText();

            StringBuilder body = new StringBuilder(512);
            body.AppendLine(ColorTitle("当前状态"));
            body.AppendLine("心情：" + GetCatMoodText());
            body.AppendLine("位置：" + catPosition);
            body.AppendLine("动作：" + actionText);
            body.AppendLine();
            body.AppendLine(ColorTitle("成长反馈"));
            body.AppendLine("陪伴等级：Lv." + companionLevel);
            body.AppendLine("成长值：" + growthValue + " / 100");
            body.AppendLine("已解锁：" + BuildUnlockedActionText());
            body.AppendLine("下一目标：" + nextGoal);
            body.AppendLine();
            body.AppendLine(ColorTitle("交互预留"));
            body.AppendLine("轻点猫咪：短反馈动作");
            body.AppendLine("长按猫咪：亲密互动");
            body.AppendLine("专注中：动作放慢，减少打扰");
            return body.ToString();
        }

        private string BuildRecordPageBody()
        {
            StringBuilder body = new StringBuilder(640);
            body.AppendLine(ColorTitle("今日概览"));
            body.AppendLine("已专注：" + todayFocusMinutes + " 分钟");
            body.AppendLine("完成段数：" + completedSessions + " 段");
            body.AppendLine("最长稳定：" + FormatMinutesOrSeconds(longestStableSeconds));
            body.AppendLine("中断次数：" + interruptionCount + " 次");
            body.AppendLine();
            body.AppendLine(ColorTitle("最近 7 天"));
            body.AppendLine(BuildRecentSevenDayText());
            body.AppendLine();
            body.AppendLine(ColorTitle("游戏化反馈"));
            body.AppendLine("奖励：星星果 x " + Mathf.Max(0, completedSessions));
            body.AppendLine("小镇变化：" + BuildTownFeedbackText());
            body.AppendLine("洞察：" + BuildFocusInsightText());
            body.AppendLine();
            body.AppendLine(ColorTitle("猫咪反馈"));
            body.AppendLine(latestFocusFeedbackText);
            if (!string.IsNullOrEmpty(latestFocusFeedbackSummary))
            {
                body.AppendLine("摘要：" + latestFocusFeedbackSummary);
            }

            body.AppendLine("动作提示：" + latestFocusFeedbackReaction);
            body.AppendLine("语气：" + latestFocusFeedbackTone + " / 置信度：" + latestFocusFeedbackConfidence.ToString("0.00"));
            body.AppendLine("来源：" + GetFeedbackSourceLabel(latestFocusFeedbackSource));
            return body.ToString();
        }

        private string BuildSettingsPageBody()
        {
            StringBuilder body = new StringBuilder(640);
            body.AppendLine(ColorTitle("识别与智能"));
            body.AppendLine("本地行为识别：" + BoolText(localRecognitionEnabled));
            body.AppendLine("智能解释：" + BoolText(smartExplanationEnabled));
            body.AppendLine("大模型建议：" + (smartExplanationEnabled ? "根据用户主动开启后的会话摘要生成建议" : "关闭，仅保留本地统计"));
            body.AppendLine("反馈降级状态：" + (latestFocusFeedbackDegraded ? "本地安全模板" : "智能反馈"));
            body.AppendLine("反馈安全状态：" + latestFocusFeedbackSafetyReason);
            body.AppendLine();
            body.AppendLine(ColorTitle("隐私边界"));
            body.AppendLine("不录屏");
            body.AppendLine("不读取输入内容");
            body.AppendLine("不跨 App 监控");
            body.AppendLine("默认只保存专注时长、打断次数、猫咪反馈状态");
            body.AppendLine("隐私网关：仅允许聚合时长、分数、次数和猫咪状态序列");
            body.AppendLine();
            body.AppendLine(ColorTitle("数据操作"));
            body.AppendLine("当前数据日期：" + FormatDisplayDate(DateTime.Now.Date));
            body.AppendLine("本地记录：" + todayFocusMinutes + " 分钟 / " + completedSessions + " 段 / " + interruptionCount + " 次中断");
            body.AppendLine("导出专注摘要：" + (todayFocusMinutes > 0 ? "已有可导出的今日摘要" : "暂无今日数据"));
            body.AppendLine();
            body.AppendLine(ColorTitle("每轮专注"));
            body.AppendLine("当前设置：      分钟");
            body.AppendLine(focusRunning ? "当前轮结束后生效" : "下一轮立即生效");
            body.AppendLine("可输入 1-" + MaxFocusSessionMinutes + " 分钟，下一轮专注生效。");
            body.AppendLine();
            body.AppendLine(ColorTitle("自动进入专注"));
            body.AppendLine("当前设置：      秒");
            body.AppendLine(autoEnterFocusAfterDelay ? "进入页面后自动计时" : "已关闭自动进入");
            body.AppendLine("可输入 0-" + MaxAutoFocusDelaySeconds + " 秒，0 表示关闭自动进入。");
            return body.ToString();
        }

        private void ShowPlaceholder(string title, string chip, string hero, Sprite icon, string body)
        {
            ApplyPlaceholderCompactLayout();

            if (placeholderTitleText != null)
            {
                placeholderTitleText.supportRichText = true;
                placeholderTitleText.text = title;
            }

            if (placeholderChipText != null)
            {
                placeholderChipText.supportRichText = true;
                placeholderChipText.text = chip;
            }

            if (placeholderHeroText != null)
            {
                placeholderHeroText.supportRichText = true;
                placeholderHeroText.text = hero;
            }

            if (placeholderHeroIcon != null && icon != null)
            {
                placeholderHeroIcon.sprite = icon;
            }

            if (placeholderBodyText != null)
            {
                placeholderBodyText.supportRichText = true;
                placeholderBodyText.text = body;
            }

            SetPlaceholderVisible(true);
            SetGameObjectVisible(focusDurationSettingsRow, activePage == HomePage.Settings);
            SetGameObjectVisible(autoFocusDelaySettingsRow, activePage == HomePage.Settings);
        }

        private void ApplyPlaceholderCompactLayout()
        {
            SetGameObjectVisible(FindSceneObjectByName("PlaceholderEyebrow"), false);
            SetGameObjectVisible(FindSceneObjectByName("PlaceholderPageStatus"), false);
            SetGameObjectVisible(GetPlaceholderCardRoot(placeholderChipText), false);
            SetGameObjectVisible(GetPlaceholderCardRoot(placeholderHeroText), false);
            SetGameObjectVisible(GetPlaceholderCardRoot(placeholderHeroIcon), false);

            if (placeholderBodyText == null)
            {
                return;
            }

            RectTransform bodyRect = placeholderBodyText.rectTransform;
            bodyRect.anchoredPosition = new Vector2(60f, -170f);
            bodyRect.sizeDelta = new Vector2(816f, 1690f);
        }

        private void ApplySettingsPageLayout()
        {
            if (placeholderBodyText != null)
            {
                RectTransform bodyRect = placeholderBodyText.rectTransform;
                bodyRect.anchoredPosition = new Vector2(60f, -170f);
                bodyRect.sizeDelta = new Vector2(816f, 1220f);
            }

            if (focusDurationSettingsRow != null)
            {
                RectTransform rowRect = focusDurationSettingsRow.GetComponent<RectTransform>();
                rowRect.anchoredPosition = new Vector2(60f, -716f);
                rowRect.sizeDelta = new Vector2(260f, 44f);
                ApplySettingsInlineInputLayout(
                    focusDurationSettingsRow,
                    "FocusDurationMinutesInput");
            }

            if (autoFocusDelaySettingsRow != null)
            {
                RectTransform rowRect = autoFocusDelaySettingsRow.GetComponent<RectTransform>();
                rowRect.anchoredPosition = new Vector2(60f, -900f);
                rowRect.sizeDelta = new Vector2(260f, 44f);
                ApplySettingsInlineInputLayout(
                    autoFocusDelaySettingsRow,
                    "AutoFocusDelaySecondsInput");
            }
        }

        private static void ApplySettingsInlineInputLayout(GameObject row, string inputName)
        {
            ApplyChildRect(row, inputName, new Vector2(142f, 0f), new Vector2(74f, 34f));
        }

        private static void ApplyChildRect(GameObject parent, string childName, Vector2 position, Vector2 size)
        {
            if (parent == null)
            {
                return;
            }

            Transform child = parent.transform.Find(childName);
            if (child == null)
            {
                return;
            }

            RectTransform rect = child as RectTransform;
            if (rect == null)
            {
                rect = child.GetComponent<RectTransform>();
            }

            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static GameObject GetPlaceholderCardRoot(Component component)
        {
            if (component == null || component.transform.parent == null)
            {
                return null;
            }

            return component.transform.parent.gameObject;
        }

        private void SetPlaceholderVisible(bool visible)
        {
            if (placeholderOverlay != null)
            {
                if (visible)
                {
                    placeholderOverlay.transform.SetAsLastSibling();
                }

                placeholderOverlay.SetActive(visible);
            }
        }

        private int GetRecentSevenDayTotalMinutes()
        {
            DateTime today = DateTime.Now.Date;
            int total = 0;
            for (int i = 0; i < 7; i++)
            {
                DateTime day = today.AddDays(-i);
                string key = FormatDateKey(day);
                total += GetDailyMinutes(key);
            }

            return total;
        }

        private string BuildRecentSevenDayText()
        {
            DateTime today = DateTime.Now.Date;
            StringBuilder trend = new StringBuilder(160);
            for (int i = 6; i >= 0; i--)
            {
                DateTime day = today.AddDays(-i);
                string key = FormatDateKey(day);
                int minutes = GetDailyMinutes(key);
                trend.Append(GetWeekdayLabel(day));
                trend.Append(" ");
                trend.Append(minutes > 0 ? minutes + "m" : "--");
                if (i == 4 || i == 1)
                {
                    trend.AppendLine();
                }
                else if (i > 0)
                {
                    trend.Append("   ");
                }
            }

            return trend.ToString();
        }

        private int GetDailyMinutes(string dateKey)
        {
            if (dateKey == currentDateKey)
            {
                return Mathf.Max(0, todayFocusMinutes);
            }

            return PlayerPrefs.GetInt(DailyMinutesKey(dateKey), 0);
        }

        private string ResolveCatPositionText()
        {
            GameObject cat = FindCatObject();
            if (cat == null)
            {
                return "未加载";
            }

            Vector3 position = cat.transform.position;
            if (Mathf.Abs(position.x) < 2.5f && Mathf.Abs(position.z) < 2.5f)
            {
                return "中心广场";
            }

            return "小镇场景 x" + position.x.ToString("0.0", CultureInfo.InvariantCulture) + " / z" + position.z.ToString("0.0", CultureInfo.InvariantCulture);
        }

        private static GameObject FindCatObject()
        {
            return GameObject.Find("CatCompanionModel");
        }

        private CatBehaviorDriver ResolveCatBehaviorDriver()
        {
            if (catBehaviorDriver != null)
            {
                return catBehaviorDriver;
            }

            catBehaviorDriver = FindAnyObjectByType<CatBehaviorDriver>();
            return catBehaviorDriver;
        }

        private void NotifyCatUiAction(CatBehaviorState state, string reason)
        {
            CatBehaviorDriver behaviorDriver = ResolveCatBehaviorDriver();
            if (behaviorDriver != null && behaviorDriver.isActiveAndEnabled)
            {
                behaviorDriver.NotifyUiAction(state, reason);
            }
        }

        private void NotifyCatFocusSessionStarted()
        {
            CatBehaviorDriver behaviorDriver = ResolveCatBehaviorDriver();
            if (behaviorDriver != null && behaviorDriver.isActiveAndEnabled)
            {
                behaviorDriver.NotifyFocusSessionStarted();
            }
        }

        private void NotifyCatFocusSessionEnded(bool completed)
        {
            CatBehaviorDriver behaviorDriver = ResolveCatBehaviorDriver();
            if (behaviorDriver != null && behaviorDriver.isActiveAndEnabled)
            {
                behaviorDriver.NotifyFocusSessionEnded(completed);
            }
        }

        private CatTownWalker ResolveCatWalker()
        {
            if (catWalker != null)
            {
                return catWalker;
            }

            catWalker = FindAnyObjectByType<CatTownWalker>();
            return catWalker;
        }

        private FocusFeedbackProvider ResolveFocusFeedbackProvider()
        {
            if (focusFeedbackProvider != null)
            {
                return focusFeedbackProvider;
            }

            focusFeedbackProvider = FindAnyObjectByType<FocusFeedbackProvider>();
            return focusFeedbackProvider;
        }

        private void RequestFocusFeedback(bool completed, int finishedSeconds)
        {
            FocusFeedbackProvider provider = ResolveFocusFeedbackProvider();
            if (provider == null)
            {
                ApplyFocusFeedback(LocalTemplateFallback.Generate(
                    BuildFeatureSummary(completed, finishedSeconds),
                    "feedback_provider_missing"));
                return;
            }

            BehaviorFeatureSummary summary = BuildFeatureSummary(completed, finishedSeconds);
            provider.RequestFeedback(summary, smartExplanationEnabled, ApplyFocusFeedback);
        }

        private BehaviorFeatureSummary BuildFeatureSummary(bool completed, int finishedSeconds)
        {
            int safeFinishedSeconds = Mathf.Max(1, finishedSeconds);
            string sessionId = "catlife-" + currentDateKey + "-" + completedSessions.ToString(CultureInfo.InvariantCulture);
            return BehaviorFeatureSummary.CreateLocalSession(
                sessionId,
                safeFinishedSeconds,
                completed ? safeFinishedSeconds : Mathf.Max(1, Mathf.RoundToInt(safeFinishedSeconds * 0.65f)),
                interruptionCount,
                completedSessions,
                todayFocusMinutes,
                longestStableSeconds,
                completed);
        }

        private void ApplyFocusFeedback(FocusFeedback feedback)
        {
            if (feedback == null)
            {
                feedback = FocusFeedback.Create("", "local_template", true, "empty_feedback");
            }

            latestFocusFeedbackText = feedback.text;
            latestFocusFeedbackSummary = feedback.recordSummary;
            latestFocusFeedbackSource = feedback.source;
            latestFocusFeedbackTone = feedback.tone;
            latestFocusFeedbackReaction = feedback.reactionHint;
            latestFocusFeedbackConfidence = Mathf.Clamp01(feedback.confidence);
            latestFocusFeedbackSafetyReason = feedback.safetyReason;
            latestFocusFeedbackDegraded = feedback.isDegraded;
            Debug.Log("[CatLife] focus_feedback llm_source=" + latestFocusFeedbackSource +
                " degraded=" + latestFocusFeedbackDegraded +
                " safety=" + latestFocusFeedbackSafetyReason +
                " confidence=" + latestFocusFeedbackConfidence.ToString("0.00", CultureInfo.InvariantCulture));

            EnsureCatBubblePresenter();
            if (catBubblePresenter != null)
            {
                catBubblePresenter.Show(latestFocusFeedbackText, latestFocusFeedbackSource);
            }

            RefreshActivePage();
        }

        private void EnsureCatBubblePresenter()
        {
            if (catBubblePresenter != null)
            {
                return;
            }

            catBubblePresenter = GetComponent<CatBubblePresenter>();
            if (catBubblePresenter != null)
            {
                return;
            }

            catBubblePresenter = gameObject.AddComponent<CatBubblePresenter>();
        }

        private GameObject ResolveMenuGroup(ref GameObject cachedGroup, string groupName, Button fallbackButton)
        {
            if (cachedGroup != null)
            {
                return cachedGroup;
            }

            cachedGroup = FindSceneObjectByName(groupName);
            if (cachedGroup != null)
            {
                return cachedGroup;
            }

            if (fallbackButton != null)
            {
                cachedGroup = fallbackButton.transform.parent != null
                    ? fallbackButton.transform.parent.gameObject
                    : fallbackButton.gameObject;
            }

            return cachedGroup;
        }

        private static GameObject FindSceneObjectByName(string objectName)
        {
            Transform[] transforms = Resources.FindObjectsOfTypeAll<Transform>();
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (candidate != null && candidate.name == objectName && candidate.gameObject.scene.IsValid())
                {
                    return candidate.gameObject;
                }
            }

            return null;
        }

        private static void SetGameObjectVisible(GameObject target, bool visible)
        {
            if (target != null)
            {
                target.SetActive(visible);
            }
        }

        private void EnsureFocusUnlockSlider()
        {
            if (focusUnlockSliderGroup == null)
            {
                focusUnlockSliderGroup = FindSceneObjectByName("FocusUnlockSliderGroup");
            }

            if (focusUnlockSliderGroup != null)
            {
                if (focusUnlockSlider == null)
                {
                    focusUnlockSlider = focusUnlockSliderGroup.GetComponentInChildren<FocusUnlockSlider>(true);
                }

                if (focusUnlockSlider != null)
                {
                    return;
                }
            }

            RectTransform canvasRect = transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            Sprite roundedSprite = FindSpriteFromImage("FocusPill");
            Sprite roundedOutlineSprite = FindSpriteFromImage("SlotOutline");
            Sprite circleSprite = FindSpriteFromImage("Menu_旋转");
            Sprite circleOutlineSprite = FindSpriteFromImage("Outline");
            Font font = todayFocusText != null ? todayFocusText.font : null;
            if (font == null && focusPillText != null)
            {
                font = focusPillText.font;
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            focusUnlockSliderGroup = new GameObject("FocusUnlockSliderGroup", typeof(RectTransform));
            focusUnlockSliderGroup.transform.SetParent(canvasRect, false);
            RectTransform rootRect = focusUnlockSliderGroup.GetComponent<RectTransform>();
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, 54f);
            rootRect.sizeDelta = new Vector2(260f, 420f);

            GameObject track = AddRuntimePanel("FocusUnlockTrack", focusUnlockSliderGroup.transform, roundedSprite, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(116f, 312f), new Color(1f, 1f, 1f, 0.22f));
            track.GetComponent<Image>().raycastTarget = false;
            RectTransform trackRect = track.GetComponent<RectTransform>();
            AddRuntimeImage("UnlockTrackHighlight", track.transform, roundedSprite, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(72f, 190f), new Color(1f, 1f, 1f, 0.20f));
            AddRuntimeImage("UnlockTrackOutline", track.transform, roundedOutlineSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 312f), new Color(1f, 0.92f, 0.54f, 0.90f));
            AddRuntimeImage("UnlockTopTick", track.transform, circleSprite, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(9f, 9f), new Color(1f, 1f, 1f, 0.58f));
            AddRuntimeImage("UnlockMidTick", track.transform, circleSprite, new Vector2(0.5f, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8f, 8f), new Color(1f, 1f, 1f, 0.45f));

            GameObject handle = AddRuntimePanel("FocusUnlockHandle", focusUnlockSliderGroup.transform, circleSprite, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 154f), new Vector2(116f, 116f), new Color(1f, 0.70f, 0.18f, 0.32f));
            Image handleImage = handle.GetComponent<Image>();
            handleImage.raycastTarget = true;
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            AddRuntimeImage("FocusUnlockHandleOutline", handle.transform, circleOutlineSprite, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 116f), new Color(1f, 0.90f, 0.46f, 1f));
            AddRuntimeText("FocusUnlockArrow", handle.transform, "↑", font, 38, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(64f, 64f), new Color(1f, 0.97f, 0.91f, 1f));

            Text label = AddRuntimeText("FocusUnlockLabel", focusUnlockSliderGroup.transform, "解锁", font, 28, FontStyle.Bold, TextAnchor.MiddleCenter, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 72f), new Vector2(140f, 44f), new Color(1f, 0.97f, 0.91f, 1f));
            AddRuntimeTextShadow(label, 0.24f, new Vector2(0f, -1.6f));

            focusUnlockSlider = handle.AddComponent<FocusUnlockSlider>();
            focusUnlockSlider.Configure(this, trackRect, handleRect, 0.96f);
            focusUnlockSliderGroup.SetActive(false);
        }

        private void EnsureSettingsTimingRows()
        {
            EnsureFocusDurationSettingsRow();
            EnsureAutoFocusDelaySettingsRow();
        }

        private void EnsureFocusDurationSettingsRow()
        {
            if (focusDurationSettingsRow != null && focusDurationInput != null)
            {
                return;
            }

            RectTransform parentRect = placeholderOverlay != null ? placeholderOverlay.transform as RectTransform : transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            Font font = placeholderBodyText != null ? placeholderBodyText.font : null;
            if (font == null && todayFocusText != null)
            {
                font = todayFocusText.font;
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            focusDurationSettingsRow = new GameObject("FocusDurationSettingsRow", typeof(RectTransform));
            focusDurationSettingsRow.transform.SetParent(parentRect, false);
            RectTransform rowRect = focusDurationSettingsRow.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(60f, -716f);
            rowRect.sizeDelta = new Vector2(260f, 44f);

            GameObject inputObject = AddRuntimePanel("FocusDurationMinutesInput", focusDurationSettingsRow.transform, null, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(142f, 0f), new Vector2(74f, 34f), Color.clear);
            Image inputImage = inputObject.GetComponent<Image>();
            inputImage.raycastTarget = true;
            focusDurationInput = inputObject.AddComponent<InputField>();
            focusDurationInput.contentType = InputField.ContentType.IntegerNumber;
            focusDurationInput.characterLimit = 3;
            focusDurationInput.lineType = InputField.LineType.SingleLine;

            int bodyFontSize = placeholderBodyText != null ? placeholderBodyText.fontSize : 24;
            Text inputText = AddRuntimeText("Text", inputObject.transform, "", font, bodyFontSize, FontStyle.Bold, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.18f, 0.1f, 0.04f, 1f));
            RectTransform inputTextRect = inputText.rectTransform;
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;
            inputText.raycastTarget = true;

            Text placeholderText = AddRuntimeText("Placeholder", inputObject.transform, "25", font, bodyFontSize, FontStyle.Bold, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.56f, 0.36f, 0.14f, 0.45f));
            RectTransform placeholderRect = placeholderText.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            placeholderText.raycastTarget = false;

            focusDurationInput.textComponent = inputText;
            focusDurationInput.placeholder = placeholderText;
            focusDurationInput.onEndEdit.AddListener(ApplyFocusDurationInput);
            SyncSettingsTimingInputs();
            ApplySettingsPageLayout();
            focusDurationSettingsRow.SetActive(activePage == HomePage.Settings && placeholderOverlay != null && placeholderOverlay.activeSelf);
        }

        private void EnsureAutoFocusDelaySettingsRow()
        {
            if (autoFocusDelaySettingsRow != null && autoFocusDelayInput != null)
            {
                return;
            }

            RectTransform parentRect = placeholderOverlay != null ? placeholderOverlay.transform as RectTransform : transform as RectTransform;
            if (parentRect == null)
            {
                return;
            }

            Font font = placeholderBodyText != null ? placeholderBodyText.font : null;
            if (font == null && todayFocusText != null)
            {
                font = todayFocusText.font;
            }

            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            autoFocusDelaySettingsRow = new GameObject("AutoFocusDelaySettingsRow", typeof(RectTransform));
            autoFocusDelaySettingsRow.transform.SetParent(parentRect, false);
            RectTransform rowRect = autoFocusDelaySettingsRow.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = new Vector2(60f, -900f);
            rowRect.sizeDelta = new Vector2(260f, 44f);

            GameObject inputObject = AddRuntimePanel("AutoFocusDelaySecondsInput", autoFocusDelaySettingsRow.transform, null, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(142f, 0f), new Vector2(74f, 34f), Color.clear);
            Image inputImage = inputObject.GetComponent<Image>();
            inputImage.raycastTarget = true;
            autoFocusDelayInput = inputObject.AddComponent<InputField>();
            autoFocusDelayInput.contentType = InputField.ContentType.IntegerNumber;
            autoFocusDelayInput.characterLimit = 4;
            autoFocusDelayInput.lineType = InputField.LineType.SingleLine;

            int bodyFontSize = placeholderBodyText != null ? placeholderBodyText.fontSize : 24;
            Text inputText = AddRuntimeText("Text", inputObject.transform, "", font, bodyFontSize, FontStyle.Bold, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.18f, 0.1f, 0.04f, 1f));
            RectTransform inputTextRect = inputText.rectTransform;
            inputTextRect.anchorMin = Vector2.zero;
            inputTextRect.anchorMax = Vector2.one;
            inputTextRect.offsetMin = Vector2.zero;
            inputTextRect.offsetMax = Vector2.zero;
            inputText.raycastTarget = true;

            Text placeholderText = AddRuntimeText("Placeholder", inputObject.transform, "10", font, bodyFontSize, FontStyle.Bold, TextAnchor.MiddleLeft, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, new Color(0.56f, 0.36f, 0.14f, 0.45f));
            RectTransform placeholderRect = placeholderText.rectTransform;
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            placeholderRect.offsetMin = Vector2.zero;
            placeholderRect.offsetMax = Vector2.zero;
            placeholderText.raycastTarget = false;

            autoFocusDelayInput.textComponent = inputText;
            autoFocusDelayInput.placeholder = placeholderText;
            autoFocusDelayInput.onEndEdit.AddListener(ApplyAutoFocusDelayInput);
            SyncSettingsTimingInputs();
            ApplySettingsPageLayout();
            autoFocusDelaySettingsRow.SetActive(activePage == HomePage.Settings && placeholderOverlay != null && placeholderOverlay.activeSelf);
        }

        private void SyncSettingsTimingInputs()
        {
            if (focusDurationInput != null)
            {
                focusDurationInput.SetTextWithoutNotify(GetFocusSessionMinutes().ToString(CultureInfo.InvariantCulture));
            }

            if (focusDurationStatusText != null)
            {
                focusDurationStatusText.text = focusRunning ? "当前轮结束后生效" : "下一轮立即生效";
            }

            if (autoFocusDelayInput != null)
            {
                autoFocusDelayInput.SetTextWithoutNotify(GetAutoFocusDelaySeconds().ToString(CultureInfo.InvariantCulture));
            }

            if (autoFocusDelayStatusText != null)
            {
                autoFocusDelayStatusText.text = autoEnterFocusAfterDelay ? "进入页面后自动计时" : "已关闭自动进入";
            }
        }

        private void ApplyFocusDurationInput(string value)
        {
            int minutes;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out minutes))
            {
                SyncSettingsTimingInputs();
                return;
            }

            minutes = Mathf.Clamp(minutes, MinFocusSessionMinutes, MaxFocusSessionMinutes);
            AndroidBehaviorEventBridge.RecordUnityEvent("UiButton", "settings_focus_duration");
            focusSessionSeconds = minutes * 60;
            if (!focusRunning)
            {
                activeSessionSeconds = focusSessionSeconds;
                focusRemainingSeconds = focusSessionSeconds;
            }

            SaveRuntimeData();
            SyncSettingsTimingInputs();
            UpdateStatusText(true);
            RefreshActivePage();
        }

        private void ApplyAutoFocusDelayInput(string value)
        {
            int seconds;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
            {
                SyncSettingsTimingInputs();
                return;
            }

            seconds = Mathf.RoundToInt(ClampAutoFocusDelaySeconds(seconds));
            AndroidBehaviorEventBridge.RecordUnityEvent("UiButton", "settings_auto_focus_delay");
            autoFocusDelaySeconds = seconds;
            autoEnterFocusAfterDelay = seconds > 0;
            if (!focusRunning && focusState == FocusFlowState.Normal)
            {
                playModeStartedAt = Time.realtimeSinceStartup;
                autoFocusConsumed = false;
            }

            SaveRuntimeData();
            SyncSettingsTimingInputs();
            UpdateStatusText(true);
            RefreshActivePage();
        }

        private static GameObject AddRuntimePanel(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image image = go.GetComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            return go;
        }

        private static Image AddRuntimeImage(string name, Transform parent, Sprite sprite, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, Color color)
        {
            GameObject go = AddRuntimePanel(name, parent, sprite, anchor, pivot, position, size, color);
            Image image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static Text AddRuntimeText(string name, Transform parent, string value, Font font, int size, FontStyle style, TextAnchor alignment, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 dimensions, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;

            Text text = go.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void AddRuntimeTextShadow(Text text, float alpha, Vector2 distance)
        {
            if (text == null)
            {
                return;
            }

            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.27f, 0.1f, 0f, alpha);
            shadow.effectDistance = distance;
            shadow.useGraphicAlpha = true;
        }

        private static Sprite FindSpriteFromImage(string objectName)
        {
            Image[] images = Resources.FindObjectsOfTypeAll<Image>();
            for (int i = 0; i < images.Length; i++)
            {
                Image image = images[i];
                if (image != null && image.name == objectName && image.sprite != null && image.gameObject.scene.IsValid())
                {
                    return image.sprite;
                }
            }

            return null;
        }

        private string BuildUnlockedActionText()
        {
            if (todayFocusMinutes >= 75)
            {
                return "挥爪、轻叫、开心转圈、贴近巡视";
            }

            if (todayFocusMinutes >= 25)
            {
                return "挥爪、轻叫、开心转圈";
            }

            return "挥爪、轻叫";
        }

        private string GetNextGoalText()
        {
            if (todayFocusMinutes < 25)
            {
                return "完成 25 分钟专注后获得小爪印";
            }

            if (todayFocusMinutes < 50)
            {
                return "累计 50 分钟专注后花丛成长";
            }

            if (todayFocusMinutes < 100)
            {
                return "累计 100 分钟专注后解锁贴近巡视";
            }

            return "今日成长已完成，继续保持稳定陪伴";
        }

        private string BuildTownFeedbackText()
        {
            if (todayFocusMinutes >= 100)
            {
                return "广场花丛明显成长";
            }

            if (todayFocusMinutes >= 50)
            {
                return "花丛轻微生长";
            }

            if (todayFocusMinutes >= 25)
            {
                return "获得新的小爪印";
            }

            return "等待首段专注完成";
        }

        private string BuildFocusInsightText()
        {
            if (completedSessions <= 0)
            {
                return "开始第一段专注后生成今日洞察";
            }

            if (interruptionCount > completedSessions)
            {
                return "今天中断偏多，建议缩短下一段专注时长";
            }

            if (longestStableSeconds >= focusSessionSeconds)
            {
                return "你已完成一段稳定专注";
            }

            return "继续保持，记录会随会话完成自动更新";
        }

        private int EstimateCompletedSessions(int minutes)
        {
            int sessionMinutes = Mathf.Max(1, Mathf.RoundToInt(Mathf.Max(1, focusSessionSeconds) / 60f));
            return Mathf.Max(0, Mathf.RoundToInt((float)Mathf.Max(0, minutes) / sessionMinutes));
        }

        private int GetFocusSessionMinutes()
        {
            return Mathf.Clamp(
                Mathf.RoundToInt(ClampFocusSessionSeconds(focusSessionSeconds) / 60f),
                MinFocusSessionMinutes,
                MaxFocusSessionMinutes);
        }

        private int GetAutoFocusDelaySeconds()
        {
            return Mathf.RoundToInt(ClampAutoFocusDelaySeconds(autoFocusDelaySeconds));
        }

        private static int ClampFocusSessionSeconds(int seconds)
        {
            int minSeconds = MinFocusSessionMinutes * 60;
            int maxSeconds = MaxFocusSessionMinutes * 60;
            return Mathf.Clamp(seconds, minSeconds, maxSeconds);
        }

        private static float ClampAutoFocusDelaySeconds(float seconds)
        {
            return Mathf.Clamp(seconds, MinAutoFocusDelaySeconds, MaxAutoFocusDelaySeconds);
        }

        private int EstimateLongestStableSeconds(int minutes)
        {
            if (minutes <= 0)
            {
                return 0;
            }

            return Mathf.Min(Mathf.Max(1, focusSessionSeconds), minutes * 60);
        }

        private static string BoolText(bool value)
        {
            return value ? "开" : "关";
        }

        private static string GetFeedbackSourceLabel(string source)
        {
            return source == "mock_llm" ||
                source == "mock_llm_structured" ||
                source == "llm_structured"
                ? "智能反馈"
                : "本地反馈";
        }

        private static string ColorTitle(string title)
        {
            return "<color=" + BrownColor + ">" + title + "</color>";
        }

        private static string FormatMinutesOrSeconds(int seconds)
        {
            if (seconds <= 0)
            {
                return "0 分钟";
            }

            int minutes = seconds / 60;
            int remainder = seconds % 60;
            if (remainder == 0)
            {
                return minutes + " 分钟";
            }

            return minutes + ":" + remainder.ToString("00", CultureInfo.InvariantCulture);
        }

        private static string FormatDisplayDate(DateTime date)
        {
            return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private static string FormatDateKey(DateTime date)
        {
            return date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        }

        private static string DailyMinutesKey(string dateKey)
        {
            return PrefPrefix + "DailyMinutes." + dateKey;
        }

        private static string DailySessionsKey(string dateKey)
        {
            return PrefPrefix + "DailySessions." + dateKey;
        }

        private static string DailyInterruptionsKey(string dateKey)
        {
            return PrefPrefix + "DailyInterruptions." + dateKey;
        }

        private static string DailyLongestKey(string dateKey)
        {
            return PrefPrefix + "DailyLongestSeconds." + dateKey;
        }

        private static string GetWeekdayLabel(DateTime date)
        {
            switch (date.DayOfWeek)
            {
                case DayOfWeek.Monday:
                    return "周一";
                case DayOfWeek.Tuesday:
                    return "周二";
                case DayOfWeek.Wednesday:
                    return "周三";
                case DayOfWeek.Thursday:
                    return "周四";
                case DayOfWeek.Friday:
                    return "周五";
                case DayOfWeek.Saturday:
                    return "周六";
                default:
                    return "周日";
            }
        }

        private void BindListeners()
        {
            if (listenersBound)
            {
                UnbindListeners();
            }

            AddListener(startFocusButton, StartFocusSession);
            AddListener(catButton, ShowCatPage);
            AddListener(recordButton, ShowRecordPage);
            AddListener(settingsButton, ShowSettingsPage);
            AddListener(closePlaceholderButton, HidePlaceholder);
            listenersBound = true;
        }

        private void UnbindListeners()
        {
            if (!listenersBound)
            {
                return;
            }

            RemoveListener(startFocusButton, StartFocusSession);
            RemoveListener(catButton, ShowCatPage);
            RemoveListener(recordButton, ShowRecordPage);
            RemoveListener(settingsButton, ShowSettingsPage);
            RemoveListener(closePlaceholderButton, HidePlaceholder);
            listenersBound = false;
        }

        private static void AddListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.AddListener(action);
            }
        }

        private static void RemoveListener(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private static string FormatClock(float seconds)
        {
            int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(seconds));
            int minutes = totalSeconds / 60;
            int remainder = totalSeconds % 60;
            return minutes.ToString("00", CultureInfo.InvariantCulture) + ":" + remainder.ToString("00", CultureInfo.InvariantCulture);
        }
    }
}
