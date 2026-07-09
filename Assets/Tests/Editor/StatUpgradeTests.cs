using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class StatUpgradeTests
    {
        // 07 문서 §13.3 초기추정 곡선 경계값 대조 (sim_8stat.py stat_cost 기준).
        [TestCase(0, 1)]
        [TestCase(99, 1)]
        [TestCase(100, 2)]
        [TestCase(249, 2)]
        [TestCase(250, 3)]
        [TestCase(449, 3)]
        [TestCase(450, 5)]
        [TestCase(649, 5)]
        [TestCase(650, 9)]
        [TestCase(799, 9)]
        [TestCase(800, 16)]
        [TestCase(949, 16)]
        [TestCase(950, 28)]
        [TestCase(998, 28)]
        public void DefaultCurveCostBoundaries(int cur, int expectedCost)
        {
            Assert.AreEqual(expectedCost, StatCostCurve.Default().CostAt(cur));
        }

        private static StatDef Def(StatId id, int cap = 999) => new StatDef(id, id.Value, 5, cap);

        [Test]
        public void UpgradeSpendsPerPointAndStopsWhenKarmaInsufficient()
        {
            // cur=98, karma=5: 98->99(1) 99->100(1) 100->101(2)=누적4, 다음 2 필요한데 1 남음 -> 정지.
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 98 } });
            var r = StatUpgrade.Upgrade(stats, Def(StatIds.STR), StatCostCurve.Default(), 5);
            Assert.AreEqual(3, r.PointsGained);
            Assert.AreEqual(4, r.KarmaSpent);
            Assert.AreEqual(101, r.Stats.Get(StatIds.STR));
        }

        [Test]
        public void UpgradeDoesNotMutateInput()
        {
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 98 } });
            StatUpgrade.Upgrade(stats, Def(StatIds.STR), StatCostCurve.Default(), 100);
            Assert.AreEqual(98, stats.Get(StatIds.STR), "입력 HeroStats 불변");
        }

        [Test]
        public void UpgradeCannotExceedCap()
        {
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 998 } });
            var r = StatUpgrade.Upgrade(stats, Def(StatIds.STR, cap: 999), StatCostCurve.Default(), 100000);
            Assert.AreEqual(999, r.Stats.Get(StatIds.STR));
            Assert.AreEqual(1, r.PointsGained);
            Assert.AreEqual(28, r.KarmaSpent);
        }

        [Test]
        public void ZeroKarmaGainsNothing()
        {
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 5 } });
            var r = StatUpgrade.Upgrade(stats, Def(StatIds.STR), StatCostCurve.Default(), 0);
            Assert.AreEqual(0, r.PointsGained);
            Assert.AreEqual(0, r.KarmaSpent);
            Assert.AreSame(stats, r.Stats, "변화 없으면 원본 그대로 반환");
        }

        [Test]
        public void SeedAndGrow_AbsentStat_StartsAtStartValueAndGrows()
        {
            // stats 에 STR 이 아예 없음(부재) -> Upgrade 는 def.StartValue(5) 부터 성장.
            var stats = HeroStats.Empty;
            var def = Def(StatIds.STR); // StartValue=5, Cap=999
            var r = StatUpgrade.Upgrade(stats, def, StatCostCurve.Default(), 5);
            Assert.IsTrue(r.PointsGained > 0);
            Assert.Greater(r.Stats.Get(StatIds.STR), def.StartValue);
            Assert.IsTrue(r.Stats.Get(StatIds.STR) >= def.StartValue);
        }

        [Test]
        public void SimCrossCheckKnownKarmaBudget()
        {
            // 5에서 karma=99 투입: 5..99 각 cost1(95점, 누적95), 100->101 cost2(97), 101->102 cost2(99) -> 정지.
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 5 } });
            var r = StatUpgrade.Upgrade(stats, Def(StatIds.STR), StatCostCurve.Default(), 99);
            Assert.AreEqual(97, r.PointsGained);
            Assert.AreEqual(99, r.KarmaSpent);
            Assert.AreEqual(102, r.Stats.Get(StatIds.STR));
        }
    }
}
