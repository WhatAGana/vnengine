using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CampaignDayRuleTests
    {
        private static IRandom Rng(int seed = 1) => SimTimeFixtures.Rng(seed);

        [Test]
        public void AdvanceDay_IncrementsDayByOne()
        {
            var c = SimTimeFixtures.CampaignAtDay(1);
            var r = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(2, r.Campaign.Run.Day);
        }

        [Test]
        public void AdvanceDay_MaintenanceDay_AppliesInnTick_NoWave()
        {
            var c = SimTimeFixtures.CampaignAtDay(1); // ->day2 정비
            int decorBefore = c.Meta.Inn.Decor;
            var r = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(DayPhase.Maintenance, r.Phase);
            Assert.IsFalse(r.WaveResolved);
            Assert.AreEqual(2, r.Campaign.Run.Day);
            Assert.AreEqual(decorBefore - 1, r.Campaign.Meta.Inn.Decor, "여관 Decor가 하루치 감쇠");
            Assert.Greater(r.Campaign.Run.Resources["gold"], c.Run.Resources["gold"], "정비 수급으로 gold 증가");
        }

        [Test]
        public void AdvanceDay_WaveDay_ResolvesWave()
        {
            var c = SimTimeFixtures.CampaignAtDay(9); // ->day10 웨이브
            var r = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(DayPhase.Wave, r.Phase);
            Assert.IsTrue(r.WaveResolved);
            Assert.AreEqual(10, r.Campaign.Run.Day);
            Assert.Greater(r.Wave.GoldGained, 0, "전투 약탈로 골드 획득");
            Assert.AreEqual(c.Run.Resources["gold"] + r.Wave.GoldGained + r.Wave.InnGoldGained,
                r.Campaign.Run.Resources["gold"], "gold가 약탈+여관 수급만큼 증가");
        }

        [Test]
        public void AdvanceDay_Day90To91_SignalsRegress_NoProcessing()
        {
            var c = SimTimeFixtures.CampaignAtDay(90);
            var r = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.IsTrue(r.RegressPending);
            Assert.IsFalse(r.WaveResolved);
            Assert.AreEqual(90, r.Campaign.Run.Day); // 91 처리 안 함, 그대로
            Assert.AreSame(c, r.Campaign, "회귀신호 시 처리 전 캠페인 그대로 반환");
        }

        [Test]
        public void AdvanceDay_Deterministic_SameSeedSameResult()
        {
            var c = SimTimeFixtures.CampaignAtDay(9);
            var a = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng(42));
            var b = CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng(42));
            Assert.AreEqual(a.Campaign.Run.Resources["gold"], b.Campaign.Run.Resources["gold"]);
            Assert.AreEqual(a.Campaign.Run.Captives.Count, b.Campaign.Run.Captives.Count);
        }

        [Test]
        public void AdvanceDay_DoesNotMutateInputCampaign()
        {
            var c = SimTimeFixtures.CampaignAtDay(9);
            int day = c.Run.Day;
            int gold = c.Run.Resources["gold"];
            CampaignDayRule.AdvanceDay(c, SimTimeFixtures.DayContext(), Rng());
            Assert.AreEqual(day, c.Run.Day); // 원본 Day 불변
            Assert.AreEqual(gold, c.Run.Resources["gold"]); // 원본 자원 불변
        }

        [Test]
        public void AdvanceDay_NullArgs_Throw()
        {
            var c = SimTimeFixtures.CampaignAtDay(1);
            var ctx = SimTimeFixtures.DayContext();
            Assert.Throws<System.ArgumentNullException>(() => CampaignDayRule.AdvanceDay(null, ctx, Rng()));
            Assert.Throws<System.ArgumentNullException>(() => CampaignDayRule.AdvanceDay(c, null, Rng()));
            Assert.Throws<System.ArgumentNullException>(() => CampaignDayRule.AdvanceDay(c, ctx, null));
        }
    }
}
