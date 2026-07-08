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

            var costById = new Dictionary<UnitClassId, int>(catalog.Count);
            foreach (var m in catalog) costById[m.Id] = m.Cost;
            var roomIds = new HashSet<RoomId>();
            foreach (var r in graph.Rooms) roomIds.Add(r.Id);

            var total = 0;
            foreach (var p in plan.Monsters)
            {
                if (!costById.TryGetValue(p.Monster, out var cost))
                    return new PlacementResult(false, budget, total, PlacementError.UnknownMonster);
                if (!roomIds.Contains(p.Room))
                    return new PlacementResult(false, budget, total, PlacementError.InvalidRoom);
                total += cost;
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
