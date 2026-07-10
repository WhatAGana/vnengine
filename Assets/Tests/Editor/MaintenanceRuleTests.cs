using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class MaintenanceRuleTests
    {
        // InnState 생성자 인자 순서는 InnState.cs 확인 후 맞출 것.
        private static InnState Inn(int staff, int decor, int menu) => new InnState(staff, decor, menu);

        private static CampaignState Campaign(InnState inn, int karmaBank, int gold)
        {
            var meta = new MetaState(1, HeroStats.Empty, inn, karmaBank, 1);
            var run = new RunState(5, new Dictionary<string, int> { ["gold"] = gold });
            return new CampaignState(meta, run);
        }

        [Test] public void ApplyInnTick_EarnsIncomeThenDecays()
        {
            var c = Campaign(Inn(3, 1, 2), karmaBank: 10, gold: 100);
            var r = MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.AreEqual(22, r.Gold);
            Assert.AreEqual(8, r.Karma);
            Assert.AreEqual(122, r.Campaign.Run.Resources["gold"]);   // 100 + 22
            Assert.AreEqual(18, r.Campaign.Meta.KarmaBank);           // 10 + 8
            Assert.AreEqual(0, r.Campaign.Meta.Inn.Decor);           // 1 -> Decay -> 0
        }

        [Test] public void ApplyInnTick_GateBeforeDecay_DecorOne_StillEarns()
        {
            // Decor=1: 게이트가 pre-decay(1>0)에서 열려 수급 발생, 그 후 Decay로 0.
            // 만약 Decay가 먼저면 Decor=0에서 게이트 닫혀 수급 0이 됐을 것.
            var c = Campaign(Inn(3, 1, 2), karmaBank: 0, gold: 0);
            var r = MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.Greater(r.Gold, 0);
            Assert.AreEqual(0, r.Campaign.Meta.Inn.Decor);
        }

        [Test] public void ApplyInnTick_DecorZero_NoIncome()
        {
            var c = Campaign(Inn(3, 0, 2), karmaBank: 5, gold: 50);
            var r = MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.AreEqual(0, r.Gold);
            Assert.AreEqual(0, r.Karma);
            Assert.AreEqual(50, r.Campaign.Run.Resources["gold"]);
            Assert.AreEqual(5, r.Campaign.Meta.KarmaBank);
            Assert.AreEqual(0, r.Campaign.Meta.Inn.Decor);
        }

        [Test] public void ApplyInnTick_PreservesDayAndRunFields()
        {
            var c = Campaign(Inn(3, 5, 2), karmaBank: 0, gold: 0);
            var r = MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.AreEqual(5, r.Campaign.Run.Day);
            Assert.AreEqual(1, r.Campaign.Meta.LoopCount);
            Assert.AreEqual(1, r.Campaign.Meta.DungeonLevel);
        }

        [Test] public void ApplyInnTick_DoesNotMutateInput()
        {
            var inn = Inn(3, 5, 2);
            var c = Campaign(inn, karmaBank: 10, gold: 100);
            MaintenanceRule.ApplyInnTick(c, "gold");
            Assert.AreEqual(5, c.Meta.Inn.Decor);       // 원본 Decor 불변
            Assert.AreEqual(10, c.Meta.KarmaBank);      // 원본 bank 불변
            Assert.AreEqual(100, c.Run.Resources["gold"]); // 원본 gold 불변
        }

        [Test] public void ApplyInnTick_NullArgs_Throw()
        {
            var c = Campaign(Inn(3, 1, 2), 0, 0);
            Assert.Throws<System.ArgumentNullException>(() => MaintenanceRule.ApplyInnTick(null, "gold"));
            Assert.Throws<System.ArgumentNullException>(() => MaintenanceRule.ApplyInnTick(c, null));
        }
    }
}
