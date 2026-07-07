using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class StatCombatWeightsTests
    {
        private static HeroStats Stats(params (StatId id, int val)[] entries)
        {
            var d = new Dictionary<StatId, int>();
            foreach (var e in entries) d[e.id] = e.val;
            return new HeroStats(d);
        }

        [Test]
        public void HeroCombatProfileGetReturnsFieldPerRole()
        {
            var profile = new HeroCombatProfile(
                physicalAttack: 1,
                magicAttack: 2,
                defense: 3,
                hitRating: 4,
                critRating: 5,
                evasion: 6,
                health: 7,
                skillResource: 8,
                combatPower: 9);

            Assert.AreEqual(1, profile.Get(CombatRole.PhysicalAttack));
            Assert.AreEqual(2, profile.Get(CombatRole.MagicAttack));
            Assert.AreEqual(3, profile.Get(CombatRole.Defense));
            Assert.AreEqual(4, profile.Get(CombatRole.HitRating));
            Assert.AreEqual(5, profile.Get(CombatRole.CritRating));
            Assert.AreEqual(6, profile.Get(CombatRole.Evasion));
            Assert.AreEqual(7, profile.Get(CombatRole.Health));
            Assert.AreEqual(8, profile.Get(CombatRole.SkillResource));
            Assert.AreEqual(9, profile.Get(CombatRole.CombatPower));
        }

        [Test]
        public void HighHpOnlyHeroHasZeroPhysicalAttack()
        {
            // raw합 아님 검증: HP만 큰 hero는 PhysicalAttack에 기여하지 않는다(STR 없음).
            var hero = Stats((StatIds.HP, 500));
            var profile = StatCombatWeights.Default().Derive(hero);
            Assert.AreEqual(0, profile.PhysicalAttack);
        }

        [Test]
        public void OnlyStrChangesPhysicalAttackNotOthers()
        {
            var weights = StatCombatWeights.Default();
            var baseline = Stats((StatIds.STR, 5), (StatIds.HP, 50));
            var boosted = Stats((StatIds.STR, 50), (StatIds.HP, 50));

            var baseProfile = weights.Derive(baseline);
            var boostedProfile = weights.Derive(boosted);

            Assert.Greater(boostedProfile.PhysicalAttack, baseProfile.PhysicalAttack);
            Assert.AreEqual(baseProfile.MagicAttack, boostedProfile.MagicAttack);
            Assert.AreEqual(baseProfile.Health, boostedProfile.Health);
        }

        [Test]
        public void HitRatingCombinesDexAndLukPerData()
        {
            var hero = Stats((StatIds.DEX, 20), (StatIds.LUK, 10));
            var profile = StatCombatWeights.Default().Derive(hero);
            // HitRating: DEX*100/100 + LUK*50/100 = 20 + 5 = 25
            Assert.AreEqual(25, profile.HitRating);
        }

        [Test]
        public void CombatPowerIsWeightedSumOfEightStatsWithAbsentStatsContributingZero()
        {
            var hero = Stats((StatIds.STR, 10), (StatIds.INT, 10), (StatIds.DEX, 10), (StatIds.DEF, 10));
            // AGI/HP/MP/LUK absent -> contribute 0.
            // CombatPower weights: STR30 INT30 DEX20 AGI20 DEF30 HP10 MP10 LUK10
            // = 10*30/100 + 10*30/100 + 10*20/100 + 0 + 10*30/100 + 0 + 0 + 0 = 3+3+2+3 = 11
            var profile = StatCombatWeights.Default().Derive(hero);
            Assert.AreEqual(11, profile.CombatPower);
        }

        [Test]
        public void DeriveDoesNotMutateInputHeroStats()
        {
            var hero = Stats((StatIds.STR, 5));
            var snapshotBefore = new Dictionary<StatId, int>(hero.Values);
            StatCombatWeights.Default().Derive(hero);
            CollectionAssert.AreEquivalent(snapshotBefore, hero.Values);
        }

        [Test]
        public void ConstructorDefensivelyCopiesWeights()
        {
            var weights = new Dictionary<CombatRole, Dictionary<StatId, int>>
            {
                { CombatRole.PhysicalAttack, new Dictionary<StatId, int> { { StatIds.STR, 100 } } },
            };
            var scw = new StatCombatWeights(weights);
            weights[CombatRole.PhysicalAttack][StatIds.STR] = 999;
            weights[CombatRole.MagicAttack] = new Dictionary<StatId, int> { { StatIds.INT, 100 } };

            var hero = Stats((StatIds.STR, 10), (StatIds.INT, 10));
            var profile = scw.Derive(hero);
            Assert.AreEqual(10, profile.PhysicalAttack, "external mutation of source dict must not leak in");
            Assert.AreEqual(0, profile.MagicAttack, "newly added role in source dict must not leak in");
        }

        [Test]
        public void NullWeightsThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new StatCombatWeights(null));
        }
    }
}
