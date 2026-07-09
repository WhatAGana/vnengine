using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CaptiveLedgerTests
    {
        private static RunState EmptyRun() => new RunState(1, new Dictionary<string, int>());

        [Test]
        public void AccumulateSplitsNamedAndMobIntoCaptives()
        {
            var named = new Attacker(new UnitClassId("Martha"), 10, 10, 10, canBeCaptured: true, isCapturingMonster: false, isNamed: true);
            var mob = new Attacker(new UnitClassId("Grunt"), 10, 10, 10, canBeCaptured: true, isCapturingMonster: false, isNamed: false);
            var result = new CombatResult(false, new List<Attacker>(), new List<Attacker> { named, mob });

            var run2 = CaptiveLedger.Accumulate(EmptyRun(), result);

            Assert.AreEqual(2, run2.Captives.Count);
            var byName = new Dictionary<string, bool>();
            foreach (var c in run2.Captives) byName[c.ClassId.Value] = c.IsNamed;
            Assert.IsTrue(byName["Martha"], "네임드 플래그 보존");
            Assert.IsFalse(byName["Grunt"], "잡졸은 IsNamed=false");
        }

        [Test]
        public void AccumulateAppendsToExistingCaptives()
        {
            var first = CaptiveLedger.Accumulate(EmptyRun(),
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("A"), 1, 1, 1, true) }));
            var second = CaptiveLedger.Accumulate(first,
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("B"), 1, 1, 1, true) }));
            Assert.AreEqual(2, second.Captives.Count);
        }

        [Test]
        public void AccumulateDoesNotMutateInputRun()
        {
            var run = EmptyRun();
            CaptiveLedger.Accumulate(run,
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("A"), 1, 1, 1, true) }));
            Assert.AreEqual(0, run.Captives.Count, "원본 RunState 불변");
        }

        [Test]
        public void CaptiveDefaultResetPolicyIsUnspecified()
        {
            var run = CaptiveLedger.Accumulate(EmptyRun(),
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("A"), 1, 1, 1, true, false, true) }));
            Assert.AreEqual(ResetPolicy.Unspecified, run.Captives[0].ResetPolicy, "리셋정책은 플래그 자리만(미결)");
        }

        [Test]
        public void AccumulatePreservesPullsThisLoop()
        {
            var run = new RunState(1, new Dictionary<string, int>(), new List<Captive>(), pullsThisLoop: 4);
            var result = CaptiveLedger.Accumulate(run,
                new CombatResult(false, new List<Attacker>(), new List<Attacker> { new Attacker(new UnitClassId("A"), 1, 1, 1, true) }));

            Assert.AreEqual(4, result.PullsThisLoop, "Accumulate가 PullsThisLoop를 리셋하면 안 된다(가챠 카운터 회귀 방지)");
        }
    }
}
