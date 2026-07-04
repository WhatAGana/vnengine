using NUnit.Framework;

namespace VNEngine.Tests
{
    public class GameStateTests
    {
        private GameState New() => new GameState(new SeededRandom(1));

        [Test] public void UndefinedVarIsIntZero()
        {
            var s = New();
            Assert.AreEqual(VnValue.Int(0), s.Get("gold"));
            Assert.IsFalse(s.Has("gold"));
        }

        [Test] public void SetThenGet()
        {
            var s = New();
            s.Set("gold", VnValue.Int(50));
            Assert.AreEqual(VnValue.Int(50), s.Get("gold"));
            Assert.IsTrue(s.Has("gold"));
        }

        [Test] public void SetOverwrites()
        {
            var s = New();
            s.Set("x", VnValue.Int(1));
            s.Set("x", VnValue.Bool(true));
            Assert.AreEqual(VnValue.Bool(true), s.Get("x"));
        }

        [Test] public void SeededRandomIsDeterministic()
        {
            var a = new SeededRandom(123);
            var b = new SeededRandom(123);
            for (int i = 0; i < 20; i++)
                Assert.AreEqual(a.Range(1, 6), b.Range(1, 6));
        }

        [Test] public void RandomInRangeInclusive()
        {
            var r = new SeededRandom(7);
            for (int i = 0; i < 1000; i++)
            {
                int v = r.Range(1, 3);
                Assert.IsTrue(v >= 1 && v <= 3, $"out of range: {v}");
            }
        }

        [Test] public void RandomDegenerateRangeReturnsLow()
        {
            var r = new SeededRandom(7);
            Assert.AreEqual(5, r.Range(5, 5));
            Assert.AreEqual(9, r.Range(9, 2)); // hi < lo → lo
        }
    }
}
