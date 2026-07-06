using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class TurnEngineTests
    {
        private static ResourceDef Money(int start = 100) => new ResourceDef("money", "재보", start);
        private static ResourceDef Magic(int start = 50) => new ResourceDef("magic", "마력", start);

        private static CommandDef Raid() => new CommandDef("raid", "약탈", new List<ResourceDelta>
        {
            new ResourceDelta("money", 50),
            new ResourceDelta("magic", -20),
        });

        private static TurnEngine Engine() => new TurnEngine(
            new List<ResourceDef> { Money(), Magic() },
            new List<CommandDef> { Raid() });

        [Test]
        public void CreateInitialStateUsesStartValuesAndDayOne()
        {
            var state = Engine().CreateInitialState();
            Assert.AreEqual(1, state.Day);
            Assert.AreEqual(100, state.Resources["money"]);
            Assert.AreEqual(50, state.Resources["magic"]);
        }

        [Test]
        public void DuplicateResourceIdThrows()
        {
            Assert.Throws<VnRuntimeException>(() => new TurnEngine(
                new List<ResourceDef> { Money(), Money() },
                new List<CommandDef>()));
        }

        [Test]
        public void DuplicateCommandIdThrows()
        {
            Assert.Throws<VnRuntimeException>(() => new TurnEngine(
                new List<ResourceDef> { Money(), Magic() },
                new List<CommandDef> { Raid(), Raid() }));
        }

        [Test]
        public void CommandReferencingUndefinedResourceThrows()
        {
            var bad = new CommandDef("bad", "나쁨", new List<ResourceDelta>
            {
                new ResourceDelta("gold", 10), // "gold" 는 정의 안 됨
            });
            Assert.Throws<VnRuntimeException>(() => new TurnEngine(
                new List<ResourceDef> { Money() },
                new List<CommandDef> { bad }));
        }

        [Test]
        public void ExecuteCommandAppliesDeltasAndAdvancesDay()
        {
            var engine = Engine();
            var next = engine.ExecuteCommand(engine.CreateInitialState(), "raid");
            Assert.AreEqual(2, next.Day);
            Assert.AreEqual(150, next.Resources["money"]); // 100 + 50
            Assert.AreEqual(30, next.Resources["magic"]);  // 50 - 20
        }

        [Test]
        public void ExecuteCommandDoesNotMutateInputState()
        {
            var engine = Engine();
            var initial = engine.CreateInitialState();
            engine.ExecuteCommand(initial, "raid");
            Assert.AreEqual(1, initial.Day);
            Assert.AreEqual(100, initial.Resources["money"]);
            Assert.AreEqual(50, initial.Resources["magic"]);
        }

        [Test]
        public void ExecuteCommandLeavesUntouchedResourcesUnchanged()
        {
            // rest 는 magic 만 건드림 → money 는 그대로여야 한다
            var rest = new CommandDef("rest", "휴식", new List<ResourceDelta>
            {
                new ResourceDelta("magic", 30),
            });
            var engine = new TurnEngine(
                new List<ResourceDef> { Money(100), Magic(50) },
                new List<CommandDef> { rest });
            var next = engine.ExecuteCommand(engine.CreateInitialState(), "rest");
            Assert.AreEqual(100, next.Resources["money"]); // 안 건드린 자원 유지
            Assert.AreEqual(80, next.Resources["magic"]);  // 50 + 30
        }

        [Test]
        public void ExecuteCommandAllowsNegativeValuesNoClamp()
        {
            var drain = new CommandDef("drain", "고갈", new List<ResourceDelta>
            {
                new ResourceDelta("magic", -100),
            });
            var engine = new TurnEngine(
                new List<ResourceDef> { Money(), Magic(50) },
                new List<CommandDef> { drain });
            var next = engine.ExecuteCommand(engine.CreateInitialState(), "drain");
            Assert.AreEqual(-50, next.Resources["magic"]); // 클램프 없음
        }

        [Test]
        public void ExecuteCommandUnknownCommandThrows()
        {
            var engine = Engine();
            var initial = engine.CreateInitialState();
            Assert.Throws<VnRuntimeException>(() => engine.ExecuteCommand(initial, "nope"));
        }
    }
}
