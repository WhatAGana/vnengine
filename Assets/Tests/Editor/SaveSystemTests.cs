using System.Collections.Generic;
using NUnit.Framework;
using VNEngine;
using VNEngine.Unity;

namespace VNEngine.Tests
{
    public class SaveSystemTests
    {
        private const int Slot = 987654; // test-only slot, unlikely to collide

        [TearDown] public void Cleanup() => SaveSystem.Delete(Slot);

        private static SaveData Sample()
        {
            var d = new SaveData
            {
                version = SaveData.SaveFormatVersion,
                programHash = "abc123",
                rngState = -42,
                pc = 7,
                pending = 1,
                lineSpeaker = "요르",
                lineText = "hi 5",
                background = "공원",
            };
            d.vars.Add(new VarEntry { name = "gold", kind = 0, value = 100 });
            d.vars.Add(new VarEntry { name = "met", kind = 1, value = 1 });
            d.callStack.Add(3);
            d.callStack.Add(9);
            d.stage.Add(new StageChar { position = "left", character = "요르" });
            return d;
        }

        [Test]
        public void WriteThenReadRoundTrips()
        {
            SaveSystem.Write(Slot, Sample());
            var r = SaveSystem.Read(Slot);
            Assert.IsNotNull(r);
            Assert.AreEqual(7, r.pc);
            Assert.AreEqual(-42, r.rngState);
            Assert.AreEqual(2, r.vars.Count);
            Assert.AreEqual("gold", r.vars[0].name);
            Assert.AreEqual(new List<int> { 3, 9 }, r.callStack);
            Assert.AreEqual("left", r.stage[0].position);
            Assert.AreEqual("공원", r.background);
        }

        [Test]
        public void ExistsAndDelete()
        {
            Assert.IsFalse(SaveSystem.Exists(Slot));
            SaveSystem.Write(Slot, Sample());
            Assert.IsTrue(SaveSystem.Exists(Slot));
            SaveSystem.Delete(Slot);
            Assert.IsFalse(SaveSystem.Exists(Slot));
        }

        [Test]
        public void ReadMissingSlotIsNull()
            => Assert.IsNull(SaveSystem.Read(Slot));

        [Test]
        public void ListSlotsIncludesWritten()
        {
            SaveSystem.Write(Slot, Sample());
            Assert.Contains(Slot, new List<int>(SaveSystem.ListSlots()));
        }

        [Test]
        public void IsCompatibleChecksVersionAndHash()
        {
            var d = Sample();
            Assert.IsTrue(SaveSystem.IsCompatible(d, "abc123"));
            Assert.IsFalse(SaveSystem.IsCompatible(d, "different"));
            d.version = 999;
            Assert.IsFalse(SaveSystem.IsCompatible(d, "abc123"));
            Assert.IsFalse(SaveSystem.IsCompatible(null, "abc123"));
        }
    }
}
