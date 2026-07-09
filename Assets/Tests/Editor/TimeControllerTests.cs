using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class TimeControllerTests
    {
        private static IRandom Rng(int seed = 1) => SimTimeFixtures.Rng(seed);

        private static ResourceDef Money(int start = 100) => new ResourceDef("money", "재보", start);
        private static ResourceDef Magic(int start = 50) => new ResourceDef("magic", "마력", start);

        private static CommandDef Raid() => new CommandDef("raid", "약탈", new List<ResourceDelta>
        {
            new ResourceDelta("money", 50),
            new ResourceDelta("magic", -20),
        });

        private static LoopEngine Engine() => new LoopEngine(new TurnEngine(
            new List<ResourceDef> { Money(), Magic() },
            new List<CommandDef> { Raid() }));

        [Test]
        public void SkipToNextWave_StopsAtWaveEve()
        {
            var c = SimTimeFixtures.CampaignAtDay(3);
            var r = TimeController.SkipToNextWave(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(9, r.Campaign.Run.Day);   // 웨이브(10) 전날
            Assert.AreEqual(6, r.DaysAdvanced);        // 4,5,6,7,8,9
        }

        [Test]
        public void SkipToNextWave_DoesNotResolveWave()
        {
            var c = SimTimeFixtures.CampaignAtDay(3);
            var before = c.Run.Captives.Count;
            var r = TimeController.SkipToNextWave(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(9, r.Campaign.Run.Day);            // 10(웨이브)까지 안 감
            Assert.AreEqual(before, r.Campaign.Run.Captives.Count); // 전투 미발생
        }

        [Test]
        public void SkipToNextWave_Settlement_EqualsStepByStep()
        {
            var ctx = SimTimeFixtures.DayContext();
            var start = SimTimeFixtures.CampaignAtDay(3);

            var skip = TimeController.SkipToNextWave(start, ctx, Rng(7)).Campaign;

            var step = start;
            for (int i = 0; i < 6; i++) step = TimeController.Step(step, ctx, Rng(7)).Campaign;

            Assert.AreEqual(step.Run.Day, skip.Run.Day);
            Assert.AreEqual(step.Run.Resources["gold"], skip.Run.Resources["gold"]); // 정산 동일
            Assert.AreEqual(step.Meta.Inn.Decor, skip.Meta.Inn.Decor);
            Assert.AreEqual(step.Meta.KarmaBank, skip.Meta.KarmaBank);
        }

        [Test]
        public void SkipToNextWave_AtWaveEve_NoOp()
        {
            var c = SimTimeFixtures.CampaignAtDay(9);
            var r = TimeController.SkipToNextWave(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(0, r.DaysAdvanced);
            Assert.AreEqual(9, r.Campaign.Run.Day);
        }

        [Test]
        public void SkipToDay_ReachesTarget_WhenNoWaveBetween()
        {
            var c = SimTimeFixtures.CampaignAtDay(1);
            var r = TimeController.SkipToDay(c, SimTimeFixtures.DayContext(), Rng(), 5);
            Assert.AreEqual(5, r.Campaign.Run.Day);
            Assert.AreEqual(4, r.DaysAdvanced);
        }

        [Test]
        public void SkipToDay_StopsAtWaveEve_WhenWaveBeforeTarget()
        {
            var c = SimTimeFixtures.CampaignAtDay(5);
            var r = TimeController.SkipToDay(c, SimTimeFixtures.DayContext(), Rng(), 15);
            Assert.AreEqual(9, r.Campaign.Run.Day);   // 웨이브(10)를 넘지 않음
        }

        [Test]
        public void SkipToDay_InvalidTarget_Throws()
        {
            var c = SimTimeFixtures.CampaignAtDay(1);
            var ctx = SimTimeFixtures.DayContext();
            Assert.Throws<VnRuntimeException>(() => TimeController.SkipToDay(c, ctx, Rng(), 0));
            Assert.Throws<VnRuntimeException>(() => TimeController.SkipToDay(c, ctx, Rng(), 91));
        }

        [Test]
        public void SkipToDay_TargetNotFuture_NoOp()
        {
            var c = SimTimeFixtures.CampaignAtDay(5);
            var r = TimeController.SkipToDay(c, SimTimeFixtures.DayContext(), Rng(), 5);
            Assert.AreEqual(0, r.DaysAdvanced);
            Assert.AreEqual(5, r.Campaign.Run.Day);
        }

        [Test]
        public void Step_AtDay90_SignalsRegress_ThenStartNewLoopResetsDay()
        {
            var c = SimTimeFixtures.CampaignAtDay(90, karmaBank: 33);
            int loop = c.Meta.LoopCount;
            var r = TimeController.Step(c, SimTimeFixtures.DayContext(), Rng());
            Assert.IsTrue(r.RegressPending);

            var engine = Engine();
            var next = engine.StartNewLoop(r.Campaign);
            Assert.AreEqual(1, next.Run.Day);                 // 런 리셋
            Assert.AreEqual(loop + 1, next.Meta.LoopCount);   // 메타 유지+증가
            Assert.AreEqual(33, next.Meta.KarmaBank);         // 메타 유지
        }
    }
}
