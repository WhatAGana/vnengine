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
    }
}
