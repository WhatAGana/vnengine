using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class PrisonRuleTests
    {
        [Test]
        public void ReleaseAll_CreditsKarmaAndEmptiesPrison()
        {
            var run = new RunState(1, new Dictionary<string, int>(),
                new[]{ new Captive(new UnitClassId("a"), false, ResetPolicy.Unspecified),
                       new Captive(new UnitClassId("b"), true, ResetPolicy.Unspecified) }, 0);
            var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, 5);
            var res = PrisonRule.ReleaseAll(run, meta);
            Assert.AreEqual(2, res.Released);
            Assert.AreEqual(6, res.KarmaGained);          // 2 * 3
            Assert.AreEqual(11, res.Meta.KarmaBank);      // 5 + 6
            Assert.AreEqual(0, res.Run.Captives.Count);
        }

        [Test]
        public void ReleaseAll_EmptyPrison_NoOp()
        {
            var run = new RunState(1, new Dictionary<string, int>());
            var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, 4);
            var res = PrisonRule.ReleaseAll(run, meta);
            Assert.AreEqual(0, res.Released);
            Assert.AreEqual(4, res.Meta.KarmaBank);
        }

        [Test]
        public void ReleaseAll_DoesNotMutateInputs()
        {
            var run = new RunState(1, new Dictionary<string, int>(),
                new[]{ new Captive(new UnitClassId("a"), false, ResetPolicy.Unspecified) }, 0);
            var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, 5);

            PrisonRule.ReleaseAll(run, meta);

            Assert.AreEqual(1, run.Captives.Count, "원본 run.Captives 불변");
            Assert.AreEqual(5, meta.KarmaBank, "원본 meta.KarmaBank 불변");
        }

        [Test]
        public void ReleaseAll_NullArgs_Throw()
        {
            var run = new RunState(1, new Dictionary<string, int>());
            var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, 0);
            Assert.Throws<System.ArgumentNullException>(() => PrisonRule.ReleaseAll(null, meta));
            Assert.Throws<System.ArgumentNullException>(() => PrisonRule.ReleaseAll(run, null));
        }
    }
}
