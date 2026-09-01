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
        public int[] SevenDayMinutesByDay { get; internal set; } = new int[7];
    }

    public static class CatLifeStats
    {
        public static CatLifeDerivedStats Derive(CatLifeAppData data, long now)
        {
            DateTime today = DateTimeOffset.FromUnixTimeSeconds(now).ToLocalTime().Date;
            CatLifeDerivedStats stats = new CatLifeDerivedStats();
            foreach (CatLifeSessionRecord record in data.records)
            {
                DateTime day = DateTimeOffset.FromUnixTimeSeconds(record.endedAt).ToLocalTime().Date;
                int minutes = record.actualSeconds / 60;
                if (day == today)
                {
                    stats.TodayMinutes += minutes;
                    if (record.completed) stats.TodayCompletedCount += 1;
                    stats.TodayLongestStableSeconds = Math.Max(stats.TodayLongestStableSeconds, record.longestStableSeconds);
                }

                int dayOffset = (today - day).Days;
                if (dayOffset >= 0 && dayOffset <= 6)
                {
                    stats.SevenDayMinutes += minutes;
                    stats.SevenDayMinutesByDay[6 - dayOffset] += minutes;
                }

                stats.TotalGrowth += record.GrowthAwarded;
                stats.Paws += record.PawsAwarded;
            }

            stats.Level = stats.TotalGrowth / 100 + 1;
            stats.LevelProgress = stats.TotalGrowth % 100;
            stats.GrowthToNextLevel = 100 - stats.LevelProgress;

            return stats;
        }
    }

    public static class CatLifeUnlocks
    {
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
