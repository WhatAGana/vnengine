using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MetaStateHeroesTests
    {
        private static TurnEngine MakeTurnEngine() =>
            new TurnEngine(new List<ResourceDef>(), new List<CommandDef>());

        [Test]
        public void DefaultConstructorGivesEmptyHeroes()
        {
            var meta = new MetaState(1);
            Assert.IsNotNull(meta.Heroes);
            Assert.AreEqual(0, meta.Heroes.Values.Count);
        }

        [Test]
        public void ConstructorStoresHeroes()
        {
            var heroes = HeroStats.Empty.WithStat(StatIds.STR, 42);
            var meta = new MetaState(2, heroes);
            Assert.AreEqual(42, meta.Heroes.Get(StatIds.STR));
            Assert.AreEqual(2, meta.LoopCount);
        }

        [Test]
        public void NullHeroesCoercedToEmpty()
        {
            var meta = new MetaState(1, null);
            Assert.IsNotNull(meta.Heroes);
            Assert.AreEqual(0, meta.Heroes.Values.Count);
        }

        [Test]
        public void StartNewLoopCarriesHeroesForward()
        {
            var engine = new LoopEngine(MakeTurnEngine());
            var heroes = HeroStats.Empty.WithStat(StatIds.STR, 300);
            var campaign = new CampaignState(new MetaState(1, heroes), new RunState(3, new Dictionary<string, int>()));

            var next = engine.StartNewLoop(campaign);

            Assert.AreEqual(2, next.Meta.LoopCount, "회차 증가");
            Assert.AreEqual(300, next.Meta.Heroes.Get(StatIds.STR), "주인공 성장은 회차 넘어 유지(메타)");
        }

        [Test]
        public void StartNewLoopCarriesInnForward()
        {
            var engine = new LoopEngine(MakeTurnEngine());
            var campaign = new CampaignState(new MetaState(1, HeroStats.Empty, new InnState(4, 7, 3)), new RunState(3, new Dictionary<string, int>()));

            var next = engine.StartNewLoop(campaign);

            Assert.AreEqual(2, next.Meta.LoopCount, "회차 증가");
            Assert.AreEqual(4, next.Meta.Inn.Staff, "여관은 메타 — 회차 넘어 유지");
            Assert.AreEqual(7, next.Meta.Inn.Decor, "여관은 메타 — 회차 넘어 유지");
            Assert.AreEqual(3, next.Meta.Inn.MenuLevel, "여관은 메타 — 회차 넘어 유지");
        }
    }
}
