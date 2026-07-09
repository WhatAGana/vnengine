using NUnit.Framework;

namespace VNEngine.Tests
{
    public class GachaRuleTests
    {
        [Test]
        public void FirstPull_IsBaseCost() => Assert.AreEqual(2, GachaRule.GachaCost(0));

        [Test]
        public void CostRisesEveryThreePulls()
        {
            Assert.AreEqual(2, GachaRule.GachaCost(2));  // 2 + 2/3 = 2
            Assert.AreEqual(3, GachaRule.GachaCost(3));  // 2 + 1
            Assert.AreEqual(4, GachaRule.GachaCost(6));  // 2 + 2
        }

        [Test]
        public void RejectsNegativePulls() =>
            Assert.Throws<VnRuntimeException>(() => GachaRule.GachaCost(-1));
    }
}
