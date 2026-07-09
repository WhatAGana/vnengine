using NUnit.Framework;

namespace VNEngine.Tests
{
    public class LootRuleTests
    {
        [Test]
        public void Kill_IsFullLoot()
        {
            // threat 100: 5 + Isqrt(100)*3 = 5 + 10*3 = 35
            Assert.AreEqual(35, LootRule.LootGold(100, false));
        }

        [Test]
        public void Capture_IsHalfLoot_IntegerDiv()
        {
            Assert.AreEqual(35 / 2, LootRule.LootGold(100, true)); // 17
        }

        [Test]
        public void SqrtFlattening_Threat4x_GoldRoughly2x()
        {
            int g100 = LootRule.LootGold(100, false); // 35
            int g400 = LootRule.LootGold(400, false); // 5 + 20*3 = 65
            Assert.AreEqual(35, g100);
            Assert.AreEqual(65, g400);
            Assert.Less(g400, g100 * 2 + 1); // ~2x not 4x (65 < 71)
        }

        [Test]
        public void LootGold_ZeroThreat_KillAndCapture()
        {
            // threatBase=0: 5 + Isqrt(0)*3 = 5 (not floored at 1)
            Assert.AreEqual(5, LootRule.LootGold(0, false));
            Assert.AreEqual(2, LootRule.LootGold(0, true)); // 5/2 integer division
        }

        [Test]
        public void CaptureKarma_IsIsqrtOfThreat()
        {
            Assert.AreEqual(10, LootRule.CaptureKarma(100));
            Assert.AreEqual(20, LootRule.CaptureKarma(400));
        }
    }
}
