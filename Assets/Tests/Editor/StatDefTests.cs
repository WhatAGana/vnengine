using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class StatDefTests
    {
        [Test]
        public void StatIdValueEqualityAndHashing()
        {
            Assert.AreEqual(new StatId("STR"), new StatId("STR"));
            Assert.IsTrue(new StatId("STR") == StatIds.STR);
            Assert.IsTrue(new StatId("STR") != new StatId("INT"));
            Assert.AreEqual(new StatId("STR").GetHashCode(), new StatId("STR").GetHashCode());
        }

        [Test]
        public void StatIdWorksAsDictionaryKey()
        {
            var d = new Dictionary<StatId, int> { { new StatId("STR"), 7 } };
            Assert.IsTrue(d.ContainsKey(StatIds.STR));
            Assert.AreEqual(7, d[new StatId("STR")]);
        }

        [Test]
        public void StatDefHoldsData()
        {
            var def = new StatDef(StatIds.HP, "HP", 50, 999);
            Assert.AreEqual(StatIds.HP, def.Id);
            Assert.AreEqual("HP", def.DisplayName);
            Assert.AreEqual(50, def.StartValue);
            Assert.AreEqual(999, def.Cap);
        }

        [Test]
        public void DefaultCatalogHasEightStatsWithCap999()
        {
            var defs = StatCatalog.Default();
            Assert.AreEqual(8, defs.Count);
            Assert.IsTrue(defs.All(d => d.Cap == 999));
            var ids = defs.Select(d => d.Id).ToList();
            foreach (var id in new[] { StatIds.STR, StatIds.INT, StatIds.DEX, StatIds.AGI, StatIds.HP, StatIds.MP, StatIds.DEF, StatIds.LUK })
                Assert.Contains(id, ids);
        }

        [Test]
        public void DefaultCatalogStartValuesMatchData()
        {
            var by = StatCatalog.Default().ToDictionary(d => d.Id, d => d.StartValue);
            Assert.AreEqual(5, by[StatIds.STR]);
            Assert.AreEqual(5, by[StatIds.INT]);
            Assert.AreEqual(5, by[StatIds.DEX]);
            Assert.AreEqual(5, by[StatIds.AGI]);
            Assert.AreEqual(50, by[StatIds.HP]);
            Assert.AreEqual(30, by[StatIds.MP]);
            Assert.AreEqual(5, by[StatIds.DEF]);
            Assert.AreEqual(5, by[StatIds.LUK]);
        }
    }
}
