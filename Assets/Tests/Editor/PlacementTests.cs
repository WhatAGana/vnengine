using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class PlacementTests
    {
        // 방 3개 선형 그래프(r0,r1,r2). CoreFront = r2.
        private static RoomGraph ThreeRooms()
            => RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false),
            });

        private static IReadOnlyList<MonsterDef> Cat() => MonsterCatalog.Default();

        private static MonsterPlacement Place(string room, UnitClassId m)
            => new MonsterPlacement { Room = new RoomId(room), Monster = m };

        private static PlacementPlan Plan(params MonsterPlacement[] ms)
            => new PlacementPlan { Monsters = new List<MonsterPlacement>(ms), HasHero = false };

        // r0=함정방, r1·r2=일반방. (포획몹은 r0에만, 일반몹은 r1/r2에만 배치 가능.)
        private static RoomGraph TrapAtR0()
            => RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), hasTrap: true),
                new RoomNode(new List<Attacker>(), hasTrap: false),
                new RoomNode(new List<Attacker>(), hasTrap: false),
            });

        [Test]
        public void BudgetIsRoomCountTimesThree()
        {
            var r = PlacementValidator.Validate(Plan(), ThreeRooms(), Cat());
            Assert.AreEqual(9, r.Budget);          // 3방 × 3
            Assert.IsTrue(r.IsValid);
        }

        [Test]
        public void MoreRoomsIncreasesBudget()
        {
            var four = RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), false), new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false), new RoomNode(new List<Attacker>(), false),
            });
            Assert.AreEqual(12, PlacementValidator.Validate(Plan(), four, Cat()).Budget);
        }

        [Test]
        public void CostSumWithinBudgetIsValid()
        {
            // 하이오크(3, 비포획)+데스나이트(4)+오크(2)=9 == 예산9.
            // (ThreeRooms()는 전부 일반방이라 포획몹인 서큐버스는 배치 불가 — 새 함정방 규칙. cost3 비포획몹으로 대체.)
            var plan = Plan(Place("r0", MonsterIds.HighOrc), Place("r1", MonsterIds.DeathKnight), Place("r2", MonsterIds.Orc));
            var r = PlacementValidator.Validate(plan, ThreeRooms(), Cat());
            Assert.IsTrue(r.IsValid);
            Assert.AreEqual(9, r.TotalCost);
            Assert.AreEqual(PlacementError.None, r.Error);
        }

        [Test]
        public void CostSumOverBudgetIsRejected()
        {
            // 고위마족(5)+데스나이트(4)+오크(2)=11 > 9.
            var plan = Plan(Place("r0", MonsterIds.HighDemon), Place("r1", MonsterIds.DeathKnight), Place("r2", MonsterIds.Orc));
            var r = PlacementValidator.Validate(plan, ThreeRooms(), Cat());
            Assert.IsFalse(r.IsValid);
            Assert.AreEqual(PlacementError.OverBudget, r.Error);
        }

        [Test]
        public void UnknownMonsterIsRejected()
        {
            var plan = Plan(Place("r0", new UnitClassId("Dragon")));
            Assert.AreEqual(PlacementError.UnknownMonster,
                PlacementValidator.Validate(plan, ThreeRooms(), Cat()).Error);
        }

        [Test]
        public void PlacementInUnknownRoomIsRejected()
        {
            var plan = Plan(Place("nope", MonsterIds.Imp));
            Assert.AreEqual(PlacementError.InvalidRoom,
                PlacementValidator.Validate(plan, ThreeRooms(), Cat()).Error);
        }

        [Test]
        public void HeroInCoreFrontRoomIsValid()
        {
            var plan = new PlacementPlan { Monsters = new List<MonsterPlacement>(), HasHero = true, HeroRoom = new RoomId("r2") };
            Assert.IsTrue(PlacementValidator.Validate(plan, ThreeRooms(), Cat()).IsValid);
        }

        [Test]
        public void HeroOutsideCoreFrontRoomIsRejected()
        {
            var plan = new PlacementPlan { Monsters = new List<MonsterPlacement>(), HasHero = true, HeroRoom = new RoomId("r0") };
            Assert.AreEqual(PlacementError.HeroRoomNotCoreFront,
                PlacementValidator.Validate(plan, ThreeRooms(), Cat()).Error);
        }

        [Test]
        public void ApplyPlacesDefendersIntoRooms()
        {
            var plan = Plan(Place("r0", MonsterIds.Imp), Place("r0", MonsterIds.Goblin), Place("r2", MonsterIds.Succubus));
            var built = PlacementBuilder.Apply(plan, ThreeRooms(), Cat());
            Assert.AreEqual(2, built.Path[0].Defenders.Count, "r0에 임프+고블린");
            Assert.AreEqual(0, built.Path[1].Defenders.Count);
            Assert.AreEqual(1, built.Path[2].Defenders.Count);
            Assert.IsTrue(built.Path[2].Defenders[0].IsCapturingMonster, "서큐버스는 포획형 플래그 전달");
            Assert.AreEqual(MonsterIds.Succubus, built.Path[2].Defenders[0].ClassId);
        }

        [Test]
        public void ValidateAndApply_InvalidPlan_Throws()
        {
            // 주인공이 코어앞1칸(r2)이 아닌 r0에 배치 -> HeroRoomNotCoreFront.
            var plan = new PlacementPlan { Monsters = new List<MonsterPlacement>(), HasHero = true, HeroRoom = new RoomId("r0") };
            var ex = Assert.Throws<VnRuntimeException>(() => PlacementBuilder.ValidateAndApply(plan, ThreeRooms(), Cat()));
            StringAssert.Contains("HeroRoomNotCoreFront", ex.Message);
        }

        [Test]
        public void ValidateAndApply_ValidPlan_ReturnsGraphWithDefenders()
        {
            // 임프(일반)→일반방 r1, 서큐버스(포획)→함정방 r0.
            var plan = Plan(Place("r1", MonsterIds.Imp), Place("r0", MonsterIds.Succubus));
            var built = PlacementBuilder.ValidateAndApply(plan, TrapAtR0(), Cat());
            Assert.AreEqual(1, built.Path[0].Defenders.Count, "r0(함정방)에 서큐버스");
            Assert.AreEqual(1, built.Path[1].Defenders.Count, "r1(일반방)에 임프");
            Assert.IsTrue(built.Path[0].Defenders[0].IsCapturingMonster);
        }

        [Test]
        public void NonCapturingMonsterInTrapRoomIsRejected()
        {
            // 오크(비포획, cost2)를 함정방 r0에 → 거부.
            var plan = Plan(Place("r0", MonsterIds.Orc));
            Assert.AreEqual(PlacementError.MonsterInTrapRoom,
                PlacementValidator.Validate(plan, TrapAtR0(), Cat()).Error);
        }

        [Test]
        public void CapturingMonsterInTrapRoomIsValid()
        {
            // 서큐버스(포획, cost3)를 함정방 r0에 → 허용.
            var plan = Plan(Place("r0", MonsterIds.Succubus));
            var r = PlacementValidator.Validate(plan, TrapAtR0(), Cat());
            Assert.IsTrue(r.IsValid, "포획몹은 함정방에 배치 가능");
        }

        [Test]
        public void CapturingMonsterInNormalRoomIsRejected()
        {
            // 서큐버스(포획)를 일반방 r1에 → 거부.
            var plan = Plan(Place("r1", MonsterIds.Succubus));
            Assert.AreEqual(PlacementError.CapturerInNormalRoom,
                PlacementValidator.Validate(plan, TrapAtR0(), Cat()).Error);
        }

        [Test]
        public void NonCapturingMonsterInNormalRoomIsValid()
        {
            // 오크(비포획)를 일반방 r1에 → 허용.
            var plan = Plan(Place("r1", MonsterIds.Orc));
            Assert.IsTrue(PlacementValidator.Validate(plan, TrapAtR0(), Cat()).IsValid);
        }
    }
}
