using System.Collections.Generic;

namespace VNEngine
{
    // 검증 통과한 PlacementPlan을 실제 방어 배치(그래프의 각 방 Defenders)로 실체화. 순수 함수(새 그래프 반환).
    public static class PlacementBuilder
    {
        public static RoomGraph Apply(PlacementPlan plan, RoomGraph graph, IReadOnlyList<MonsterDef> catalog)
        {
            if (plan.Monsters == null) throw new System.ArgumentNullException(nameof(plan));
            if (graph == null) throw new System.ArgumentNullException(nameof(graph));
            if (catalog == null) throw new System.ArgumentNullException(nameof(catalog));

            var defById = new Dictionary<UnitClassId, MonsterDef>(catalog.Count);
            foreach (var m in catalog) defById[m.Id] = m;

            var perRoom = new Dictionary<RoomId, List<Attacker>>();
            foreach (var p in plan.Monsters)
            {
                if (!defById.TryGetValue(p.Monster, out var def))
                    throw new VnRuntimeException($"Unknown monster in placement: {p.Monster}");
                if (!perRoom.TryGetValue(p.Room, out var list)) { list = new List<Attacker>(); perRoom[p.Room] = list; }
                list.Add(new Attacker(def.Id, def.BaseHp, def.BaseAtk, def.BaseDef, false, def.IsCapturingMonster));
            }

            var rebuilt = new List<RoomNode>(graph.Rooms.Count);
            foreach (var room in graph.Rooms)
            {
                var defs = perRoom.TryGetValue(room.Id, out var placed) ? placed : new List<Attacker>();
                rebuilt.Add(new RoomNode(room.Id, defs, room.HasTrap, room.NextRooms));
            }
            return new RoomGraph(rebuilt, graph.Entry);
        }

        // 검증 게이트를 강제하는 진입점 — 호출자가 미검증 플랜으로 그래프를 만들 수 없게 한다(07-B defer 해소).
        public static RoomGraph ValidateAndApply(PlacementPlan plan, RoomGraph graph, IReadOnlyList<MonsterDef> catalog)
        {
            var result = PlacementValidator.Validate(plan, graph, catalog);
            if (!result.IsValid)
                throw new VnRuntimeException($"Invalid placement: {result.Error}");
            return Apply(plan, graph, catalog);
        }
    }
}
