using NUnit.Framework;

namespace VNEngine.Tests
{
    public class RandomTests
    {
        [Test]
        public void SameSeedSameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 50; i++)
                Assert.AreEqual(a.Range(0, 1000), b.Range(0, 1000));
        }

        [Test]
        public void StateRestoresSequence()
        {
            var a = new SeededRandom(7);
            for (int i = 0; i < 5; i++) a.Range(0, 1000); // advance
            uint snap = a.State;
            var expected = new int[10];
            for (int i = 0; i < 10; i++) expected[i] = a.Range(0, 1000);

            var b = new SeededRandom(999);   // different seed
            b.State = snap;                  // but restored state
            for (int i = 0; i < 10; i++)
                Assert.AreEqual(expected[i], b.Range(0, 1000));
        }

        [Test]
        public void RangeBoundsInclusive()
        {
            var r = new SeededRandom(3);
            for (int i = 0; i < 500; i++)
            {
                int v = r.Range(1, 6);
                Assert.IsTrue(v >= 1 && v <= 6);
            }
            Assert.AreEqual(5, r.Range(5, 5));   // single value
            Assert.AreEqual(3, r.Range(3, 1));   // max < min => min
        }
    }
}
