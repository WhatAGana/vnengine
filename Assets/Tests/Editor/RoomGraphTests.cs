using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class RoomGraphTests
    {
        private static RoomNode Content(bool hasTrap, params Attacker[] defs)
            => new RoomNode(new List<Attacker>(defs), hasTrap);

        [Test]
        public void RoomIdEqualityByValue()
        {
            Assert.AreEqual(new RoomId("a"), new RoomId("a"));
            Assert.AreNotEqual(new RoomId("a"), new RoomId("b"));
            Assert.IsTrue(new RoomId("a") == new RoomId("a"));
        }

        [Test]
        public void LinearGraphPathMatchesInputOrder()
        {
            var r0 = Content(false, new Attacker(UnitClassIds.Tank, 10, 1, 1, false));
            var r1 = Content(true);
            var graph = RoomGraph.Linear(new List<RoomNode> { r0, r1 });

            Assert.AreEqual(2, graph.Path.Count);
            Assert.IsFalse(graph.Path[0].HasTrap);
            Assert.IsTrue(graph.Path[1].HasTrap);
            Assert.AreEqual(graph.Path[1].Id, graph.CoreFrontRoom.Id, "코어앞1칸 = 경로 마지막 방");
        }

        [Test]
        public void EmptyLinearGraphHasEmptyPathAndNullCoreFront()
        {
            var graph = RoomGraph.Linear(new List<RoomNode>());
            Assert.IsTrue(graph.IsEmpty);
            Assert.AreEqual(0, graph.Path.Count);
            Assert.IsNull(graph.CoreFrontRoom);
        }

        [Test]
        public void BranchingGraphFollowsFirstNextDeterministically()
        {
            // entry -> {a, b}; a -> (terminal), b -> (terminal). NextRooms[0]=a 를 따라감.
            var entry = new RoomNode(new RoomId("entry"), new List<Attacker>(), false,
                new List<RoomId> { new RoomId("a"), new RoomId("b") });
            var a = new RoomNode(new RoomId("a"), new List<Attacker>(), true, new List<RoomId>());
            var b = new RoomNode(new RoomId("b"), new List<Attacker>(), false, new List<RoomId>());
            var graph = new RoomGraph(new List<RoomNode> { entry, a, b }, new RoomId("entry"));

            var path = graph.Path;
            Assert.AreEqual(2, path.Count);
            Assert.AreEqual(new RoomId("entry"), path[0].Id);
            Assert.AreEqual(new RoomId("a"), path[1].Id, "분기에서 NextRooms[0] 추종");
            Assert.IsTrue(path[1].HasTrap);
        }

        [Test]
        public void GraphConstructorRejectsUnknownNextRoom()
        {
            var entry = new RoomNode(new RoomId("entry"), new List<Attacker>(), false,
                new List<RoomId> { new RoomId("missing") });
            Assert.Throws<System.ArgumentException>(
                () => new RoomGraph(new List<RoomNode> { entry }, new RoomId("entry")));
        }
    }
}
