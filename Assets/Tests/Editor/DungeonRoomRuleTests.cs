using NUnit.Framework;

namespace VNEngine.Tests
{
    public class DungeonRoomRuleTests
    {
        [Test]
        public void RoomsCapFollowsCurve()
        {
            Assert.AreEqual(5, DungeonRoomRule.RoomsCap(1)); // 3 + 1*2
            Assert.AreEqual(7, DungeonRoomRule.RoomsCap(2)); // 3 + 2*2
            Assert.AreEqual(9, DungeonRoomRule.RoomsCap(3));
        }

        [Test]
        public void CanBuildRoomOnlyBelowCap()
        {
            Assert.IsTrue(DungeonRoomRule.CanBuildRoom(4, 1), "cap5, 현재4 → 건설 가능");
            Assert.IsFalse(DungeonRoomRule.CanBuildRoom(5, 1), "cap5, 현재5 → 천장");
        }

        [Test]
        public void NonPositiveDungeonLevelThrows()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => DungeonRoomRule.RoomsCap(0));
        }
    }
}
