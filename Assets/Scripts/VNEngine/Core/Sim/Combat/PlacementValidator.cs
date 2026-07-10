using System.Collections.Generic;

namespace VNEngine
{
    // 배치 예산제 검증(순수 쿼리). 예산 = 방수 × BudgetPerRoom. 몹 코스트합 ≤ 예산 + 개체/방 유효성 + 주인공 코어앞1칸.
    // UI 게이트용이라 예외가 아니라 PlacementResult(Error 코드)로 반환.
    public static class PlacementValidator
    {
        public const int BudgetPerRoom = 3;

        public static PlacementResult Validate(PlacementPlan plan, RoomGraph graph, IReadOnlyList<MonsterDef> catalog)
        {
            if (plan.Monsters == null) throw new System.ArgumentNullException(nameof(plan));
            if (graph == null) throw new System.ArgumentNullException(nameof(graph));
            if (catalog == null) throw new System.ArgumentNullException(nameof(catalog));

            var budget = graph.Rooms.Count * BudgetPerRoom;

            var defById = new Dictionary<UnitClassId, MonsterDef>(catalog.Count);
            foreach (var m in catalog) defById[m.Id] = m;
            var roomById = new Dictionary<RoomId, RoomNode>();
            foreach (var r in graph.Rooms) roomById[r.Id] = r;

            var total = 0;
            foreach (var p in plan.Monsters)
            {
                if (!defById.TryGetValue(p.Monster, out var def))
                    return new PlacementResult(false, budget, total, PlacementError.UnknownMonster);
                if (!roomById.TryGetValue(p.Room, out var room))
                    return new PlacementResult(false, budget, total, PlacementError.InvalidRoom);

                // 함정방=포획몹만, 일반방=일반몹만.
                if (room.HasTrap && !def.IsCapturingMonster)
                    return new PlacementResult(false, budget, total, PlacementError.MonsterInTrapRoom);
                if (!room.HasTrap && def.IsCapturingMonster)
                    return new PlacementResult(false, budget, total, PlacementError.CapturerInNormalRoom);

                total += def.Cost;
            }
            if (total > budget)
                return new PlacementResult(false, budget, total, PlacementError.OverBudget);

            if (plan.HasHero)
            {
                var coreFront = graph.CoreFrontRoom;
                if (coreFront == null || !plan.HeroRoom.Equals(coreFront.Id))
                    return new PlacementResult(false, budget, total, PlacementError.HeroRoomNotCoreFront);
            }
            return new PlacementResult(true, budget, total, PlacementError.None);
        }
    }
}
