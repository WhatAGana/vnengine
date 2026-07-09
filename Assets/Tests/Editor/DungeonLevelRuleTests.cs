using NUnit.Framework;

namespace VNEngine.Tests
{
    public class DungeonLevelRuleTests
    {
        [Test]
        public void LevelUpCost_MatchesTable()
        {
            Assert.AreEqual(120, DungeonLevelRule.LevelUpCost(1));
            Assert.AreEqual(299, DungeonLevelRule.LevelUpCost(2));
            Assert.AreEqual(511, DungeonLevelRule.LevelUpCost(3));
            Assert.AreEqual(1004, DungeonLevelRule.LevelUpCost(5));
            Assert.AreEqual(2507, DungeonLevelRule.LevelUpCost(10));
        }

        [Test]
        public void LevelUpCost_Monotonic()
        {
            for (int dl = 1; dl < 20; dl++)
                Assert.Less(DungeonLevelRule.LevelUpCost(dl), DungeonLevelRule.LevelUpCost(dl + 1));
        }

        [Test]
        public void LevelUpCost_ClampsAboveTable()
        {
            Assert.AreEqual(DungeonLevelRule.LevelUpCost(20), DungeonLevelRule.LevelUpCost(25));
        }

        [Test]
        public void LevelUpCost_RejectsNonPositive()
        {
            Assert.Throws<VnRuntimeException>(() => DungeonLevelRule.LevelUpCost(0));
        }
    }
}
