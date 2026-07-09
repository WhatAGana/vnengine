using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class EconomySpendTests
    {
        private static StatDef StrDef(int cap = 999) => new StatDef(StatIds.STR, "STR", 5, cap);

        [Test]
        public void UpgradeHeroStat_SpendsKarmaAndRaisesStat()
        {
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 5 } });
            var meta = new MetaState(1, stats, InnState.Empty, 100);

            var result = EconomySpend.UpgradeHeroStat(meta, StrDef(), StatCostCurve.Default());

            var expected = StatUpgrade.Upgrade(stats, StrDef(), StatCostCurve.Default(), 100);
            Assert.Greater(expected.KarmaSpent, 0);
            Assert.AreEqual(expected.Stats.Get(StatIds.STR), result.Heroes.Get(StatIds.STR));
            Assert.AreEqual(100 - expected.KarmaSpent, result.KarmaBank);
            Assert.GreaterOrEqual(result.KarmaBank, 0, "KarmaBank never negative");
        }

        [Test]
        public void UpgradeHeroStat_ZeroBank_NoChange()
        {
            var stats = new HeroStats(new Dictionary<StatId, int> { { StatIds.STR, 5 } });
            var meta = new MetaState(1, stats, InnState.Empty, 0);

            var result = EconomySpend.UpgradeHeroStat(meta, StrDef(), StatCostCurve.Default());

            Assert.AreEqual(5, result.Heroes.Get(StatIds.STR));
            Assert.AreEqual(0, result.KarmaBank);
        }

        [Test]
        public void UpgradeHeroStat_PreservesLoopCountInnAndDungeonLevel()
        {
            var meta = new MetaState(3, HeroStats.Empty, new InnState(1, 2, 0), 10, 4);

            var result = EconomySpend.UpgradeHeroStat(meta, StrDef(), StatCostCurve.Default());

            Assert.AreEqual(3, result.LoopCount);
            Assert.AreEqual(2, result.Inn.Decor);
            Assert.AreEqual(4, result.DungeonLevel);
        }

        private static CampaignState CampaignWithGoldAndKarma(int gold, int karma, int dungeonLevel = 1)
        {
            var meta = new MetaState(1, HeroStats.Empty, InnState.Empty, karma, dungeonLevel);
            var run = new RunState(1, new Dictionary<string, int> { { "gold", gold } });
            return new CampaignState(meta, run);
        }

        [Test]
        public void LevelUpDungeon_EnoughGoldAndKarma_ChargesBothAndIncrementsLevel()
        {
            var campaign = CampaignWithGoldAndKarma(gold: 2000, karma: 50, dungeonLevel: 1);
            int goldCost = DungeonLevelRule.LevelUpCost(1);

            var result = EconomySpend.LevelUpDungeon(campaign, "gold", karmaCost: 10);

            Assert.IsTrue(result.Leveled);
            Assert.AreEqual(2000 - goldCost, result.Campaign.Run.Resources["gold"]);
            Assert.AreEqual(50 - 10, result.Campaign.Meta.KarmaBank);
            Assert.AreEqual(2, result.Campaign.Meta.DungeonLevel);
        }

        [Test]
        public void LevelUpDungeon_InsufficientGold_NoOp()
        {
            int goldCost = DungeonLevelRule.LevelUpCost(1);
            var campaign = CampaignWithGoldAndKarma(gold: goldCost - 1, karma: 50, dungeonLevel: 1);

            var result = EconomySpend.LevelUpDungeon(campaign, "gold", karmaCost: 10);

            Assert.IsFalse(result.Leveled);
            Assert.AreEqual(goldCost - 1, result.Campaign.Run.Resources["gold"]);
            Assert.AreEqual(50, result.Campaign.Meta.KarmaBank);
            Assert.AreEqual(1, result.Campaign.Meta.DungeonLevel);
        }

        [Test]
        public void LevelUpDungeon_InsufficientKarma_NoOp()
        {
            var campaign = CampaignWithGoldAndKarma(gold: 2000, karma: 5, dungeonLevel: 1);

            var result = EconomySpend.LevelUpDungeon(campaign, "gold", karmaCost: 10);

            Assert.IsFalse(result.Leveled);
            Assert.AreEqual(2000, result.Campaign.Run.Resources["gold"]);
            Assert.AreEqual(5, result.Campaign.Meta.KarmaBank);
            Assert.AreEqual(1, result.Campaign.Meta.DungeonLevel);
        }

        [Test]
        public void LevelUpDungeon_PreservesOtherRunAndMetaFields()
        {
            var meta = new MetaState(7, HeroStats.Empty, new InnState(1, 2, 0), 50, 1);
            var captives = new List<Captive> { new Captive(new UnitClassId("Imp"), false, ResetPolicy.Unspecified) };
            var run = new RunState(3, new Dictionary<string, int> { { "gold", 2000 } }, captives, pullsThisLoop: 4);
            var campaign = new CampaignState(meta, run);

            var result = EconomySpend.LevelUpDungeon(campaign, "gold", karmaCost: 10);

            Assert.IsTrue(result.Leveled);
            Assert.AreEqual(3, result.Campaign.Run.Day);
            Assert.AreEqual(1, result.Campaign.Run.Captives.Count);
            Assert.AreEqual(4, result.Campaign.Run.PullsThisLoop);
            Assert.AreEqual(7, result.Campaign.Meta.LoopCount);
            Assert.AreEqual(2, result.Campaign.Meta.Inn.Decor);
        }

        [Test]
        public void UpgradeHeroStat_NullArgsThrow()
        {
            var meta = new MetaState(1);
            Assert.Throws<System.ArgumentNullException>(() => EconomySpend.UpgradeHeroStat(null, StrDef(), StatCostCurve.Default()));
            Assert.Throws<System.ArgumentNullException>(() => EconomySpend.UpgradeHeroStat(meta, null, StatCostCurve.Default()));
            Assert.Throws<System.ArgumentNullException>(() => EconomySpend.UpgradeHeroStat(meta, StrDef(), null));
        }

        [Test]
        public void LevelUpDungeon_NullArgsThrow()
        {
            var campaign = CampaignWithGoldAndKarma(2000, 50);
            Assert.Throws<System.ArgumentNullException>(() => EconomySpend.LevelUpDungeon(null, "gold", 10));
            Assert.Throws<System.ArgumentNullException>(() => EconomySpend.LevelUpDungeon(campaign, null, 10));
        }

        private static RunState RunWithMana(int mana, int pullsThisLoop = 0) =>
            new RunState(1, new Dictionary<string, int> { { "mana", mana } }, new List<Captive>(), pullsThisLoop);

        [Test]
        public void GachaPull_EnoughMana_DeductsAndIncrementsCounter()
        {
            var run = RunWithMana(mana: 10, pullsThisLoop: 0);

            var result = EconomySpend.GachaPull(run, "mana");

            Assert.IsTrue(result.Pulled);
            Assert.AreEqual(2, result.Cost);
            Assert.AreEqual(8, result.Run.Resources["mana"]);
            Assert.AreEqual(1, result.Run.PullsThisLoop);
        }

        [Test]
        public void GachaPull_CostRisesWithPulls()
        {
            var run = RunWithMana(mana: 10, pullsThisLoop: 3);

            var result = EconomySpend.GachaPull(run, "mana");

            Assert.AreEqual(3, result.Cost);
            Assert.IsTrue(result.Pulled);
            Assert.AreEqual(7, result.Run.Resources["mana"]);
            Assert.AreEqual(4, result.Run.PullsThisLoop);
        }

        [Test]
        public void GachaPull_InsufficientMana_NoOp()
        {
            var run = RunWithMana(mana: 1, pullsThisLoop: 0);

            var result = EconomySpend.GachaPull(run, "mana");

            Assert.IsFalse(result.Pulled);
            Assert.AreEqual(2, result.Cost);
            Assert.AreEqual(1, result.Run.Resources["mana"]);
            Assert.AreEqual(0, result.Run.PullsThisLoop);
        }

        [Test]
        public void GachaPull_NullArgsThrow()
        {
            var run = RunWithMana(10);
            Assert.Throws<System.ArgumentNullException>(() => EconomySpend.GachaPull(null, "mana"));
            Assert.Throws<System.ArgumentNullException>(() => EconomySpend.GachaPull(run, null));
        }
    }
}
