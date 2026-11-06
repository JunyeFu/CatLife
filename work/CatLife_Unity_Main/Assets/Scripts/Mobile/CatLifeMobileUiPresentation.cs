using System;
using CatLife.Mobile;
using UnityEngine;
using UnityEngine.UI;

// Serialized visual bindings. Session, rewards, consent and persistence stay in CatLifeMobileApp.
public sealed class CatLifeMobileUiPresentation : MonoBehaviour
{
    public Sprite chipNormal, chipSelected, secondary, toggleOff, toggleOn, chartBar;
    public Image[] minuteImages;
    public Text[] minuteTexts;
    public Image quietImage, gentleImage, setupAiToggle;
    public Text quietText, gentleText, focusMode;
    public Text setupToday;
    public Text durationValue, reminderValue, autoFocusValue, recordsCountValue, clearValue;
    public Text[] settingsRowLabels;
    public Image settingsAiToggle, settingsBehaviorToggle, settingsExitToggle;
    public Image growthProgress;
    public Image[] growthCards, growthIcons;
    public Text[] growthLabels;
    public Image[] dayBars;
    public Text[] dayLabels;
    public Text[] recordRows;
    public GameObject[] recordCards;
    public GameObject rewardStats;
    public Text rewardDuration, rewardBehavior, rewardGrowth, rewardPaws, rewardInsight, rewardScrollHint;
    public Image rewardProgress;
    public RectTransform rewardViewport, rewardContent;
    public ScrollRect rewardScroll;
    public Color ink, mutedInk, onDark;
    public Font darkBodyFont;
    public Text[] darkBodyTexts = Array.Empty<Text>();

    public void ApplyTypography()
    {
        foreach (Text text in darkBodyTexts) text.font = darkBodyFont;
    }

    public void RenderSetup(int minutes, bool quiet, bool ai, string today)
    {
        setupToday.text = today;
        int[] choices = { 15, 25, 45 };
        for (int i = 0; i < choices.Length; i++)
        {
            bool selected = choices[i] == minutes;
            minuteImages[i].sprite = selected ? chipSelected : chipNormal;
            minuteImages[i].color = Color.white;
            minuteTexts[i].color = selected ? onDark : ink;
        }
        quietImage.sprite = gentleImage.sprite = secondary;
        quietImage.color = gentleImage.color = Color.white;
        quietText.color = gentleText.color = ink;
        focusMode.text = quiet ? "安静陪伴" : "轻提醒";
        setupAiToggle.sprite = ai ? toggleOn : toggleOff;
        setupAiToggle.color = Color.white;
    }

    public void RenderSettings(CatLifeAppData data, bool clearArmed)
    {
        durationValue.text = data.settings.defaultMinutes + " 分钟";
        reminderValue.text = data.settings.reminderMode == "quiet" ? "安静陪伴" : "轻提醒";
        autoFocusValue.text = data.settings.autoFocusAdaptationSeconds == 0 ? "关闭" : data.settings.autoFocusAdaptationSeconds + " 秒";
        recordsCountValue.text = data.records.Count + " 条  ›";
        clearValue.text = clearArmed ? "再次点击将清除" : "确认后清除 ›";
        string[] labels = { "默认时长", "陪伴模式", "自动专注", "智能建议", "本地行为统计", "专注记录" };
        for (int i = 0; i < labels.Length; i++) settingsRowLabels[i].text = labels[i];
        settingsAiToggle.sprite = data.settings.aiEnabled ? toggleOn : toggleOff;
        settingsBehaviorToggle.sprite = data.settings.localBehaviorStatsEnabled ? toggleOn : toggleOff;
        settingsExitToggle.sprite = data.settings.catExitInterventionEnabled ? toggleOn : toggleOff;
    }

    public void RenderGrowth(CatLifeDerivedStats stats)
    {
        growthProgress.fillAmount = stats.LevelProgress / 100f;
        int[] required = { 1, 3, 5, 8 };
        for (int i = 0; i < required.Length; i++)
        {
            bool unlocked = stats.Paws >= required[i];
            growthCards[i].color = Color.white;
            growthIcons[i].color = new Color(1, 1, 1, unlocked ? 1 : .58f);
            growthLabels[i].color = new Color(ink.r, ink.g, ink.b, unlocked ? 1 : .58f);
        }
    }

    public void RenderChart(int[] minutes, int maximum)
    {
        for (int i = 0; i < 7; i++)
        {
            float x = (92 + i * 110) / 952f;
            var rect = dayBars[i].rectTransform;
            rect.anchorMin = new Vector2(x, 170f / 650f);
            rect.anchorMax = new Vector2(x + 62f / 952f, (170f + 300f * minutes[i] / maximum) / 650f);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            dayBars[i].sprite = chartBar;
            dayBars[i].type = Image.Type.Sliced;
            dayBars[i].color = Color.white;
            var label = dayLabels[i].rectTransform;
            label.anchorMin = new Vector2((72 + i * 110) / 952f, 98f / 650f);
            label.anchorMax = new Vector2((158 + i * 110) / 952f, 150f / 650f);
            label.offsetMin = label.offsetMax = Vector2.zero;
        }
    }

    public void RenderRecords(string recentRecords)
    {
        string[] lines = recentRecords.Split('\n');
        for (int i = 0; i < recordRows.Length; i++)
        {
            recordCards[i].SetActive(i < lines.Length);
            if (i < lines.Length) recordRows[i].text = lines[i];
        }
    }

    public void RenderReward(CatLifeSessionRecord record, string insight, CatLifeDerivedStats stats)
    {
        rewardStats.SetActive(true);
        rewardDuration.text = (record.ActualSeconds / 60) + " 分钟";
        rewardBehavior.text = record.behaviorStatsDisabled ? "行为统计未采集" : $"后台 {record.BackgroundCount} 次     ·     稳定度 {record.StabilityPercent}%";
        rewardGrowth.text = $"Lv.{stats.Level}   {stats.LevelProgress}/100                          成长 +{record.GrowthAwarded}";
        rewardPaws.text = "爪印 +" + record.PawsAwarded;
        rewardProgress.fillAmount = stats.LevelProgress / 100f;
        rewardInsight.text = insight;
        rewardInsight.fontSize = 34;
        SetRewardViewport(670, 170);
        ResizeInsight();
    }

    public void ShowLocalAction()
    {
        rewardInsight.fontSize = 34;
        rewardStats.SetActive(false);
        SetRewardViewport(210, 630);
        ResizeInsight();
    }

    private void SetRewardViewport(float top, float height)
    {
        rewardViewport.anchorMin = new Vector2(64f / 1080f, 1 - (top + height) / 1104f);
        rewardViewport.anchorMax = new Vector2(1016f / 1080f, 1 - top / 1104f);
        rewardViewport.offsetMin = rewardViewport.offsetMax = Vector2.zero;
    }

    private void ResizeInsight()
    {
        Canvas.ForceUpdateCanvases();
        rewardInsight.text = CatLifeTextWrap.WithoutSingleCharacterTail(rewardInsight, rewardInsight.text);
        float contentHeight = rewardInsight.preferredHeight + 48;
        rewardContent.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
            Mathf.Max(rewardViewport.rect.height, contentHeight));
        rewardScrollHint.gameObject.SetActive(contentHeight > rewardViewport.rect.height);
        rewardScroll.verticalNormalizedPosition = 1;
    }
}
