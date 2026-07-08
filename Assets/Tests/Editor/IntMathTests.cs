using NUnit.Framework;

namespace VNEngine.Tests
{
    public class IntMathTests
    {
        [Test]
        public void IsqrtOfPerfectSquares()
        {
            Assert.AreEqual(0, IntMath.Isqrt(0));
            Assert.AreEqual(1, IntMath.Isqrt(1));
            Assert.AreEqual(2, IntMath.Isqrt(4));
            Assert.AreEqual(5, IntMath.Isqrt(25));
        }

        [Test]
        public void IsqrtFloorsNonPerfectSquares()
        {
            Assert.AreEqual(1, IntMath.Isqrt(3));
            Assert.AreEqual(4, IntMath.Isqrt(24)); // 4^2=16<=24<25=5^2
            Assert.AreEqual(3, IntMath.Isqrt(15));
        }

        [Test]
        public void IsqrtNegativeThrows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => IntMath.Isqrt(-1));
        }
    }
}
