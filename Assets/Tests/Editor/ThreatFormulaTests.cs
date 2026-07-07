using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class ThreatFormulaTests
    {
        private static HeroStats Stats(params (StatId id, int val)[] entries)
        {
            var d = new Dictionary<StatId, int>();
            foreach (var e in entries) d[e.id] = e.val;
            return new HeroStats(d);
        }

        [Test]
        public void ComputeSumsWeightedTermsPlusOffset()
        {
            var w = new ThreatWeights(wHero: 2, wLoop: 8, wPlaced: 1, wDungeon: 3, baseOffset: 20);
            // heroPower=10, loopCount=3, avgPlaced=5, dungeonLevel=4
            // = 2*10 + 8*(3-1) + 1*5 + 3*4 + 20 = 20 + 16 + 5 + 12 + 20 = 73
            var result = ThreatFormula.Compute(w, heroPower: 10, loopCount: 3, avgPlacedMonsterLevel: 5, dungeonLevel: 4);
            Assert.AreEqual(73, result);
        }

        [Test]
        public void LoopCountOneMakesLoopTermZero()
        {
            var w = ThreatWeights.Default();
            var withLoop1 = ThreatFormula.Compute(w, heroPower: 0, loopCount: 1, avgPlacedMonsterLevel: 0, dungeonLevel: 0);
            Assert.AreEqual(w.BaseOffset, withLoop1);
        }

        [Test]
        public void EachTermContributesIndependently()
        {
            var w = ThreatWeights.Default();
            var baseline = ThreatFormula.Compute(w, 0, 1, 0, 0);
            var heroOnly = ThreatFormula.Compute(w, 10, 1, 0, 0);
            var loopOnly = ThreatFormula.Compute(w, 0, 4, 0, 0);
            var placedOnly = ThreatFormula.Compute(w, 0, 1, 5, 0);
            var dungeonOnly = ThreatFormula.Compute(w, 0, 1, 0, 6);

            Assert.AreEqual(baseline + w.WHero * 10, heroOnly);
            Assert.AreEqual(baseline + w.WLoop * 3, loopOnly);
            Assert.AreEqual(baseline + w.WPlaced * 5, placedOnly);
            Assert.AreEqual(baseline + w.WDungeon * 6, dungeonOnly);
        }

        [Test]
        public void ResultFloorsAtOne()
        {
            var w = new ThreatWeights(wHero: 1, wLoop: 1, wPlaced: 1, wDungeon: 1, baseOffset: -1000);
            var result = ThreatFormula.Compute(w, 0, 1, 0, 0);
            Assert.AreEqual(1, result);
        }

        [Test]
        public void ComputeNullWeightsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => ThreatFormula.Compute(null, 1, 1, 1, 1));
        }

        [Test]
        public void HeroPowerOfUsesCombatPowerNotRawSum()
        {
            var scw = StatCombatWeights.Default();
            var strOnly = Stats((StatIds.STR, 50));
            var hpOnly = Stats((StatIds.HP, 50));
            var baseline = Stats();

            var basePower = ThreatFormula.HeroPowerOf(scw, baseline);
            var strPower = ThreatFormula.HeroPowerOf(scw, strOnly);
            var hpPower = ThreatFormula.HeroPowerOf(scw, hpOnly);

            // STR weight for CombatPower = 30 -> 50*30/100=15; HP weight=10 -> 50*10/100=5.
            Assert.AreEqual(15, strPower - basePower);
            Assert.AreEqual(5, hpPower - basePower);
            Assert.Less(hpPower - basePower, strPower - basePower);
        }

        [Test]
        public void HeroPowerOfDrivesThreatBaseHigherWhenStrRaised()
        {
            var scw = StatCombatWeights.Default();
            var w = ThreatWeights.Default();
            var lowStr = Stats((StatIds.STR, 5));
            var highStr = Stats((StatIds.STR, 50));

            var lowPower = ThreatFormula.HeroPowerOf(scw, lowStr);
            var highPower = ThreatFormula.HeroPowerOf(scw, highStr);

            var lowThreat = ThreatFormula.Compute(w, lowPower, 1, 0, 0);
            var highThreat = ThreatFormula.Compute(w, highPower, 1, 0, 0);

            Assert.Greater(highThreat, lowThreat);
        }

        [Test]
        public void HeroPowerOfNullGuards()
        {
            Assert.Throws<ArgumentNullException>(() => ThreatFormula.HeroPowerOf(null, Stats()));
            Assert.Throws<ArgumentNullException>(() => ThreatFormula.HeroPowerOf(StatCombatWeights.Default(), null));
        }
    }
}
