using System;
using NUnit.Framework;

namespace CatLife.Mobile.Tests
{
    public sealed class CatLifeSessionControllerTests
    {
        [Test]
        public void CompletedSessionDrivesApprovedFlowAndRewards()
        {
            CatLifeAppData data = new CatLifeAppData();
            CatLifeSessionController controller = new CatLifeSessionController(data);

            controller.BeginTransition(25 * 60, 100);
            Assert.That(controller.Phase, Is.EqualTo(CatLifeSessionPhase.Transition));

            controller.EnterFocus(102);
            Assert.That(controller.Phase, Is.EqualTo(CatLifeSessionPhase.Focus));
            Assert.That(controller.RemainingSeconds(102), Is.EqualTo(25 * 60));

            bool completed = controller.TryComplete(1602, out CatLifeSessionRecord record);

            Assert.That(completed, Is.True);
            Assert.That(controller.Phase, Is.EqualTo(CatLifeSessionPhase.Reward));
            Assert.That(record.Completed, Is.True);
            Assert.That(record.ActualSeconds, Is.EqualTo(25 * 60));
            Assert.That(record.GrowthAwarded, Is.EqualTo(25));
            Assert.That(record.PawsAwarded, Is.EqualTo(1));
            Assert.That(data.Records, Has.Count.EqualTo(1));
        }

        [Test]
        public void BackgroundKeepsTimerAndResetsStableSegmentAcrossRestore()
        {
            CatLifeAppData data = new CatLifeAppData();
            CatLifeSessionController controller = new CatLifeSessionController(data);
            controller.BeginTransition(10 * 60, 0);
            controller.EnterFocus(2);

            controller.RecordBackground(122);
            CatLifeSessionController restored = new CatLifeSessionController(data);

            Assert.That(restored.Phase, Is.EqualTo(CatLifeSessionPhase.Focus));
            Assert.That(restored.RemainingSeconds(302), Is.EqualTo(300));

            restored.RecordForeground(302);
            bool completed = restored.TryComplete(602, out CatLifeSessionRecord record);

            Assert.That(completed, Is.True);
            Assert.That(record.BackgroundCount, Is.EqualTo(1));
            Assert.That(record.LongestStableSeconds, Is.EqualTo(300));
            Assert.That(record.StabilityPercent, Is.EqualTo(50));
        }

        [Test]
        public void InterruptedSessionIsRecordedWithoutRewards()
        {
            CatLifeAppData data = new CatLifeAppData();
            CatLifeSessionController controller = new CatLifeSessionController(data);
            controller.BeginTransition(15 * 60, 0);
            controller.EnterFocus(2);
            controller.RecordTouch();
            controller.RecordTouch();

            CatLifeSessionRecord record = controller.Interrupt(122);

            Assert.That(controller.Phase, Is.EqualTo(CatLifeSessionPhase.Normal));
            Assert.That(record.Completed, Is.False);
            Assert.That(record.ActualSeconds, Is.EqualTo(120));
            Assert.That(record.TouchCount, Is.EqualTo(2));
            Assert.That(record.GrowthAwarded, Is.Zero);
            Assert.That(record.PawsAwarded, Is.Zero);
            Assert.That(data.Records, Has.Count.EqualTo(1));
        }

        [Test]
        public void RecordsDeriveGrowthPawsSevenDayMinutesAndLocalInsight()
        {
            CatLifeAppData data = new CatLifeAppData();
            data.records.Add(new CatLifeSessionRecord
            {
                endedAt = 10 * 86400 + 100,
                plannedSeconds = 25 * 60,
                actualSeconds = 25 * 60,
                completed = true,
                backgroundCount = 1,
                longestStableSeconds = 21 * 60 + 30
            });
            data.records.Add(new CatLifeSessionRecord
            {
                endedAt = 10 * 86400 + 200,
                plannedSeconds = 15 * 60,
                actualSeconds = 5 * 60,
                completed = false,
                longestStableSeconds = 5 * 60
            });
            data.records.Add(new CatLifeSessionRecord
            {
                endedAt = 8 * 86400,
                plannedSeconds = 15 * 60,
                actualSeconds = 15 * 60,
                completed = true,
                longestStableSeconds = 15 * 60
            });

            CatLifeDerivedStats stats = CatLifeStats.Derive(data, 10 * 86400 + 300);
            string insight = CatLifeInsightEngine.Create(data.records[0]);

            Assert.That(stats.TodayMinutes, Is.EqualTo(30));
            Assert.That(stats.SevenDayMinutes, Is.EqualTo(45));
            Assert.That(stats.TotalGrowth, Is.EqualTo(40));
            Assert.That(stats.Paws, Is.EqualTo(2));
            Assert.That(stats.Level, Is.EqualTo(1));
            Assert.That(stats.LevelProgress, Is.EqualTo(40));
            Assert.That(stats.GrowthToNextLevel, Is.EqualTo(60));
            Assert.That(CatLifeUnlocks.GetUnlockedReactions(stats.Paws), Is.EqualTo(new[] { "paw_wave" }));
            Assert.That(insight, Is.EqualTo("完成 25 分钟，稳定度 86%；中途返回 1 次，下一轮继续保持。"));
            Assert.That(data.settings.aiEnabled, Is.False);
        }

        [Test]
        public void JsonRoundTripRestoresActiveDeadlineAndSettings()
        {
            CatLifeAppData data = new CatLifeAppData();
            data.settings.aiEnabled = true;
            CatLifeSessionController controller = new CatLifeSessionController(data);
            controller.BeginTransition(45 * 60, 100);
            controller.EnterFocus(102);

            string json = CatLifeDataJson.Serialize(data);
            CatLifeAppData restoredData = CatLifeDataJson.Deserialize(json);
            CatLifeSessionController restoredController = new CatLifeSessionController(restoredData);

            Assert.That(restoredController.Phase, Is.EqualTo(CatLifeSessionPhase.Focus));
            Assert.That(restoredController.RemainingSeconds(202), Is.EqualTo(43 * 60 + 20));
            Assert.That(restoredData.settings.aiEnabled, Is.True);
        }

        [Test]
        public void CompletedSessionCanAttemptAiOnlyOnceAcrossJsonRestore()
        {
            CatLifeAppData data = new CatLifeAppData();
            CatLifeSessionController controller = new CatLifeSessionController(data);
            controller.BeginTransition(60, 100);
            controller.EnterFocus(102);
            controller.TryComplete(162, out CatLifeSessionRecord record);

            Assert.That(controller.TryMarkAiRequestAttempted(record, "mimo_cloud_pending"), Is.True);
            Assert.That(controller.TryMarkAiRequestAttempted(record, "mimo_cloud_pending"), Is.False);

            CatLifeAppData restored = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
            Assert.That(restored.records[0].aiRequestAttempted, Is.True);
            Assert.That(restored.records[0].aiSource, Is.EqualTo("mimo_cloud_pending"));
        }

        [Test]
        public void DerivedStatsExposeTodayCountsLongestAndSevenLocalDayBuckets()
        {
            DateTime localDay = new DateTime(2026, 9, 1, 12, 0, 0, DateTimeKind.Local);
            long now = new DateTimeOffset(localDay).ToUnixTimeSeconds();
            CatLifeAppData data = new CatLifeAppData();
            data.records.Add(new CatLifeSessionRecord { endedAt = now - 60, actualSeconds = 600, plannedSeconds = 600, completed = true, longestStableSeconds = 480 });
            data.records.Add(new CatLifeSessionRecord { endedAt = now - 120, actualSeconds = 180, plannedSeconds = 900, completed = false, longestStableSeconds = 180 });
            data.records.Add(new CatLifeSessionRecord { endedAt = now - 2 * 86400, actualSeconds = 300, plannedSeconds = 300, completed = true, longestStableSeconds = 300 });

            CatLifeDerivedStats stats = CatLifeStats.Derive(data, now);

            Assert.That(stats.TodayMinutes, Is.EqualTo(13));
            Assert.That(stats.TodayCompletedCount, Is.EqualTo(1));
            Assert.That(stats.TodayLongestStableSeconds, Is.EqualTo(480));
            Assert.That(stats.SevenDayMinutesByDay, Has.Length.EqualTo(7));
            Assert.That(stats.SevenDayMinutesByDay[4], Is.EqualTo(5));
            Assert.That(stats.SevenDayMinutesByDay[6], Is.EqualTo(13));
        }

        [Test]
        public void AppSettingsRoundTripLocalBehaviorStatistics()
        {
            CatLifeAppData data = new CatLifeAppData();
            data.settings.localBehaviorStatsEnabled = false;
            CatLifeAppData restored = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
            Assert.That(restored.settings.localBehaviorStatsEnabled, Is.False);
        }
    }
}
