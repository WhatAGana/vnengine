using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class ClassMatchupTests
    {
        [Test]
        public void RegisteredPairReturnsExactPercent()
        {
            var m = new ClassMatchup(new List<ClassMatchup.Entry>
            {
                new ClassMatchup.Entry { Atk = UnitClassIds.Archer, Def = UnitClassIds.Mage, Percent = 150 },
            });

            Assert.AreEqual(150, m.Multiplier(UnitClassIds.Archer, UnitClassIds.Mage));
        }

        [Test]
        public void UnregisteredPairReturnsNeutral100()
        {
            var m = new ClassMatchup(new List<ClassMatchup.Entry>
            {
                new ClassMatchup.Entry { Atk = UnitClassIds.Archer, Def = UnitClassIds.Mage, Percent = 150 },
            });

            Assert.AreEqual(100, m.Multiplier(UnitClassIds.Tank, UnitClassIds.Priest));
            Assert.AreEqual(100, m.Multiplier(UnitClassIds.Mage, UnitClassIds.Archer)); // reverse direction not registered
        }

        [Test]
        public void EmptyEntriesAlwaysNeutral()
        {
            var m = new ClassMatchup(new List<ClassMatchup.Entry>());
            Assert.AreEqual(100, m.Multiplier(UnitClassIds.Tank, UnitClassIds.Mage));
        }

        [Test]
        public void HeroAttackWithNoClassIsAlwaysNeutral()
        {
            // 주인공(무병종) 공격 id는 등록되지 않으므로 항상 100(중립)이어야 함.
            var m = ClassMatchup.Default();
            var heroId = new UnitClassId("Hero");
            Assert.AreEqual(100, m.Multiplier(heroId, UnitClassIds.Mage));
            Assert.AreEqual(100, m.Multiplier(heroId, UnitClassIds.Tank));
        }

        [Test]
        public void DuplicateEntryThrows()
        {
            Assert.Throws<ArgumentException>(() => new ClassMatchup(new List<ClassMatchup.Entry>
            {
                new ClassMatchup.Entry { Atk = UnitClassIds.Archer, Def = UnitClassIds.Mage, Percent = 150 },
                new ClassMatchup.Entry { Atk = UnitClassIds.Archer, Def = UnitClassIds.Mage, Percent = 120 },
            }));
        }

        [Test]
        public void NegativePercentThrows()
        {
            Assert.Throws<ArgumentException>(() => new ClassMatchup(new List<ClassMatchup.Entry>
            {
                new ClassMatchup.Entry { Atk = UnitClassIds.Archer, Def = UnitClassIds.Mage, Percent = -1 },
            }));
        }

        [Test]
        public void NullEntriesThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new ClassMatchup(null));
        }

        [Test]
        public void DefaultCatalogExampleMatchups()
        {
            var m = ClassMatchup.Default();
            Assert.AreEqual(150, m.Multiplier(UnitClassIds.Archer, UnitClassIds.Mage));
            Assert.AreEqual(70, m.Multiplier(UnitClassIds.Archer, UnitClassIds.Tank));
            Assert.AreEqual(70, m.Multiplier(UnitClassIds.Mage, UnitClassIds.Tank));
            Assert.AreEqual(130, m.Multiplier(UnitClassIds.Paladin, UnitClassIds.Mage));
        }

        [Test]
        public void DefaultCatalogUnregisteredPairIsNeutral()
        {
            var m = ClassMatchup.Default();
            Assert.AreEqual(100, m.Multiplier(UnitClassIds.Priest, UnitClassIds.Tank));
        }
    }
}
