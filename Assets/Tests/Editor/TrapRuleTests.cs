using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class TrapRuleTests
    {
        [Test]
        public void DamageIsBasePlusLevelTimesPerLevel()
        {
            var def = new TrapDef(new TrapId("Spike"), "가시함정", baseDamage: 10, perLevel: 5);
            Assert.AreEqual(15, TrapRule.Damage(def, 1));  // 10 + 1*5
            Assert.AreEqual(20, TrapRule.Damage(def, 2));  // 10 + 2*5
            Assert.AreEqual(10, TrapRule.Damage(def, 0));  // 10 + 0
        }

        [Test]
        public void DamageNullDefThrows()
        {
            Assert.Throws<ArgumentNullException>(() => TrapRule.Damage(null, 1));
        }

        [Test]
        public void DamageNegativeLevelThrows()
        {
            var def = new TrapDef(new TrapId("Spike"), "가시함정", 10, 5);
            Assert.Throws<ArgumentException>(() => TrapRule.Damage(def, -1));
        }

        [Test]
        public void CatalogDefaultHasSpike()
        {
            var cat = TrapCatalog.Default();
            Assert.AreEqual(1, cat.Count);
            Assert.AreEqual(TrapIds.Spike, cat[0].Id);
        }

        [Test]
        public void ConfigDefaultComputesDamageFromRule()
        {
            var cfg = TrapConfig.Default();
            Assert.AreEqual(TrapRule.Damage(cfg.Def, cfg.Level), cfg.Damage);
            Assert.AreEqual(1, cfg.Level);
        }

        [Test]
        public void ConfigNoneHasZeroDamage()
        {
            Assert.AreEqual(0, TrapConfig.None().Damage);
        }

        [Test]
        public void TrapDefRejectsNonPositiveBase()
        {
            Assert.Throws<ArgumentException>(() => new TrapDef(new TrapId("X"), "x", baseDamage: 0, perLevel: 5));
        }
    }
}
