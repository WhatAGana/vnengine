using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class HeroStatsTests
    {
        [Test]
        public void ConstructorDefensivelyCopiesInput()
        {
            var src = new Dictionary<StatId, int> { { StatIds.STR, 10 } };
            var hs = new HeroStats(src);
            src[StatIds.STR] = 999;                 // 원본 수정
            src[StatIds.INT] = 7;                   // 키 추가
            Assert.AreEqual(10, hs.Get(StatIds.STR), "원본 수정이 새어들면 안 됨");
            Assert.IsFalse(hs.Has(StatIds.INT));
        }

        [Test]
        public void WithStatReturnsNewInstanceAndDoesNotMutateOriginal()
        {
            var hs = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 10 } });
            var hs2 = hs.WithStat(StatIds.STR, 42);
            Assert.AreEqual(10, hs.Get(StatIds.STR), "원본 불변");
            Assert.AreEqual(42, hs2.Get(StatIds.STR));
            Assert.AreNotSame(hs, hs2);
        }

        [Test]
        public void WithStatCanAddPreviouslyAbsentStat()
        {
            var hs = HeroStats.Empty.WithStat(StatIds.LUK, 3);
            Assert.AreEqual(3, hs.Get(StatIds.LUK));
        }

        [Test]
        public void GetThrowsForUnknownStat()
        {
            Assert.Throws<VnRuntimeException>(() => HeroStats.Empty.Get(StatIds.STR));
        }

        [Test]
        public void TryGetReportsPresence()
        {
            var hs = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 5 } });
            Assert.IsTrue(hs.TryGet(StatIds.STR, out var v));
            Assert.AreEqual(5, v);
            Assert.IsFalse(hs.TryGet(StatIds.INT, out _));
        }

        [Test]
        public void FromDefsSeedsStartValues()
        {
            var hs = HeroStats.FromDefs(StatCatalog.Default());
            Assert.AreEqual(5, hs.Get(StatIds.STR));
            Assert.AreEqual(50, hs.Get(StatIds.HP));
            Assert.AreEqual(30, hs.Get(StatIds.MP));
            Assert.AreEqual(8, hs.Values.Count);
        }
    }
}
