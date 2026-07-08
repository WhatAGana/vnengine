using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CaptureRuleTests
    {
        private static CaptureContext Ctx(CaptureTrigger t) => new CaptureContext(t);

        [Test]
        public void CapturableWithAnyEnabledTriggerIsCaptured()
        {
            var rule = CaptureRule.Default();
            Assert.IsTrue(rule.ShouldCapture(true, Ctx(CaptureTrigger.Trap)));
            Assert.IsTrue(rule.ShouldCapture(true, Ctx(CaptureTrigger.HeroSubdue)));
            Assert.IsTrue(rule.ShouldCapture(true, Ctx(CaptureTrigger.CapturingMonster)));
        }

        [Test]
        public void NonCapturableNeverCaptured()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(false, Ctx(CaptureTrigger.Trap)));
        }

        [Test]
        public void NoTriggerPresentMeansKilled()
        {
            Assert.IsFalse(CaptureRule.Default().ShouldCapture(true, Ctx(CaptureTrigger.None)));
        }

        [Test]
        public void DisabledTriggerDoesNotCapture()
        {
            var trapOnly = new CaptureRule(CaptureTrigger.Trap);
            Assert.IsFalse(trapOnly.ShouldCapture(true, Ctx(CaptureTrigger.HeroSubdue)), "HeroSubdue 비활성 → 포획 안 됨");
            Assert.IsTrue(trapOnly.ShouldCapture(true, Ctx(CaptureTrigger.Trap)));
        }
    }
}
