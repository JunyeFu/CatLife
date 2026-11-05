using System;

namespace CatLife.Mobile
{
    public sealed class CatLifeDerivedStats
    {
        public int TodayMinutes { get; internal set; }
        public int SevenDayMinutes { get; internal set; }
        public int TotalGrowth { get; internal set; }
        public int Paws { get; internal set; }
        public int Level { get; internal set; }
        public int LevelProgress { get; internal set; }
        public int GrowthToNextLevel { get; internal set; }
        public int TodayCompletedCount { get; internal set; }
        public int TodayLongestStableSeconds { get; internal set; }
        public bool TodayHasBehaviorStats { get; internal set; }
        public int[] SevenDayMinutesByDay { get; internal set; } = new int[7];
    }

    public static class CatLifeStats
    {
        public static CatLifeDerivedStats Derive(CatLifeAppData data, long now)
        {
            DateTime today = DateTimeOffset.FromUnixTimeSeconds(now).ToLocalTime().Date;
            CatLifeDerivedStats stats = new CatLifeDerivedStats();
            long todaySeconds = 0, weekSeconds = 0;
            long[] daySeconds = new long[7];
            foreach (CatLifeSessionRecord record in data.records)
            {
                DateTime day = DateTimeOffset.FromUnixTimeSeconds(record.endedAt).ToLocalTime().Date;
                if (day == today)
                {
                    todaySeconds += record.actualSeconds;
                    if (record.completed) stats.TodayCompletedCount += 1;
                    if (!record.behaviorStatsDisabled)
                    {
                        stats.TodayHasBehaviorStats = true;
                        stats.TodayLongestStableSeconds = Math.Max(stats.TodayLongestStableSeconds, record.longestStableSeconds);
                    }
                }

                int dayOffset = (today - day).Days;
                if (dayOffset >= 0 && dayOffset <= 6)
                {
                    weekSeconds += record.actualSeconds;
                    daySeconds[6 - dayOffset] += record.actualSeconds;
                }

                stats.TotalGrowth += record.GrowthAwarded;
                stats.Paws += record.PawsAwarded;
            }

            stats.TodayMinutes = (int)(todaySeconds / 60);
            stats.SevenDayMinutes = (int)(weekSeconds / 60);
            for (int i = 0; i < 7; i++) stats.SevenDayMinutesByDay[i] = (int)(daySeconds[i] / 60);
            stats.Level = stats.TotalGrowth / 100 + 1;
            stats.LevelProgress = stats.TotalGrowth % 100;
            stats.GrowthToNextLevel = 100 - stats.LevelProgress;

            return stats;
        }
    }

    public static class CatLifeUnlocks
    {
        public static string ResolveAiReaction(string action, int paws)
        {
            string requested;
            switch (action)
            {
                case "quiet_idle": requested = "focus_rest"; break;
                case "soft_roam": requested = "stretch"; break;
                case "social_response": requested = "tail_wag"; break;
                case "none": return string.Empty;
                default: return string.Empty;
            }
            return SelectUnlockedReaction(requested, paws);
        }

        public static string ReactionLabel(string action)
        {
            switch (action)
            {
                case "paw_wave": return "挥爪回应";
                case "tail_wag": return "轻摆尾";
                case "focus_rest": return "安静休息";
                case "stretch": return "伸个懒腰";
                default: return "暂无动作";
            }
        }

        public static string[] GetUnlockedReactions(int paws)
        {
            if (paws >= 8)
            {
                return new[] { "paw_wave", "tail_wag", "focus_rest", "stretch" };
            }

            if (paws >= 5)
            {
                return new[] { "paw_wave", "tail_wag", "focus_rest" };
            }

            if (paws >= 3)
            {
                return new[] { "paw_wave", "tail_wag" };
            }

            if (paws >= 1)
            {
                return new[] { "paw_wave" };
            }

            return Array.Empty<string>();
        }

        public static string SelectUnlockedReaction(string requested, int paws)
        {
            string[] unlocked = GetUnlockedReactions(paws);
            foreach (string reaction in unlocked)
            {
                if (reaction == requested)
                {
                    return reaction;
                }
            }

            return unlocked.Length == 0 ? string.Empty : unlocked[0];
        }
    }

    public static class CatLifeInsightEngine
    {
        public static string Create(CatLifeSessionRecord record)
        {
            int minutes = record.actualSeconds / 60;
            if (!record.completed)
            {
                return "本次专注提前结束，记录已保存；下一轮可以选择更短时长。";
            }

            if (record.behaviorStatsDisabled)
                return string.Format("完成 {0} 分钟；行为统计未采集，下一轮从一个小步骤开始。", minutes);

            if (record.backgroundCount > 0)
            {
                return string.Format(
                    "完成 {0} 分钟，稳定度 {1}%；中途返回 {2} 次，下一轮继续保持。",
                    minutes,
                    record.StabilityPercent,
                    record.backgroundCount);
            }

            return string.Format(
                "完成 {0} 分钟，稳定度 {1}%；这一段节奏很稳。",
                minutes,
                record.StabilityPercent);
        }
    }
}
