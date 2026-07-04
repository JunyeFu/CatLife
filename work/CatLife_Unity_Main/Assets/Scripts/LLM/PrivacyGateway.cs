using UnityEngine;

namespace CatLife.LLM
{
    [DisallowMultipleComponent]
    public sealed class PrivacyGateway : MonoBehaviour
    {
        [SerializeField] private int maxDurationSeconds = 24 * 60 * 60;

        public bool TryValidate(BehaviorFeatureSummary summary, out string reason)
        {
            if (summary.HasBlockedPrivacyFields())
            {
                reason = "blocked_privacy_fields";
                return false;
            }

            if (summary.durationSec <= 0 || summary.durationSec > maxDurationSeconds)
            {
                reason = "invalid_duration";
                return false;
            }

            if (summary.focusDurationSec < 0 || summary.focusDurationSec > summary.durationSec)
            {
                reason = "invalid_focus_duration";
                return false;
            }

            if (summary.interruptCount < 0 || summary.completedSessionsToday < 0 || summary.todayFocusMinutes < 0)
            {
                reason = "invalid_negative_counter";
                return false;
            }

            reason = "passed";
            return true;
        }

        public BehaviorFeatureSummary Sanitize(BehaviorFeatureSummary summary)
        {
            summary.schemaVersion = string.IsNullOrEmpty(summary.schemaVersion)
                ? "catlife.focus_summary.v1"
                : summary.schemaVersion;
            summary.locale = "zh-CN";
            summary.durationSec = Mathf.Clamp(summary.durationSec, 1, maxDurationSeconds);
            summary.focusDurationSec = Mathf.Clamp(summary.focusDurationSec, 0, summary.durationSec);
            summary.interruptCount = Mathf.Max(0, summary.interruptCount);
            summary.completedSessionsToday = Mathf.Max(0, summary.completedSessionsToday);
            summary.todayFocusMinutes = Mathf.Max(0, summary.todayFocusMinutes);
            summary.focusScoreAvg01 = Mathf.Clamp01(summary.focusScoreAvg01);
            summary.arousalScoreAvg01 = Mathf.Clamp01(summary.arousalScoreAvg01);
            summary.distractionScoreAvg01 = Mathf.Clamp01(summary.distractionScoreAvg01);
            summary.longestFocusSec = Mathf.Max(0, summary.longestFocusSec);
            summary.rawTextIncluded = false;
            summary.rawTouchPathIncluded = false;
            summary.screenContentIncluded = false;
            summary.crossAppContentIncluded = false;
            return summary;
        }
    }
}
