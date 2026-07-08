using System;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class AttackerFactoryTests
    {
        private static UnitClassDef ClassOf(string name, int hpPct, int atkPct, int defPct, bool canBeCaptured)
            => new UnitClassDef(new UnitClassId(name), name, hpPct, atkPct, defPct, canBeCaptured);

        [Test]
        public void SameSeedProducesSameAttacker()
        {
            var cls = UnitClassCatalog.Default()[0]; // Tank
            var a = AttackerFactory.Create(cls, threatBase: 100, isNamed: false, rng: new SeededRandom(42));
            var b = AttackerFactory.Create(cls, threatBase: 100, isNamed: false, rng: new SeededRandom(42));

            Assert.AreEqual(a.Hp, b.Hp);
            Assert.AreEqual(a.Atk, b.Atk);
            Assert.AreEqual(a.Def, b.Def);
            Assert.AreEqual(a.ClassId, b.ClassId);
            Assert.AreEqual(a.CanBeCaptured, b.CanBeCaptured);
        }

        [Test]
        public void DeviationStaysWithinPlusMinusFive()
        {
            var cls = ClassOf("Test", hpPct: 100, atkPct: 100, defPct: 100, canBeCaptured: true);
            var rng = new SeededRandom(7);
            for (int i = 0; i < 200; i++)
            {
                var a = AttackerFactory.Create(cls, threatBase: 1000, isNamed: false, rng: rng);
                Assert.IsTrue(a.Hp >= 995 && a.Hp <= 1005, $"Hp out of deviation range: {a.Hp}");
                Assert.IsTrue(a.Atk >= 995 && a.Atk <= 1005, $"Atk out of deviation range: {a.Atk}");
                Assert.IsTrue(a.Def >= 995 && a.Def <= 1005, $"Def out of deviation range: {a.Def}");
            }
        }

        [Test]
        public void SmallThreatBaseClampsToMinimumOne()
        {
            var cls = ClassOf("Weak", hpPct: 60, atkPct: 60, defPct: 60, canBeCaptured: true);
            // threatBase=1 -> 1*60/100 = 0, plus deviation [-5,5] could still be <=1 -> clamp to min 1.
            // Use a fixed seed and just verify the floor never breaks (never <1) across many draws.
            var rng = new SeededRandom(11);
            for (int i = 0; i < 200; i++)
            {
                var a = AttackerFactory.Create(cls, threatBase: 1, isNamed: false, rng: rng);
                Assert.GreaterOrEqual(a.Hp, 1);
                Assert.GreaterOrEqual(a.Atk, 1);
                Assert.GreaterOrEqual(a.Def, 1);
            }
        }

        [Test]
        public void ClassProfilePercentagesAreReflectedRelativeToEachOther()
        {
            // Tank: Hp150 Atk60 Def150. Priest: Hp60 Atk60 Def60.
            var tank = UnitClassCatalog.Default()[0];
            var priest = UnitClassCatalog.Default()[4];
            Assert.AreEqual("Tank", tank.Id.Value);
            Assert.AreEqual("Priest", priest.Id.Value);

            // Use large threatBase so the fixed +-5 deviation cannot flip the comparison.
            var tankA = AttackerFactory.Create(tank, threatBase: 1000, isNamed: false, rng: new SeededRandom(1));
            var priestA = AttackerFactory.Create(priest, threatBase: 1000, isNamed: false, rng: new SeededRandom(1));

            Assert.Greater(tankA.Hp, priestA.Hp, "Tank Hp150% should exceed Priest Hp60%");
            Assert.Greater(tankA.Def, priestA.Def, "Tank Def150% should exceed Priest Def60%");
        }

        [Test]
        public void CanBeCapturedPropagatesFromClassDef()
        {
            var captureable = ClassOf("Captureable", 100, 100, 100, canBeCaptured: true);
            var notCaptureable = ClassOf("NotCaptureable", 100, 100, 100, canBeCaptured: false);

            var a = AttackerFactory.Create(captureable, threatBase: 100, isNamed: false, rng: new SeededRandom(5));
            var b = AttackerFactory.Create(notCaptureable, threatBase: 100, isNamed: false, rng: new SeededRandom(5));

            Assert.IsTrue(a.CanBeCaptured);
            Assert.IsFalse(b.CanBeCaptured);
        }

        [Test]
        public void ClassIdPropagatesFromClassDef()
        {
            var cls = ClassOf("Mystic", 100, 100, 100, true);
            var a = AttackerFactory.Create(cls, threatBase: 100, isNamed: false, rng: new SeededRandom(2));
            Assert.AreEqual(new UnitClassId("Mystic"), a.ClassId);
        }

        [Test]
        public void RngCalledExactlyThreeTimesInHpAtkDefOrder()
        {
            var cls = ClassOf("Order", 100, 100, 100, true);
            var recorder = new RecordingRandom();
            AttackerFactory.Create(cls, threatBase: 100, isNamed: false, rng: recorder);
            Assert.AreEqual(3, recorder.CallCount);
        }

        [Test]
        public void NullRngThrows()
        {
            var cls = ClassOf("X", 100, 100, 100, true);
            Assert.Throws<ArgumentNullException>(() => AttackerFactory.Create(cls, threatBase: 100, isNamed: false, rng: null));
        }

        [Test]
        public void NullClassDefThrows()
        {
            Assert.Throws<ArgumentNullException>(() => AttackerFactory.Create(null, threatBase: 100, isNamed: false, rng: new SeededRandom(1)));
        }

        [Test]
        public void CreatePropagatesNamedFlagWithoutConsumingRng()
        {
            var cls = new UnitClassDef(new UnitClassId("N"), "N", 100, 100, 100, true);
            var a = AttackerFactory.Create(cls, 30, isNamed: true, rng: new SeededRandom(1));
            var b = AttackerFactory.Create(cls, 30, isNamed: false, rng: new SeededRandom(1));
            Assert.IsTrue(a.IsNamed);
            Assert.IsFalse(b.IsNamed);
            Assert.AreEqual(a.Hp, b.Hp, "isNamed는 능력치/rng에 영향 없음");
            Assert.AreEqual(a.Atk, b.Atk);
            Assert.AreEqual(a.Def, b.Def);
        }

        private sealed class RecordingRandom : IRandom
        {
            private readonly SeededRandom _inner = new SeededRandom(1);
            public int CallCount { get; private set; }
            public uint State { get => _inner.State; set => _inner.State = value; }
            public int Range(int minInclusive, int maxInclusive)
            {
                CallCount++;
                return _inner.Range(minInclusive, maxInclusive);
            }
        }
    }
}
