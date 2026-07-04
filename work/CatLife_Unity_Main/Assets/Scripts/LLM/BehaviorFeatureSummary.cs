using System;
using UnityEngine;

namespace CatLife.LLM
{
    [Serializable]
    public struct BehaviorFeatureSummary
    {
        public string schemaVersion;
        public string sessionId;
        public string locale;
        public int durationSec;
        public int focusDurationSec;
        public int interruptCount;
        public int completedSessionsToday;
        public int todayFocusMinutes;
        [Range(0f, 1f)] public float focusScoreAvg01;
        [Range(0f, 1f)] public float arousalScoreAvg01;
        [Range(0f, 1f)] public float distractionScoreAvg01;
        public int longestFocusSec;
        public string catStateSequence;
        public bool rawTextIncluded;
        public bool rawTouchPathIncluded;
        public bool screenContentIncluded;
        public bool crossAppContentIncluded;

        public static BehaviorFeatureSummary CreateLocalSession(
            string sessionId,
            int durationSec,
            int focusDurationSec,
            int interruptCount,
            int completedSessionsToday,
            int todayFocusMinutes,
            int longestFocusSec,
            bool completed)
        {
            int safeDuration = Mathf.Max(1, durationSec);
            int safeFocusDuration = Mathf.Clamp(focusDurationSec, 0, safeDuration);
            float focusRatio = Mathf.Clamp01(safeFocusDuration / (float)safeDuration);
            float interruptionPressure = Mathf.Clamp01(interruptCount / 4f);

            return new BehaviorFeatureSummary
            {
                schemaVersion = "catlife.focus_summary.v1",
                sessionId = string.IsNullOrEmpty(sessionId) ? "local-session" : sessionId,
                locale = "zh-CN",
                durationSec = safeDuration,
                focusDurationSec = safeFocusDuration,
                interruptCount = Mathf.Max(0, interruptCount),
                completedSessionsToday = Mathf.Max(0, completedSessionsToday),
                todayFocusMinutes = Mathf.Max(0, todayFocusMinutes),
                focusScoreAvg01 = Mathf.Clamp01(completed ? Mathf.Max(0.68f, focusRatio) : focusRatio * 0.72f),
                arousalScoreAvg01 = Mathf.Clamp01(0.18f + interruptionPressure * 0.42f),
                distractionScoreAvg01 = interruptionPressure,
                longestFocusSec = Mathf.Max(0, longestFocusSec),
                catStateSequence = completed ? "NORMAL>FOCUS>REWARD" : "NORMAL>FOCUS>UNLOCKED",
                rawTextIncluded = false,
                rawTouchPathIncluded = false,
                screenContentIncluded = false,
                crossAppContentIncluded = false
            };
        }

        public bool HasBlockedPrivacyFields()
        {
            return rawTextIncluded ||
                rawTouchPathIncluded ||
                screenContentIncluded ||
                crossAppContentIncluded;
        }
    }
}
