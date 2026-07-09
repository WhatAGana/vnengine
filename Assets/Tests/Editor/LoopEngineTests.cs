using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class LoopEngineTests
    {
        private static ResourceDef Money(int start = 100) => new ResourceDef("money", "재보", start);
        private static ResourceDef Magic(int start = 50) => new ResourceDef("magic", "마력", start);

        private static CommandDef Raid() => new CommandDef("raid", "약탈", new List<ResourceDelta>
        {
            new ResourceDelta("money", 50),
            new ResourceDelta("magic", -20),
        });

        private static LoopEngine Engine() => new LoopEngine(new TurnEngine(
            new List<ResourceDef> { Money(), Magic() },
            new List<CommandDef> { Raid() }));

        [Test]
        public void CreateInitialCampaignStartsAtLoopOneDayOne()
        {
            var c = Engine().CreateInitialCampaign();
            Assert.AreEqual(1, c.Meta.LoopCount);
            Assert.AreEqual(1, c.Run.Day);
            Assert.AreEqual(100, c.Run.Resources["money"]);
            Assert.AreEqual(50, c.Run.Resources["magic"]);
        }

        [Test]
        public void ExecuteCommandAdvancesRunAndLeavesMetaUntouched()
        {
            var engine = Engine();
            var next = engine.ExecuteCommand(engine.CreateInitialCampaign(), "raid");
            Assert.AreEqual(2, next.Run.Day);
            Assert.AreEqual(150, next.Run.Resources["money"]);
            Assert.AreEqual(30, next.Run.Resources["magic"]);
            Assert.AreEqual(1, next.Meta.LoopCount); // Meta 불변
        }

        [Test]
        public void ExecuteCommandDoesNotMutateInput()
        {
            var engine = Engine();
            var initial = engine.CreateInitialCampaign();
            engine.ExecuteCommand(initial, "raid");
            Assert.AreEqual(1, initial.Run.Day);
            Assert.AreEqual(100, initial.Run.Resources["money"]);
            Assert.AreEqual(1, initial.Meta.LoopCount);
        }

        [Test]
        public void StartNewLoopIncrementsLoopAndResetsRun()
        {
            var engine = Engine();
            var c = engine.CreateInitialCampaign();
            c = engine.ExecuteCommand(c, "raid"); // Day=2, money=150
            var looped = engine.StartNewLoop(c);
            Assert.AreEqual(2, looped.Meta.LoopCount);   // +1
            Assert.AreEqual(1, looped.Run.Day);          // 리셋
            Assert.AreEqual(100, looped.Run.Resources["money"]); // 초기값
            Assert.AreEqual(50, looped.Run.Resources["magic"]);
        }

        [Test]
        public void StartNewLoopDoesNotMutateInput()
        {
            var engine = Engine();
            var c = engine.ExecuteCommand(engine.CreateInitialCampaign(), "raid");
            engine.StartNewLoop(c);
            Assert.AreEqual(1, c.Meta.LoopCount); // 입력 불변
            Assert.AreEqual(2, c.Run.Day);
        }

        [Test]
        public void RoundTripKeepsMetaResetsRun()
        {
            var engine = Engine();
            var c = engine.CreateInitialCampaign();
            c = engine.ExecuteCommand(c, "raid");
            c = engine.ExecuteCommand(c, "raid"); // Day=3, money=200
            c = engine.StartNewLoop(c);           // Loop=2, Run 리셋
            c = engine.ExecuteCommand(c, "raid"); // Day=2, money=150
            Assert.AreEqual(2, c.Meta.LoopCount); // 회차는 유지·증가
            Assert.AreEqual(2, c.Run.Day);        // 새 회차 기준
            Assert.AreEqual(150, c.Run.Resources["money"]);
        }

        [Test]
        public void ExecuteCommandUnknownCommandThrows()
        {
            var engine = Engine();
            var c = engine.CreateInitialCampaign();
            Assert.Throws<VnRuntimeException>(() => engine.ExecuteCommand(c, "nope"));
        }

        [Test]
        public void StartNewLoopCarriesKarmaBankForwardAndIncrementsLoopCount()
        {
            var engine = Engine();
            var c = new CampaignState(
                new MetaState(1, HeroStats.Empty, InnState.Empty, 10),
                engine.CreateInitialCampaign().Run);

            var looped = engine.StartNewLoop(c);

            Assert.AreEqual(10, looped.Meta.KarmaBank);
            Assert.AreEqual(2, looped.Meta.LoopCount);
        }
    }
}
