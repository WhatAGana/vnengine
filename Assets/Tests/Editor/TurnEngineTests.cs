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
        public void CreateInitialStateUsesStartValuesAndWeekOne()
        {
            var state = Engine().CreateInitialState();
            Assert.AreEqual(1, state.Week);
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
    }
}
