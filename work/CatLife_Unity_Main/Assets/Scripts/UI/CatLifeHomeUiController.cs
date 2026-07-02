using UnityEngine;
using UnityEngine.UI;

namespace CatLife.UI
{
    [DisallowMultipleComponent]
    public sealed class CatLifeHomeUiController : MonoBehaviour
    {
        private const string HighlightColor = "#FFD14A";

        [SerializeField] private Text todayFocusText;
        [SerializeField] private Text focusPillText;
        [SerializeField] private Button startFocusButton;
        [SerializeField] private Button catButton;
        [SerializeField] private Button recordButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button closePlaceholderButton;
        [SerializeField] private GameObject placeholderOverlay;
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

        private int todayFocusMinutes;
        private float focusRemainingSeconds;
        private bool focusRunning;
        private float nextStatusRefreshTime;
        private bool listenersBound;

        private void Awake()
        {
            todayFocusMinutes = Mathf.Max(0, initialTodayFocusMinutes);
            focusRemainingSeconds = Mathf.Max(1, focusSessionSeconds);
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
            focusRemainingSeconds = Mathf.Max(1, focusSessionSeconds);
            focusRunning = true;
            SetPlaceholderVisible(false);
            UpdateStatusText(true);
        }

        public void ShowCatPage()
        {
            ShowPlaceholder(
                "猫咪",
                "猫咪状态",
                "查看当前陪伴状态、成长值和已解锁动作。",
                catPageIcon,
                "<color=#8F541C>当前状态</color>\n心情：安静陪伴\n位置：中心广场巡游\n动作：连续行走 / 呼吸 / 摆尾 / 歪头\n\n<color=#8F541C>成长反馈</color>\n陪伴等级：Lv.1\n成长值：48 / 100\n已解锁：挥爪、轻叫、开心转圈\n下一目标：完成 25 分钟专注后获得小爪印\n\n<color=#8F541C>交互预留</color>\n轻点猫咪：短反馈动作\n长按猫咪：亲密互动\n专注中：动作放慢，减少打扰");
        }

        public void ShowRecordPage()
        {
            ShowPlaceholder(
                "专注记录",
                "今日专注",
                "查看专注时长、奖励和最近 7 天趋势。",
                recordPageIcon,
                "<color=#8F541C>今日概览</color>\n已专注：" + todayFocusMinutes + " 分钟\n完成段数：2 段\n最长稳定：25 分钟\n中断次数：1 次\n\n<color=#8F541C>最近 7 天</color>\n周一 10m   周二 18m   周三 25m\n周四 48m   周五 --    周六 --    周日 --\n\n<color=#8F541C>游戏化反馈</color>\n奖励：星星果 x 2\n小镇变化：花丛轻微生长\n洞察：你在晚上更容易进入稳定状态");
        }

        public void ShowSettingsPage()
        {
            ShowPlaceholder(
                "设置",
                "识别与隐私",
                "管理本地识别、智能解释和数据操作边界。",
                settingsPageIcon,
                "<color=#8F541C>识别与智能</color>\n本地行为识别：开\n智能解释：关\n大模型建议：仅在用户主动开启后分析会话摘要\n\n<color=#8F541C>隐私边界</color>\n不录屏\n不读取输入内容\n不跨 App 监控\n默认只保存专注时长、打断次数、猫咪反馈状态\n\n<color=#8F541C>数据操作</color>\n清除本地记录：待接入\n导出专注摘要：待接入");
        }

        public void HidePlaceholder()
        {
            SetPlaceholderVisible(false);
        }

        private void CompleteFocusSession()
        {
            focusRunning = false;
            todayFocusMinutes += Mathf.Max(1, Mathf.RoundToInt(focusSessionSeconds / 60f));
            focusRemainingSeconds = Mathf.Max(1, focusSessionSeconds);
            UpdateStatusText(true);
        }

        private void UpdateStatusText(bool force)
        {
            if (!force && Time.unscaledTime < nextStatusRefreshTime)
            {
                return;
            }

            nextStatusRefreshTime = Time.unscaledTime + 0.25f;

            if (todayFocusText != null)
            {
                todayFocusText.supportRichText = true;
                todayFocusText.text = "今天已专注 <color=" + HighlightColor + ">" + todayFocusMinutes + "</color> 分钟";
            }

            if (focusPillText != null)
            {
                focusPillText.supportRichText = true;
                string status = focusRunning ? "专注中" : "准备中";
                focusPillText.text = status + " <color=" + HighlightColor + ">" + FormatClock(focusRemainingSeconds) + "</color>";
            }
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
            return minutes.ToString("00") + ":" + remainder.ToString("00");
        }
    }
}
