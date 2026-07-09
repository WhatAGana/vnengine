using NUnit.Framework;

namespace VNEngine.Tests
{
    public class SpendPriorityTests
    {
        [Test]
        public void Order_MatchesDoc()
        {
            Assert.AreEqual(5, SpendPriority.Order.Count);
            Assert.AreEqual(SpendCategory.DurabilityRepair, SpendPriority.Order[0]);
            Assert.AreEqual(SpendCategory.LevelUp, SpendPriority.Order[1]);
            Assert.AreEqual(SpendCategory.InnInvest, SpendPriority.Order[2]);
            Assert.AreEqual(SpendCategory.MobUpgrade, SpendPriority.Order[3]);
            Assert.AreEqual(SpendCategory.Gacha, SpendPriority.Order[4]);
        }
    }
}
