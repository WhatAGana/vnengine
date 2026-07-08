using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MonsterCatalogTests
    {
        private static Dictionary<UnitClassId, MonsterDef> ById()
        {
            var d = new Dictionary<UnitClassId, MonsterDef>();
            foreach (var m in MonsterCatalog.Default()) d[m.Id] = m;
            return d;
        }

        [Test]
        public void DefaultCatalogHasSevenMonstersWithSpecifiedCosts()
        {
            var by = ById();
            Assert.AreEqual(7, by.Count);
            Assert.AreEqual(1, by[MonsterIds.Imp].Cost);
            Assert.AreEqual(1, by[MonsterIds.Goblin].Cost);
            Assert.AreEqual(2, by[MonsterIds.Orc].Cost);
            Assert.AreEqual(3, by[MonsterIds.HighOrc].Cost);
            Assert.AreEqual(3, by[MonsterIds.Succubus].Cost);
            Assert.AreEqual(4, by[MonsterIds.DeathKnight].Cost);
            Assert.AreEqual(5, by[MonsterIds.HighDemon].Cost);
        }

        [Test]
        public void SuccubusIsCapturingMonsterOthersAreNot()
        {
            var by = ById();
            Assert.IsTrue(by[MonsterIds.Succubus].IsCapturingMonster);
            Assert.IsFalse(by[MonsterIds.Imp].IsCapturingMonster);
            Assert.IsFalse(by[MonsterIds.DeathKnight].IsCapturingMonster);
        }

        [Test]
        public void MonsterDefRejectsNonPositiveCost()
        {
            Assert.Throws<System.ArgumentException>(
                () => new MonsterDef(new UnitClassId("X"), "X", 0, 10, 10, 10, false));
        }

        [Test]
        public void AttackerCarriesCapturingMonsterFlagWithBackCompatDefault()
        {
            var plain = new Attacker(MonsterIds.Imp, 10, 5, 5, false);
            Assert.IsFalse(plain.IsCapturingMonster, "5-arg 편의 생성자는 false 기본");
            var cap = new Attacker(MonsterIds.Succubus, 10, 5, 5, false, isCapturingMonster: true);
            Assert.IsTrue(cap.IsCapturingMonster);
        }
    }
}
