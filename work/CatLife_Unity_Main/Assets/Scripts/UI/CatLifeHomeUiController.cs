using System;
using System.Globalization;
using System.Text;
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

        [SerializeField] private Text todayFocusText;
        [SerializeField] private Text focusPillText;
        [SerializeField] private Button startFocusButton;
        [SerializeField] private Button catButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button settingsButton;
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
        [SerializeField] private int initialTodayFocusMinutes = 48;
        [SerializeField] private int focusSessionSeconds = 1509;
        [SerializeField] private bool startTimerOnEnable = true;
        [SerializeField] private bool bootstrapLocalDataWhenEmpty = true;
        [SerializeField] private bool localRecognitionDefault = true;
        [SerializeField] private bool smartExplanationDefault;

        private int todayFocusMinutes;
        private int completedSessions;
        private int interruptionCount;
        private int longestStableSeconds;
        private int activeSessionSeconds;
        private float focusRemainingSeconds;
        private bool focusRunning;
        private bool localRecognitionEnabled;
        private bool smartExplanationEnabled;
        private string currentDateKey;
        private float nextStatusRefreshTime;
        private bool listenersBound;
        private HomePage activePage;

        private enum HomePage
        {
            None,
            Cat,
            Record,
            Settings
        }

        private void Awake()
        {
            LoadRuntimeData();
            activeSessionSeconds = Mathf.Max(1, focusSessionSeconds);
            focusRemainingSeconds = activeSessionSeconds;
            SetPlaceholderVisible(false);
            UpdateStatusText(true);
        }

        private void OnEnable()
        {
            BindListeners();
            if (startTimerOnEnable && focusRemainingSeconds > 0f)
            {
                focusRunning = true;
            }

            UpdateStatusText(true);
        }

        private void OnDisable()
        {
            UnbindListeners();
        }

        private void Update()
        {
            if (!focusRunning)
            {
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
            activeSessionSeconds = Mathf.Max(1, focusSessionSeconds);
            focusRemainingSeconds = activeSessionSeconds;
            focusRunning = true;
            SetPlaceholderVisible(false);
            UpdateStatusText(true);
        }

        public void ShowCatPage()
        {
            activePage = HomePage.Cat;
            ShowPlaceholder(
                "猫咪",
                "猫咪状态",
                BuildCatHeroText(),
                catPageIcon,
                BuildCatPageBody());
        }

        public void ShowRecordPage()
        {
            activePage = HomePage.Record;
            ShowPlaceholder(
                "记录",
                "专注记录",
                "查看专注时长、奖励和最近 7 天趋势。",
                recordPageIcon,
                BuildRecordPageBody());
        }

        public void ShowSettingsPage()
        {
            activePage = HomePage.Settings;
            ShowPlaceholder(
                "设置",
                "识别与隐私",
                "管理本地识别、智能解释和数据操作边界。",
                settingsPageIcon,
                BuildSettingsPageBody());
        }

        public void HidePlaceholder()
        {
            activePage = HomePage.None;
            SetPlaceholderVisible(false);
        }

        private void CompleteFocusSession()
        {
            focusRunning = false;
            int finishedSeconds = Mathf.Max(1, activeSessionSeconds);
            int gainedMinutes = Mathf.Max(1, Mathf.RoundToInt(finishedSeconds / 60f));
            todayFocusMinutes += gainedMinutes;
            completedSessions += 1;
            longestStableSeconds = Mathf.Max(longestStableSeconds, finishedSeconds);
            focusRemainingSeconds = Mathf.Max(1, focusSessionSeconds);
            SaveRuntimeData();
            UpdateStatusText(true);
            RefreshActivePage();
        }

        private void LoadRuntimeData()
        {
            DateTime today = DateTime.Now.Date;
            currentDateKey = FormatDateKey(today);

            string todayMinutesKey = DailyMinutesKey(currentDateKey);
            bool hasTodayRecord = PlayerPrefs.HasKey(todayMinutesKey);
            int bootstrapMinutes = bootstrapLocalDataWhenEmpty ? Mathf.Max(0, initialTodayFocusMinutes) : 0;
            todayFocusMinutes = PlayerPrefs.GetInt(todayMinutesKey, bootstrapMinutes);
            completedSessions = PlayerPrefs.GetInt(DailySessionsKey(currentDateKey), EstimateCompletedSessions(todayFocusMinutes));
            interruptionCount = PlayerPrefs.GetInt(DailyInterruptionsKey(currentDateKey), 0);
            longestStableSeconds = PlayerPrefs.GetInt(DailyLongestKey(currentDateKey), EstimateLongestStableSeconds(todayFocusMinutes));
            localRecognitionEnabled = PlayerPrefs.GetInt(LocalRecognitionKey, localRecognitionDefault ? 1 : 0) == 1;
            smartExplanationEnabled = PlayerPrefs.GetInt(SmartExplanationKey, smartExplanationDefault ? 1 : 0) == 1;

            if (!hasTodayRecord && bootstrapLocalDataWhenEmpty)
            {
                SaveRuntimeData();
            }
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
                string status = focusRunning ? "专注中" : "准备中";
                focusPillText.text = status + " <color=" + HighlightColor + ">" + FormatClock(focusRemainingSeconds) + "</color>";
            }
        }

        private void RefreshActivePage()
        {
            if (activePage == HomePage.Cat)
            {
                ShowCatPage();
            }
            else if (activePage == HomePage.Record)
            {
                ShowRecordPage();
            }
            else if (activePage == HomePage.Settings)
            {
                ShowSettingsPage();
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

            return focusRunning ? "专注中保持低打扰陪伴，猫咪会慢速巡游。" : "查看当前陪伴状态、成长值和已解锁动作。";
        }

        private string BuildCatPageBody()
        {
            int growthValue = Mathf.Clamp(todayFocusMinutes, 0, 100);
            int companionLevel = Mathf.Max(1, 1 + GetRecentSevenDayTotalMinutes() / 120);
            string catPosition = ResolveCatPositionText();
            string actionText = focusRunning ? "连续行走 / Walk / 呼吸 / 摆尾" : "待机呼吸 / 轻微摆尾 / 可被唤起";
            string nextGoal = GetNextGoalText();

            StringBuilder body = new StringBuilder(512);
            body.AppendLine(ColorTitle("当前状态"));
            body.AppendLine("心情：" + (focusRunning ? "专注陪伴" : "安静陪伴"));
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
            return body.ToString();
        }

        private string BuildSettingsPageBody()
        {
            StringBuilder body = new StringBuilder(640);
            body.AppendLine(ColorTitle("识别与智能"));
            body.AppendLine("本地行为识别：" + BoolText(localRecognitionEnabled));
            body.AppendLine("智能解释：" + BoolText(smartExplanationEnabled));
            body.AppendLine("大模型建议：" + (smartExplanationEnabled ? "根据用户主动开启后的会话摘要生成建议" : "关闭，仅保留本地统计"));
            body.AppendLine();
            body.AppendLine(ColorTitle("隐私边界"));
            body.AppendLine("不录屏");
            body.AppendLine("不读取输入内容");
            body.AppendLine("不跨 App 监控");
            body.AppendLine("默认只保存专注时长、打断次数、猫咪反馈状态");
            body.AppendLine();
            body.AppendLine(ColorTitle("数据操作"));
            body.AppendLine("当前数据日期：" + FormatDisplayDate(DateTime.Now.Date));
            body.AppendLine("本地记录：" + todayFocusMinutes + " 分钟 / " + completedSessions + " 段 / " + interruptionCount + " 次中断");
            body.AppendLine("导出专注摘要：" + (todayFocusMinutes > 0 ? "已有可导出的今日摘要" : "暂无今日数据"));
            return body.ToString();
        }

        private void ShowPlaceholder(string title, string chip, string hero, Sprite icon, string body)
        {
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
