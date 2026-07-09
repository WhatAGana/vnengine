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
    }
}
