using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CaptureRuleTests
    {
        private static CaptureContext Ctx(CaptureTrigger t) => new CaptureContext(t);

        [Test]
        public void TrapAndCapturingMonsterTogetherCapture()
        {
            Assert.IsTrue(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.Trap | CaptureTrigger.CapturingMonster)));
        }

        [Test]
        public void HeroSubdueAloneCaptures()
        {
            Assert.IsTrue(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.HeroSubdue)));
        }

        [Test]
        public void TrapAloneDoesNotCapture()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.Trap)),
                "함정만으로는 포획 안 됨(초회차 안전판: 데미지만)");
        }

        [Test]
        public void CapturingMonsterAloneDoesNotCapture()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.CapturingMonster)),
                "포획몹이라도 함정방이 아니면 포획 안 됨");
        }

        [Test]
        public void NonCapturableNeverCaptured()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(false, Ctx(CaptureTrigger.Trap | CaptureTrigger.CapturingMonster)));
        }

        [Test]
        public void NoTriggerPresentMeansKilled()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.None)));
        }

        [Test]
        public void DisabledTrapDisablesMonsterCaptureButSubdueStillWorks()
        {
            // Trap 비활성 → 함정+포획몹 조합도 포획 불가. HeroSubdue는 활성이라 유효.
            var rule = new CaptureRule(CaptureTrigger.HeroSubdue | CaptureTrigger.CapturingMonster);
            Assert.IsFalse(rule.ShouldCapture(true, Ctx(CaptureTrigger.Trap | CaptureTrigger.CapturingMonster)),
                "Trap 트리거 비활성 → 몹 포획 경로 차단");
            Assert.IsTrue(rule.ShouldCapture(true, Ctx(CaptureTrigger.HeroSubdue)));
        }
    }
}
