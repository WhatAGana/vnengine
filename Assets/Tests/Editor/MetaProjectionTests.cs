using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MetaProjectionTests
    {
        [Test]
        public void ProjectsLoopCountIntoNamedVariable()
        {
            var state = new GameState(new SeededRandom(1));
            MetaProjection.Project(new MetaState(3), state, "회차");
            Assert.AreEqual(VnValue.Int(3), state.Get("회차"));
        }

        [Test]
        public void ProjectionOverwritesOnRepeat()
        {
            var state = new GameState(new SeededRandom(1));
            MetaProjection.Project(new MetaState(1), state, "loop");
            MetaProjection.Project(new MetaState(2), state, "loop");
            Assert.AreEqual(VnValue.Int(2), state.Get("loop"));
        }

        [Test]
        public void RejectsEmptyVariableName()
        {
            var state = new GameState(new SeededRandom(1));
            Assert.Throws<System.ArgumentException>(() => MetaProjection.Project(new MetaState(1), state, ""));
        }

        [Test]
        public void ProjectsIndividualHeroStatsIntoInjectedVariables()
        {
            var state = new GameState(new SeededRandom(1));
            var heroes = HeroStats.Empty.WithStat(StatIds.STR, 500).WithStat(StatIds.INT, 12);
            var map = new System.Collections.Generic.Dictionary<StatId, string>
            {
                { StatIds.STR, "주인공_STR" },
                { StatIds.INT, "주인공_INT" },
            };
            MetaProjection.ProjectHeroStats(heroes, state, map);
            Assert.AreEqual(VnValue.Int(500), state.Get("주인공_STR"));
            Assert.AreEqual(VnValue.Int(12), state.Get("주인공_INT"));
        }

        [Test]
        public void AbsentStatProjectsZero()
        {
            var state = new GameState(new SeededRandom(1));
            var map = new System.Collections.Generic.Dictionary<StatId, string> { { StatIds.STR, "주인공_STR" } };
            MetaProjection.ProjectHeroStats(HeroStats.Empty, state, map);
            Assert.AreEqual(VnValue.Int(0), state.Get("주인공_STR"));
        }

        [Test]
        public void ProjectsHeroTotalAsSumOfAllStats()
        {
            var state = new GameState(new SeededRandom(1));
            var heroes = HeroStats.Empty.WithStat(StatIds.STR, 100).WithStat(StatIds.DEF, 25);
            MetaProjection.ProjectHeroTotal(heroes, state, "주인공_전투력");
            Assert.AreEqual(VnValue.Int(125), state.Get("주인공_전투력"));
        }

        [Test]
        public void HeroTotalRejectsEmptyVariableName()
        {
            var state = new GameState(new SeededRandom(1));
            Assert.Throws<System.ArgumentException>(() => MetaProjection.ProjectHeroTotal(HeroStats.Empty, state, ""));
        }

        [Test]
        public void ProjectKarmaBank_WritesValue()
        {
            var state = new GameState(new SeededRandom(1));
            var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, 17);
            MetaProjection.ProjectKarmaBank(meta, state, "인과율");
            Assert.AreEqual(VnValue.Int(17), state.Get("인과율"));
        }

        [Test]
        public void ProjectKarmaBank_RejectsEmptyVariableName()
        {
            var state = new GameState(new SeededRandom(1));
            var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, 17);
            Assert.Throws<System.ArgumentException>(() => MetaProjection.ProjectKarmaBank(meta, state, ""));
        }

        [Test]
        public void ProjectKarmaBank_NullArgsThrow()
        {
            var state = new GameState(new SeededRandom(1));
            var meta = new MetaState(1);
            Assert.Throws<System.ArgumentNullException>(() => MetaProjection.ProjectKarmaBank(null, state, "인과율"));
            Assert.Throws<System.ArgumentNullException>(() => MetaProjection.ProjectKarmaBank(meta, null, "인과율"));
        }

        [Test]
        public void ProjectResources_WritesEach_AbsentIsZero()
        {
            var state = new GameState(new SeededRandom(1));
            var resources = new System.Collections.Generic.Dictionary<string, int> { { "gold", 50 } };
            var run = new RunState(1, resources);
            var map = new System.Collections.Generic.Dictionary<string, string>
            {
                { "gold", "varGold" },
                { "manaStone", "varMana" },
            };
            MetaProjection.ProjectResources(run, state, map);
            Assert.AreEqual(VnValue.Int(50), state.Get("varGold"));
            Assert.AreEqual(VnValue.Int(0), state.Get("varMana"));
        }

        [Test]
        public void ProjectResources_NullArgsThrow()
        {
            var state = new GameState(new SeededRandom(1));
            var run = new RunState(1, new System.Collections.Generic.Dictionary<string, int>());
            var map = new System.Collections.Generic.Dictionary<string, string>();
            Assert.Throws<System.ArgumentNullException>(() => MetaProjection.ProjectResources(null, state, map));
            Assert.Throws<System.ArgumentNullException>(() => MetaProjection.ProjectResources(run, null, map));
            Assert.Throws<System.ArgumentNullException>(() => MetaProjection.ProjectResources(run, state, null));
        }

        [Test]
        public void ProjectResources_RejectsEmptyVariableName()
        {
            var state = new GameState(new SeededRandom(1));
            var resources = new System.Collections.Generic.Dictionary<string, int> { { "gold", 50 } };
            var run = new RunState(1, resources);
            var map = new System.Collections.Generic.Dictionary<string, string> { { "gold", "" } };
            Assert.Throws<System.ArgumentException>(() => MetaProjection.ProjectResources(run, state, map));
        }
    }
}
