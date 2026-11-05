using UnityEngine;

namespace CatLife.Recognition
{
    [DisallowMultipleComponent]
    public sealed class RealtimeFeatureEngine : MonoBehaviour
    {
        private const int MaxInteractionEvents = 96;
        private const int MaxPageEvents = 48;

        private readonly float[] tapTimes = new float[MaxInteractionEvents];
        private readonly float[] interactionTimes = new float[MaxInteractionEvents];
        private readonly float[] pageSwitchTimes = new float[MaxPageEvents];

        private int tapIndex;
        private int interactionIndex;
        private int pageSwitchIndex;
        private bool focusSessionActive;
        private float lastInteractionTime = -999f;
        private float focusSessionStartedAt = -999f;
        private string lastLocalEvent = "none";
        private float quietDwellSeconds;
        private bool quietDwellReported;
        private RealtimeFeatureSnapshot latest;
        private bool collectionEnabled = true;

        public void SetCollectionEnabled(bool value)
        {
            collectionEnabled = value;
            if (value) return;
            System.Array.Clear(tapTimes, 0, tapTimes.Length);
            System.Array.Clear(interactionTimes, 0, interactionTimes.Length);
            System.Array.Clear(pageSwitchTimes, 0, pageSwitchTimes.Length);
            tapIndex = interactionIndex = pageSwitchIndex = 0;
            lastLocalEvent = "disabled";
            quietDwellSeconds = 0; quietDwellReported = false;
            latest = new RealtimeFeatureSnapshot { localEventSummary = "disabled" };
        }

        public RealtimeFeatureSnapshot Latest
        {
            get { return latest; }
        }

        public string LastAcceptedBehaviorEvent
        {
            get { return lastLocalEvent; }
        }

        private void Awake()
        {
            Tick(0f);
        }

        public void RecordUiEvent(string eventName)
        {
            if (!collectionEnabled) return;
            string safeEventName = string.IsNullOrEmpty(eventName) ? "ui_event" : eventName;
            RecordInteraction(safeEventName);
            if (safeEventName.Contains("page"))
            {
                WriteTimestamp(pageSwitchTimes, ref pageSwitchIndex, CurrentTimestamp());
            }
        }

        public void RecordUiTap(string eventName)
        {
            if (!collectionEnabled) return;
            RecordInteraction(string.IsNullOrEmpty(eventName) ? "ui_tap" : eventName);
            WriteTimestamp(tapTimes, ref tapIndex, CurrentTimestamp());
        }

        public void RecordCatInteraction(string eventName)
        {
            if (!collectionEnabled) return;
            RecordInteraction(string.IsNullOrEmpty(eventName) ? "cat_interaction" : eventName);
            WriteTimestamp(tapTimes, ref tapIndex, CurrentTimestamp());
        }

        public void RecordFocusSessionStarted()
        {
            if (!collectionEnabled) return;
            focusSessionActive = true;
            focusSessionStartedAt = Time.unscaledTime;
            RecordInteraction("focus_started");
        }

        public void RecordFocusSessionEnded(bool completed)
        {
            if (!collectionEnabled) return;
            focusSessionActive = false;
            RecordInteraction(completed ? "focus_completed" : "focus_unlocked");
        }

        public void RecordBehaviorEvent(BehaviorEvent behaviorEvent)
        {
            if (behaviorEvent == null)
            {
                return;
            }

            switch (behaviorEvent.eventType)
            {
                case "FocusStart":
                    RecordFocusSessionStarted();
                    break;
                case "FocusComplete":
                    RecordFocusSessionEnded(true);
                    break;
                case "FocusCancel":
                case "Unlock":
                    RecordFocusSessionEnded(false);
                    break;
                case "CatTap":
                    RecordCatInteraction("cat_tap");
                    break;
                case "CatLongPress":
                    RecordCatInteraction("cat_long_press");
                    break;
                case "ScenePointTap":
                    RecordUiEvent("scene_" + SafeLabel(behaviorEvent.zoneId));
                    break;
                case "PageEnter":
                    RecordUiEvent("page_enter_" + SafeLabel(behaviorEvent.routeId));
                    break;
                case "PageExit":
                    RecordUiEvent("page_exit_" + SafeLabel(behaviorEvent.routeId));
                    break;
                case "UiButton":
                    RecordUiEvent("button_" + SafeLabel(behaviorEvent.routeId));
                    break;
                case "UiScroll":
                    RecordUiEvent("ui_scroll");
                    break;
                case "AppPause":
                    RecordUiEvent("app_pause");
                    break;
                case "AppResume":
                    RecordUiEvent("app_resume");
                    break;
                default:
                    RecordUiTap("ui_tap");
                    break;
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
            if (!collectionEnabled) return;
            quietDwellSeconds += Mathf.Max(0f, unscaledDeltaTime);
            if (!quietDwellReported && quietDwellSeconds >= 6f)
            {
                lastLocalEvent = "quiet_dwell";
                quietDwellReported = true;
            }
            float now = Time.unscaledTime;
            int tapCount1s = CountSince(tapTimes, now, 1f);
            int tapCount5s = CountSince(tapTimes, now, 5f);
            int pageSwitches30s = CountSince(pageSwitchTimes, now, 30f);
            float secondsSinceLastInteraction = lastInteractionTime > -100f ? now - lastInteractionTime : 999f;
            float secondsSinceLastFocusStart = focusSessionStartedAt > -100f ? now - focusSessionStartedAt : 999f;
            float tapRate1s = tapCount1s;
            float tapRate5s = tapCount5s / 5f;
            float distraction = Mathf.Clamp01(tapRate5s / 1.2f + pageSwitches30s / 6f);
            float arousal = Mathf.Clamp01(tapRate1s / 3f + pageSwitches30s / 8f);
            float quietScore = Mathf.Clamp01(secondsSinceLastInteraction / 12f);
            float focusScore = focusSessionActive
                ? Mathf.Clamp01(0.62f + quietScore * 0.34f - distraction * 0.5f)
                : Mathf.Clamp01(0.22f + quietScore * 0.62f - distraction * 0.45f);

            latest.realtimeSinceStartup = Time.realtimeSinceStartup;
            latest.isFocusSessionActive = focusSessionActive;
            latest.secondsSinceLastInteraction = secondsSinceLastInteraction;
            latest.secondsSinceLastFocusStart = secondsSinceLastFocusStart;
            latest.tapRate1s = tapRate1s;
            latest.tapRate5s = tapRate5s;
            latest.pageSwitches30s = pageSwitches30s;
            latest.focusScore01 = focusScore;
            latest.arousal01 = arousal;
            latest.distraction01 = distraction;
            latest.localEventSummary = string.Format(
                "event={0}; focusActive={1}; tap1s={2:F1}; tap5s={3:F1}; pages30s={4}; quiet={5:F1}",
                lastLocalEvent,
                focusSessionActive,
                tapRate1s,
                tapRate5s,
                pageSwitches30s,
                secondsSinceLastInteraction);
        }

        private void RecordInteraction(string eventName)
        {
            float now = Time.unscaledTime;
            lastInteractionTime = now;
            lastLocalEvent = eventName;
            quietDwellSeconds = 0f;
            quietDwellReported = false;
            WriteTimestamp(interactionTimes, ref interactionIndex, now);
            Tick(0f);
        }

        private static void WriteTimestamp(float[] values, ref int index, float time)
        {
            values[index] = time;
            index = (index + 1) % values.Length;
        }

        private static float CurrentTimestamp()
        {
            // PlayMode and a real first-frame tap can occur while unscaledTime is
            // exactly zero. Zero is the ring-buffer sentinel, so store a small
            // positive value instead of silently dropping that first input.
            return Mathf.Max(Time.unscaledTime, .0001f);
        }

        private static int CountSince(float[] values, float now, float windowSeconds)
        {
            int count = 0;
            for (int i = 0; i < values.Length; i++)
            {
                float value = values[i];
                if (value > 0f && now - value <= windowSeconds)
                {
                    count += 1;
                }
            }

            return count;
        }

        private static string SafeLabel(string value)
        {
            return string.IsNullOrEmpty(value) ? "none" : value;
        }
    }
}
