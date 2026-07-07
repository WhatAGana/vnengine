using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class UnitClassDefTests
    {
        [Test]
        public void UnitClassIdValueEqualityAndHashing()
        {
            Assert.AreEqual(new UnitClassId("Tank"), new UnitClassId("Tank"));
            Assert.IsTrue(new UnitClassId("Tank") == UnitClassIds.Tank);
            Assert.IsTrue(new UnitClassId("Tank") != new UnitClassId("Mage"));
            Assert.AreEqual(new UnitClassId("Tank").GetHashCode(), new UnitClassId("Tank").GetHashCode());
        }

        [Test]
        public void UnitClassIdWorksAsDictionaryKey()
        {
            var d = new Dictionary<UnitClassId, int> { { new UnitClassId("Tank"), 7 } };
            Assert.IsTrue(d.ContainsKey(UnitClassIds.Tank));
            Assert.AreEqual(7, d[new UnitClassId("Tank")]);
        }

        [Test]
        public void UnitClassIdIsNullSafe()
        {
            var idA = new UnitClassId(null);
            var idB = new UnitClassId(null);
            Assert.AreEqual(idA, idB);
            Assert.AreEqual(string.Empty, idA.ToString());
            Assert.AreEqual(0, idA.GetHashCode());
        }

        [Test]
        public void UnitClassDefHoldsData()
        {
            var def = new UnitClassDef(UnitClassIds.Tank, "탱커", 150, 60, 150, false);
            Assert.AreEqual(UnitClassIds.Tank, def.Id);
            Assert.AreEqual("탱커", def.DisplayName);
            Assert.AreEqual(150, def.HpPct);
            Assert.AreEqual(60, def.AtkPct);
            Assert.AreEqual(150, def.DefPct);
            Assert.IsFalse(def.CanBeCaptured);
        }

        [Test]
        public void UnitClassDefThrowsOnEmptyId()
        {
            Assert.Throws<ArgumentException>(() => new UnitClassDef(new UnitClassId(""), "X", 100, 100, 100, false));
            Assert.Throws<ArgumentException>(() => new UnitClassDef(new UnitClassId(null), "X", 100, 100, 100, false));
        }

        [Test]
        public void UnitClassDefThrowsOnNegativePct()
        {
            Assert.Throws<ArgumentException>(() => new UnitClassDef(UnitClassIds.Tank, "X", -1, 100, 100, false));
            Assert.Throws<ArgumentException>(() => new UnitClassDef(UnitClassIds.Tank, "X", 100, -1, 100, false));
            Assert.Throws<ArgumentException>(() => new UnitClassDef(UnitClassIds.Tank, "X", 100, 100, -1, false));
        }

        [Test]
        public void DefaultCatalogHasFiveClasses()
        {
            var defs = UnitClassCatalog.Default();
            Assert.AreEqual(5, defs.Count);
            var ids = defs.Select(d => d.Id).ToList();
            foreach (var id in new[] { UnitClassIds.Tank, UnitClassIds.Mage, UnitClassIds.Paladin, UnitClassIds.Archer, UnitClassIds.Priest })
                Assert.Contains(id, ids);
        }

        [Test]
        public void DefaultCatalogValuesMatchData()
        {
            var by = UnitClassCatalog.Default().ToDictionary(d => d.Id, d => d);

            var tank = by[UnitClassIds.Tank];
            Assert.AreEqual(150, tank.HpPct);
            Assert.AreEqual(60, tank.AtkPct);
            Assert.AreEqual(150, tank.DefPct);
            Assert.IsFalse(tank.CanBeCaptured);

            var mage = by[UnitClassIds.Mage];
            Assert.AreEqual(70, mage.HpPct);
            Assert.AreEqual(150, mage.AtkPct);
            Assert.AreEqual(60, mage.DefPct);
            Assert.IsTrue(mage.CanBeCaptured);

            var paladin = by[UnitClassIds.Paladin];
            Assert.AreEqual(100, paladin.HpPct);
            Assert.AreEqual(100, paladin.AtkPct);
            Assert.AreEqual(100, paladin.DefPct);
            Assert.IsFalse(paladin.CanBeCaptured);

            var archer = by[UnitClassIds.Archer];
            Assert.AreEqual(70, archer.HpPct);
            Assert.AreEqual(100, archer.AtkPct);
            Assert.AreEqual(70, archer.DefPct);
            Assert.IsTrue(archer.CanBeCaptured);

            var priest = by[UnitClassIds.Priest];
            Assert.AreEqual(60, priest.HpPct);
            Assert.AreEqual(60, priest.AtkPct);
            Assert.AreEqual(60, priest.DefPct);
            Assert.IsTrue(priest.CanBeCaptured);
        }
    }
}
