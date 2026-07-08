using NUnit.Framework;

namespace VNEngine.Tests
{
    public class InnIncomeRuleTests
    {
        [Test]
        public void ComputesGuestsGoldKarmaFromInnScale()
        {
            // Staff3,Menu2,Decor5: guests=min(3*2+2,25)=8; isqrt(8)=2; gold=min(2*8+2*3,300)=22; karma=8.
            var inc = InnIncomeRule.Compute(new InnState(3, 5, 2));
            Assert.AreEqual(8, inc.Guests);
            Assert.AreEqual(22, inc.Gold);
            Assert.AreEqual(8, inc.Karma);
        }

        [Test]
        public void KarmaEqualsGuests()
        {
            var inc = InnIncomeRule.Compute(new InnState(5, 5, 1)); // guests=min(11,25)=11
            Assert.AreEqual(inc.Guests, inc.Karma, "인과율이 주 산출 = 손님수 그대로");
            Assert.AreEqual(11, inc.Karma);
        }

        [Test]
        public void GuestsAreCappedAt25()
        {
            var inc = InnIncomeRule.Compute(new InnState(20, 5, 10)); // 40+10 -> cap 25
            Assert.AreEqual(25, inc.Guests);
        }

        [Test]
        public void GoldIsCappedAt300()
        {
            // Staff0,Menu100,Decor5: guests=min(100,25)=25; isqrt(25)=5; gold=min(5*8+100*3,300)=min(340,300)=300.
            var inc = InnIncomeRule.Compute(new InnState(0, 5, 100));
            Assert.AreEqual(25, inc.Guests);
            Assert.AreEqual(300, inc.Gold, "여관 골드 상한 — 던전/메뉴 올려도 폭증 안 함");
        }

        [Test]
        public void GuestsDoNotDependOnAnythingButInnScale()
        {
            // Compute는 dungeonLevel 파라미터가 아예 없음(시그니처 레벨 불변식). 같은 여관 → 항상 같은 손님수.
            var inn = new InnState(4, 5, 4); // guests=min(8+4,25)=12
            Assert.AreEqual(12, InnIncomeRule.Compute(inn).Guests);
            Assert.AreEqual(12, InnIncomeRule.Compute(inn).Guests);
        }

        [Test]
        public void ZeroDecorGatesIncomeToZero()
        {
            var inc = InnIncomeRule.Compute(new InnState(10, 0, 5)); // Decor=0 → 게이트 닫힘
            Assert.AreEqual(0, inc.Guests);
            Assert.AreEqual(0, inc.Gold);
            Assert.AreEqual(0, inc.Karma);
        }

        [Test]
        public void NullInnThrows()
        {
            Assert.Throws<System.ArgumentNullException>(() => InnIncomeRule.Compute(null));
        }

        [Test]
        public void DecayReducesDecorByOneFloorAtZero()
        {
            Assert.AreEqual(4, InnUpkeepRule.Decay(new InnState(3, 5, 2)).Decor);
            Assert.AreEqual(0, InnUpkeepRule.Decay(new InnState(3, 0, 2)).Decor, "0 미만 방지");
        }

        [Test]
        public void DecayPreservesOtherFieldsAndDoesNotMutateInput()
        {
            var inn = new InnState(3, 5, 2);
            var decayed = InnUpkeepRule.Decay(inn);
            Assert.AreEqual(3, decayed.Staff);
            Assert.AreEqual(2, decayed.MenuLevel);
            Assert.AreEqual(5, inn.Decor, "원본 불변");
        }
    }
}
