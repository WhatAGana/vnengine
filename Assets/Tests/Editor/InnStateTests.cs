using NUnit.Framework;

namespace VNEngine.Tests
{
    public class InnStateTests
    {
        [Test]
        public void ConstructsAndExposesFields()
        {
            var inn = new InnState(3, 5, 2);
            Assert.AreEqual(3, inn.Staff);
            Assert.AreEqual(5, inn.Decor);
            Assert.AreEqual(2, inn.MenuLevel);
        }

        [Test]
        public void EmptyIsAllZero()
        {
            Assert.AreEqual(0, InnState.Empty.Staff);
            Assert.AreEqual(0, InnState.Empty.Decor);
            Assert.AreEqual(0, InnState.Empty.MenuLevel);
        }

        [Test]
        public void WithDecorReturnsNewInstanceLeavingOriginalUnchanged()
        {
            var inn = new InnState(3, 5, 2);
            var repaired = inn.WithDecor(10);
            Assert.AreEqual(10, repaired.Decor);
            Assert.AreEqual(5, inn.Decor, "원본 불변");
            Assert.AreEqual(3, repaired.Staff, "다른 필드 보존");
            Assert.AreEqual(2, repaired.MenuLevel);
        }

        [Test]
        public void NegativeFieldsThrow()
        {
            Assert.Throws<System.ArgumentException>(() => new InnState(-1, 0, 0));
            Assert.Throws<System.ArgumentException>(() => new InnState(0, -1, 0));
            Assert.Throws<System.ArgumentException>(() => new InnState(0, 0, -1));
        }
    }
}
