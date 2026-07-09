using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MetaStateTests
    {
        [Test]
        public void KarmaBank_DefaultsToZero()
        {
            var m = new MetaState(1);
            Assert.AreEqual(0, m.KarmaBank);
        }

        [Test]
        public void KarmaBank_RoundTripsThroughFullCtor()
        {
            var m = new MetaState(2, HeroStats.Empty, InnState.Empty, 42);
            Assert.AreEqual(42, m.KarmaBank);
        }

        [Test]
        public void DungeonLevel_DefaultsToOne()
        {
            Assert.AreEqual(1, new MetaState(1).DungeonLevel);
            Assert.AreEqual(1, new MetaState(1, HeroStats.Empty).DungeonLevel);
            Assert.AreEqual(1, new MetaState(1, HeroStats.Empty, InnState.Empty).DungeonLevel);
            Assert.AreEqual(1, new MetaState(1, HeroStats.Empty, InnState.Empty, 42).DungeonLevel);
        }

        [Test]
        public void DungeonLevel_RoundTripsThroughFullCtor()
        {
            var m = new MetaState(2, HeroStats.Empty, InnState.Empty, 42, 5);
            Assert.AreEqual(5, m.DungeonLevel);
        }
    }
}
