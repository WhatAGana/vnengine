using System;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class DamageFormulaTests
    {
        private sealed class RecordingRandom : IRandom
        {
            private readonly SeededRandom _inner;
            public int CallCount { get; private set; }
            public RecordingRandom(int seed) { _inner = new SeededRandom(seed); }
            public uint State { get => _inner.State; set => _inner.State = value; }
            public int Range(int minInclusive, int maxInclusive)
            {
                CallCount++;
                return _inner.Range(minInclusive, maxInclusive);
            }
        }

        // ---- Raw ----

        [Test]
        public void RawAtkGreaterThanDefUsesLinearBranch()
        {
            // atk*2 - def
            Assert.AreEqual(30, DamageFormula.Raw(atk: 20, def: 10));
        }

        [Test]
        public void RawAtkEqualsDefYieldsAtkAndIsPositive()
        {
            var raw = DamageFormula.Raw(atk: 15, def: 15);
            Assert.AreEqual(15, raw);
            Assert.Greater(raw, 0);
        }

        [Test]
        public void RawAtkLessThanDefUsesSquareOverDefBranchWithIntegerDivision()
        {
            // atk*atk/def = 36/10 = 3 (truncated)
            Assert.AreEqual(3, DamageFormula.Raw(atk: 6, def: 10));
        }

        [Test]
        public void RawNonPositiveDefThrows()
        {
            Assert.Throws<ArgumentException>(() => DamageFormula.Raw(10, 0));
            Assert.Throws<ArgumentException>(() => DamageFormula.Raw(10, -1));
        }

        // ---- Apply ----

        [Test]
        public void ApplyFloorsAtOneWhenScaledRawWouldBeZero()
        {
            // Raw(3,100) = 9/100 = 0 -> Apply must floor to 1.
            var dmg = DamageFormula.Apply(atk: 3, def: 100, matchupPct: 100);
            Assert.AreEqual(1, dmg);
        }

        [Test]
        public void ApplyMatchup150TruncatesCorrectly()
        {
            // Raw(4,1) = 2*4-1 = 7. 7*150/100 = 1050/100 = 10 (truncated from 10.5).
            var dmg = DamageFormula.Apply(atk: 4, def: 1, matchupPct: 150);
            Assert.AreEqual(10, dmg);
        }

        [Test]
        public void ApplyMatchup70TruncatesCorrectly()
        {
            // Raw(4,1) = 7. 7*70/100 = 490/100 = 4 (truncated from 4.9).
            var dmg = DamageFormula.Apply(atk: 4, def: 1, matchupPct: 70);
            Assert.AreEqual(4, dmg);
        }

        [Test]
        public void ApplyNeutralMatchupExactMultiplication()
        {
            // Raw(20,10) = 30. 30*100/100 = 30.
            var dmg = DamageFormula.Apply(atk: 20, def: 10, matchupPct: 100);
            Assert.AreEqual(30, dmg);
        }

        // ---- Resolve ----

        [Test]
        public void ResolveIsDeterministicForSameSeed()
        {
            var hp = new HitParams { HitRating = 50, Evasion = 10, CritRating = 20, CritMultiplierPct = 200, HitRollMax = 100 };
            var a = DamageFormula.Resolve(20, 10, 100, hp, new SeededRandom(99));
            var b = DamageFormula.Resolve(20, 10, 100, hp, new SeededRandom(99));
            Assert.AreEqual(a, b);
        }

        [Test]
        public void ResolveGuaranteedMissReturnsZero()
        {
            // hitRating - evasion <= 0 with roll in [1, hitRollMax] -> roll always > threshold -> always miss.
            var hp = new HitParams { HitRating = 0, Evasion = 10, CritRating = 100, CritMultiplierPct = 200, HitRollMax = 100 };
            for (int seed = 1; seed <= 20; seed++)
            {
                var dmg = DamageFormula.Resolve(20, 10, 100, hp, new SeededRandom(seed));
                Assert.AreEqual(0, dmg, $"seed {seed} should always miss when evasion far exceeds hitRating");
            }
        }

        [Test]
        public void ResolveGuaranteedHitNoCritEqualsApply()
        {
            // hitRating - evasion >= hitRollMax -> roll <= threshold always -> always hit.
            var hp = new HitParams { HitRating = 1000, Evasion = 0, CritRating = 0, CritMultiplierPct = 200, HitRollMax = 100 };
            var expected = DamageFormula.Apply(20, 10, 100);
            for (int seed = 1; seed <= 20; seed++)
            {
                var dmg = DamageFormula.Resolve(20, 10, 100, hp, new SeededRandom(seed));
                Assert.AreEqual(expected, dmg, $"seed {seed} should always hit with no crit and equal Apply()");
            }
        }

        [Test]
        public void ResolveGuaranteedHitAndCritAppliesCritMultiplier()
        {
            var hp = new HitParams { HitRating = 1000, Evasion = 0, CritRating = 1000, CritMultiplierPct = 200, HitRollMax = 100 };
            var baseDmg = DamageFormula.Apply(20, 10, 100);
            var expected = baseDmg * 200 / 100;
            var dmg = DamageFormula.Resolve(20, 10, 100, hp, new SeededRandom(3));
            Assert.AreEqual(expected, dmg);
            Assert.Greater(dmg, baseDmg);
        }

        [Test]
        public void ResolveMissConsumesOnlyOneRngCall()
        {
            var hp = new HitParams { HitRating = 0, Evasion = 10, CritRating = 100, CritMultiplierPct = 200, HitRollMax = 100 };
            var recorder = new RecordingRandom(1);
            DamageFormula.Resolve(20, 10, 100, hp, recorder);
            Assert.AreEqual(1, recorder.CallCount, "miss should stop after the hit roll (no crit roll)");
        }

        [Test]
        public void ResolveHitConsumesTwoRngCallsInHitThenCritOrder()
        {
            var hp = new HitParams { HitRating = 1000, Evasion = 0, CritRating = 0, CritMultiplierPct = 200, HitRollMax = 100 };
            var recorder = new RecordingRandom(1);
            DamageFormula.Resolve(20, 10, 100, hp, recorder);
            Assert.AreEqual(2, recorder.CallCount, "hit should consume hit-roll then crit-roll, in that order");
        }

        [Test]
        public void ResolveNullRngThrows()
        {
            var hp = new HitParams { HitRating = 50, Evasion = 10, CritRating = 20, CritMultiplierPct = 200, HitRollMax = 100 };
            Assert.Throws<ArgumentNullException>(() => DamageFormula.Resolve(20, 10, 100, hp, null));
        }
    }
}
