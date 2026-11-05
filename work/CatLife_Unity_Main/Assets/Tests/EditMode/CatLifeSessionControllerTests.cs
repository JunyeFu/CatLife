using System;
using NUnit.Framework;

namespace CatLife.Mobile.Tests
{
    public sealed class CatLifeSessionControllerTests
    {
        [Test]
        public void DisabledStatisticsSurviveRestoreWithoutChangingDeadlineOrRewards()
        {
            var data = new CatLifeAppData();
            data.settings.localBehaviorStatsEnabled = false;
            var controller = new CatLifeSessionController(data);
            controller.BeginTransition(60, 100);
            controller.EnterFocus(102);
            controller.RecordTouch();
            controller.RecordBackground(110);
            data = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
            controller = new CatLifeSessionController(data);
            controller.RecordForeground(130);
            Assert.That(controller.RemainingSeconds(130), Is.EqualTo(32));
            Assert.That(controller.TryComplete(162, out var record), Is.True);
            Assert.That(record.behaviorStatsDisabled, Is.True);
            Assert.That(record.touchCount, Is.Zero);
            Assert.That(record.backgroundCount, Is.Zero);
            Assert.That(record.longestStableSeconds, Is.Zero);
            Assert.That(record.GrowthAwarded, Is.EqualTo(1));
            Assert.That(record.PawsAwarded, Is.EqualTo(1));
            StringAssert.Contains("未采集", CatLifeInsightEngine.Create(record));
            StringAssert.DoesNotContain("稳定度", CatLifeInsightEngine.Create(record));
            Assert.That(CatLifeStats.Derive(data, 162).TodayHasBehaviorStats, Is.False);
        }

        [Test]
        public void DisabledStatisticsInterruptionStoresNoBehaviorAndNoReward()
        {
            var data = new CatLifeAppData();
            data.settings.localBehaviorStatsEnabled = false;
            var controller = new CatLifeSessionController(data);
            controller.BeginTransition(60, 100);
            controller.EnterFocus(102);
            controller.RecordTouch();
            controller.RecordBackground(110);
            controller.RecordForeground(120);
            var record = controller.Interrupt(130);
            Assert.That(record.behaviorStatsDisabled, Is.True);
            Assert.That(record.backgroundCount + record.touchCount + record.longestStableSeconds, Is.Zero);
            Assert.That(record.GrowthAwarded + record.PawsAwarded, Is.Zero);
            data = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
            Assert.That(data.records[0].behaviorStatsDisabled, Is.True);
        }

        [Test]
        public void FreshInstallDoesNotEnableAutoFocus()
        {
            CatLifeAppData data = new CatLifeAppData();

            Assert.That(data.settings.autoFocusAdaptationSeconds, Is.EqualTo(0));
        }

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
            Assert.That(restored.records[0].aiRequestCount, Is.EqualTo(1));
            Assert.That(new CatLifeSessionController(restored).TryMarkAiRequestAttempted(restored.records[0], "mimo_cloud_pending"), Is.False);
        }

        [TestCase("quiet_idle", 5, "focus_rest")]
        [TestCase("soft_roam", 8, "stretch")]
        [TestCase("social_response", 3, "tail_wag")]
        [TestCase("quiet_idle", 1, "paw_wave")]
        [TestCase("none", 0, "")]
        [TestCase("none", 1, "")]
        [TestCase("none", 8, "")]
        [TestCase("social_response", 1, "paw_wave")]
        [TestCase("soft_roam", 5, "paw_wave")]
        [TestCase("transform", 8, "")]
        [TestCase("social_response", 0, "")]
        public void AiRecommendationRespectsActionWhitelistAndUnlocks(string action, int paws, string expected)
        {
            Assert.That(CatLifeUnlocks.ResolveAiReaction(action, paws), Is.EqualTo(expected));
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
            data.settings.autoFocusAdaptationSeconds = 30;
            CatLifeAppData restored = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data));
            Assert.That(restored.settings.localBehaviorStatsEnabled, Is.False);
            Assert.That(restored.settings.autoFocusAdaptationSeconds, Is.EqualTo(30));
        }

        [Test]
        public void EmptyStatsHaveSevenZeroDaysAndNoInventedStability()
        {
            var stats = CatLifeStats.Derive(new CatLifeAppData(), 1791504000);
            Assert.That(stats.SevenDayMinutesByDay, Is.EqualTo(new int[7]));
            Assert.That(stats.TodayMinutes, Is.Zero);
            Assert.That(stats.TodayCompletedCount, Is.Zero);
            Assert.That(stats.TodayHasBehaviorStats, Is.False);
            Assert.That(stats.TodayLongestStableSeconds, Is.Zero);
            Assert.That(stats.TotalGrowth, Is.Zero);
            Assert.That(stats.Paws, Is.Zero);
            Assert.That(stats.Level, Is.EqualTo(1));
            Assert.That(stats.GrowthToNextLevel, Is.EqualTo(100));
        }

        [Test]
        public void LocalMidnightAndSevenDayEdgesUseEndDateAfterJsonRoundTrip()
        {
            var midnight = new DateTime(2026, 10, 10, 0, 0, 0, DateTimeKind.Local);
            long boundary = new DateTimeOffset(midnight).ToUnixTimeSeconds();
            var data = new CatLifeAppData();
            data.records.Add(new CatLifeSessionRecord { endedAt = boundary - 1, actualSeconds = 60, plannedSeconds = 60, completed = true, longestStableSeconds = 60 });
            data.records.Add(new CatLifeSessionRecord { endedAt = boundary + 1, actualSeconds = 120, plannedSeconds = 120, completed = true, behaviorStatsDisabled = true, longestStableSeconds = 999 });
            data.records.Add(new CatLifeSessionRecord { endedAt = boundary + 2, actualSeconds = 60, plannedSeconds = 600, completed = false, longestStableSeconds = 20 });
            data.records.Add(new CatLifeSessionRecord { endedAt = new DateTimeOffset(midnight.AddDays(-6)).ToUnixTimeSeconds(), actualSeconds = 180, plannedSeconds = 180, completed = true });
            data.records.Add(new CatLifeSessionRecord { endedAt = new DateTimeOffset(midnight.AddDays(-6)).ToUnixTimeSeconds() - 1, actualSeconds = 240, plannedSeconds = 240, completed = true });
            var stats = CatLifeStats.Derive(CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(data)), boundary + 30);
            Assert.That(stats.SevenDayMinutesByDay, Is.EqualTo(new[] { 3, 0, 0, 0, 0, 1, 3 }));
            Assert.That(stats.SevenDayMinutes, Is.EqualTo(7));
            Assert.That(stats.TodayMinutes, Is.EqualTo(3));
            Assert.That(stats.TodayCompletedCount, Is.EqualTo(1));
            Assert.That(stats.TodayLongestStableSeconds, Is.EqualTo(20));
            Assert.That(stats.TotalGrowth, Is.EqualTo(10));
            Assert.That(stats.Paws, Is.EqualTo(4)); // One paw per completed session, not per minute.
        }

        [Test]
        public void EmptyDataJsonRoundTripHasNoActiveSession()
        {
            var restored = CatLifeDataJson.Deserialize(CatLifeDataJson.Serialize(new CatLifeAppData()));
            Assert.That(restored.activeSession, Is.Null);
            Assert.That(new CatLifeSessionController(restored).Phase, Is.EqualTo(CatLifeSessionPhase.Normal));
        }

        [Test]
        public void AutoFocusRequiresContinuousStableDwellAndTriggersOnlyOnce()
        {
            Type policyType = Type.GetType("CatLife.Mobile.CatLifeAutoFocusPolicy, CatLife.Mobile.Core");
            Assert.That(policyType, Is.Not.Null);
            object policy = Activator.CreateInstance(policyType, new object[] { 2f, .70f });
            var shouldStart = policyType.GetMethod("ShouldStart");

            Assert.That((bool)shouldStart.Invoke(policy, new object[] { CatLifeSessionPhase.Normal, true, .80f, 1f }), Is.False);
            Assert.That((bool)shouldStart.Invoke(policy, new object[] { CatLifeSessionPhase.Normal, true, .80f, 1f }), Is.True);
            Assert.That((bool)shouldStart.Invoke(policy, new object[] { CatLifeSessionPhase.Normal, true, .80f, 10f }), Is.False);
            Assert.That((bool)shouldStart.Invoke(policy, new object[] { CatLifeSessionPhase.Transition, true, .80f, 10f }), Is.False);
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(120)]
        public void AutoFocusResetDropsElapsedDwellButDoesNotRearmConsumedPolicy(int duration)
        {
            var policy = new CatLifeAutoFocusPolicy(duration, .68f);
            Assert.That(policy.ShouldStart(CatLifeSessionPhase.Normal, true, .82f, duration - 1), Is.False);
            policy.ResetDwell();
            Assert.That(policy.ShouldStart(CatLifeSessionPhase.Normal, true, .82f, 1), Is.False);
            Assert.That(policy.ShouldStart(CatLifeSessionPhase.Normal, true, .82f, duration - 1), Is.True);
            policy.ResetDwell();
            Assert.That(policy.ShouldStart(CatLifeSessionPhase.Normal, true, .82f, duration), Is.False);
        }
    }
}
