using NUnit.Framework;

namespace VNEngine.Tests
{
    public class StageStateTests
    {
        [Test]
        public void ShowPlacesCharacterInSlot()
        {
            var s = new StageState();
            s.Show("요르", "left");
            Assert.AreEqual("요르", s.Slots["left"]);
        }

        [Test]
        public void ShowSameCharacterNewSlotMoves()
        {
            var s = new StageState();
            s.Show("요르", "left");
            s.Show("요르", "right");
            Assert.IsFalse(s.Slots.ContainsKey("left"));
            Assert.AreEqual("요르", s.Slots["right"]);
        }

        [Test]
        public void ShowDifferentCharacterSameSlotEvicts()
        {
            var s = new StageState();
            s.Show("요르", "center");
            s.Show("민지", "center");
            Assert.AreEqual("민지", s.Slots["center"]);
            Assert.AreEqual(1, s.Slots.Count);
        }

        [Test]
        public void HideRemoves()
        {
            var s = new StageState();
            s.Show("요르", "left");
            s.Hide("요르");
            Assert.AreEqual(0, s.Slots.Count);
        }

        [Test]
        public void SetBackgroundStores()
        {
            var s = new StageState();
            s.SetBackground("공원");
            Assert.AreEqual("공원", s.Background);
        }
    }
}
