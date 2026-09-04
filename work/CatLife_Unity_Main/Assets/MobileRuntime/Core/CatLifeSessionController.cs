using System;
using System.Collections.Generic;

namespace CatLife.Mobile
{
    public enum CatLifeSessionPhase
    {
        Normal,
        Transition,
        Focus,
        Reward
    }

    [Serializable]
    public sealed class CatLifeAppData
    {
        public List<CatLifeSessionRecord> records = new List<CatLifeSessionRecord>();
        public CatLifeActiveSession activeSession;
        public CatLifeAppSettings settings = new CatLifeAppSettings();

        public IReadOnlyList<CatLifeSessionRecord> Records => records;
    }

    [Serializable]
    public sealed class CatLifeAppSettings
    {
        public int defaultMinutes = 25;
        public string reminderMode = "quiet";
        public bool aiEnabled;
        public bool aiConsent;
        public bool localBehaviorStatsEnabled = true;
        public int autoFocusAdaptationSeconds = 12;
    }

    [Serializable]
    public sealed class CatLifeActiveSession
    {
        public CatLifeSessionPhase phase;
        public int plannedSeconds;
        public long focusStartedAt;
        public long targetEndAt;
        public long stableSegmentStartedAt;
        public int touchCount;
        public int backgroundCount;
        public int longestStableSeconds;
        public bool foreground;
        public string stateSequence;
    }

    [Serializable]
    public sealed class CatLifeSessionRecord
    {
        public long startedAt;
        public long endedAt;
        public int plannedSeconds;
        public int actualSeconds;
        public bool completed;
        public int touchCount;
        public int backgroundCount;
        public int longestStableSeconds;
        public string stateSequence;
        public string localInsight;
        public string aiAdvice;
        public string aiReaction;
        public bool aiRequestAttempted;
        public string aiSource;

        public bool Completed => completed;
        public int ActualSeconds => actualSeconds;
        public int BackgroundCount => backgroundCount;
        public int LongestStableSeconds => longestStableSeconds;
        public int TouchCount => touchCount;
        public int GrowthAwarded => completed ? Math.Max(1, plannedSeconds / 60) : 0;
        public int PawsAwarded => completed ? 1 : 0;
        public int StabilityPercent => actualSeconds > 0
            ? Math.Min(100, (int)Math.Round(longestStableSeconds * 100d / actualSeconds))
            : 0;
    }

    public sealed class CatLifeSessionController
    {
        private readonly CatLifeAppData data;
        private CatLifeSessionPhase phase;

        public CatLifeSessionController(CatLifeAppData data)
        {
            this.data = data ?? throw new ArgumentNullException(nameof(data));
            phase = data.activeSession == null ? CatLifeSessionPhase.Normal : data.activeSession.phase;
        }

        public CatLifeSessionPhase Phase => phase;

        public void BeginTransition(int plannedSeconds, long now)
        {
            data.activeSession = new CatLifeActiveSession
            {
                phase = CatLifeSessionPhase.Transition,
                plannedSeconds = plannedSeconds,
                stateSequence = "Normal>Transition"
            };
            phase = CatLifeSessionPhase.Transition;
        }

        public void EnterFocus(long now)
        {
            CatLifeActiveSession active = data.activeSession;
            active.phase = CatLifeSessionPhase.Focus;
            active.focusStartedAt = now;
            active.targetEndAt = now + active.plannedSeconds;
            active.stableSegmentStartedAt = now;
            active.foreground = true;
            active.stateSequence = "Normal>Transition>Focus";
            phase = CatLifeSessionPhase.Focus;
        }

        public void CancelTransition()
        {
            if (phase != CatLifeSessionPhase.Transition)
            {
                return;
            }

            data.activeSession = null;
            phase = CatLifeSessionPhase.Normal;
        }

        public int RemainingSeconds(long now)
        {
            return phase == CatLifeSessionPhase.Focus
                ? (int)Math.Max(0, data.activeSession.targetEndAt - now)
                : 0;
        }

        public void RecordBackground(long now)
        {
            CatLifeActiveSession active = data.activeSession;
            if (phase != CatLifeSessionPhase.Focus || !active.foreground)
            {
                return;
            }

            int stableSeconds = (int)Math.Max(0, Math.Min(now, active.targetEndAt) - active.stableSegmentStartedAt);
            active.longestStableSeconds = Math.Max(active.longestStableSeconds, stableSeconds);
            active.backgroundCount += 1;
            active.foreground = false;
        }

        public void RecordForeground(long now)
        {
            CatLifeActiveSession active = data.activeSession;
            if (phase != CatLifeSessionPhase.Focus)
            {
                return;
            }

            active.stableSegmentStartedAt = now;
            active.foreground = true;
        }

        public void RecordTouch()
        {
            if (phase == CatLifeSessionPhase.Focus)
            {
                data.activeSession.touchCount += 1;
            }
        }

        public CatLifeSessionRecord Interrupt(long now)
        {
            CatLifeActiveSession active = data.activeSession;
            if (active.foreground)
            {
                int stableSeconds = (int)Math.Max(0, now - active.stableSegmentStartedAt);
                active.longestStableSeconds = Math.Max(active.longestStableSeconds, stableSeconds);
            }

            int actualSeconds = (int)Math.Max(0, Math.Min(active.plannedSeconds, now - active.focusStartedAt));
            CatLifeSessionRecord record = new CatLifeSessionRecord
            {
                startedAt = active.focusStartedAt,
                endedAt = now,
                plannedSeconds = active.plannedSeconds,
                actualSeconds = actualSeconds,
                completed = false,
                touchCount = active.touchCount,
                backgroundCount = active.backgroundCount,
                longestStableSeconds = active.longestStableSeconds,
                stateSequence = active.stateSequence + ">Normal"
            };
            data.records.Add(record);
            data.activeSession = null;
            phase = CatLifeSessionPhase.Normal;
            return record;
        }

        public bool TryComplete(long now, out CatLifeSessionRecord record)
        {
            record = null;
            CatLifeActiveSession active = data.activeSession;
            if (phase != CatLifeSessionPhase.Focus || now < active.targetEndAt)
            {
                return false;
            }

            if (active.foreground)
            {
                int stableSeconds = (int)Math.Max(0, active.targetEndAt - active.stableSegmentStartedAt);
                active.longestStableSeconds = Math.Max(active.longestStableSeconds, stableSeconds);
            }
            record = new CatLifeSessionRecord
            {
                startedAt = active.focusStartedAt,
                endedAt = active.targetEndAt,
                plannedSeconds = active.plannedSeconds,
                actualSeconds = active.plannedSeconds,
                completed = true,
                touchCount = active.touchCount,
                backgroundCount = active.backgroundCount,
                longestStableSeconds = active.longestStableSeconds,
                stateSequence = active.stateSequence + ">Reward"
            };
            data.records.Add(record);
            data.activeSession = null;
            phase = CatLifeSessionPhase.Reward;
            return true;
        }

        public void ReturnToTown()
        {
            phase = CatLifeSessionPhase.Normal;
        }

        public bool TryMarkAiRequestAttempted(CatLifeSessionRecord record, string source)
        {
            if (record == null || !record.completed || record.aiRequestAttempted)
            {
                return false;
            }

            record.aiRequestAttempted = true;
            record.aiSource = source ?? string.Empty;
            return true;
        }
    }
}
