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
        private RealtimeFeatureSnapshot latest;

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
            string safeEventName = string.IsNullOrEmpty(eventName) ? "ui_event" : eventName;
            RecordInteraction(safeEventName);
            if (safeEventName.Contains("page"))
            {
                WriteTimestamp(pageSwitchTimes, ref pageSwitchIndex, Time.unscaledTime);
            }
        }

        public void RecordCatInteraction(string eventName)
        {
            RecordInteraction(string.IsNullOrEmpty(eventName) ? "cat_interaction" : eventName);
            WriteTimestamp(tapTimes, ref tapIndex, Time.unscaledTime);
        }

        public void RecordFocusSessionStarted()
        {
            focusSessionActive = true;
            focusSessionStartedAt = Time.unscaledTime;
            RecordInteraction("focus_started");
        }

        public void RecordFocusSessionEnded(bool completed)
        {
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
                    RecordUiEvent("ui_tap");
                    break;
            }
        }

        public void Tick(float unscaledDeltaTime)
        {
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
                : Mathf.Clamp01(0.28f + quietScore * 0.08f - distraction * 0.22f);

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
            WriteTimestamp(interactionTimes, ref interactionIndex, now);
            Tick(0f);
        }

        private static void WriteTimestamp(float[] values, ref int index, float time)
        {
            values[index] = time;
            index = (index + 1) % values.Length;
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
