using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 방 노드의 그래프. 전투 경로(Path)는 Entry에서 NextRooms[0]을 따라 terminal(NextRooms 빈 방)까지.
    // 선형 던전은 각 방 NextRooms가 1개인 특수케이스. 코어는 암묵적 — Path 마지막 방이 "코어앞1칸".
    public sealed class RoomGraph
    {
        public IReadOnlyList<RoomNode> Rooms { get; }
        public RoomId Entry { get; }
        public IReadOnlyList<RoomNode> Path { get; }   // 생성 시 1회 계산(불변)
        public bool IsEmpty => Rooms.Count == 0;
        public RoomNode CoreFrontRoom => Path.Count == 0 ? null : Path[Path.Count - 1];

        public RoomGraph(IReadOnlyList<RoomNode> rooms, RoomId entry)
        {
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            var copy = new List<RoomNode>(rooms);
            Rooms = copy;
            Entry = entry;

            var byId = new Dictionary<RoomId, RoomNode>(copy.Count);
            foreach (var r in copy)
            {
                if (r == null) throw new ArgumentException("rooms must not contain null", nameof(rooms));
                byId[r.Id] = r;
                foreach (var nx in r.NextRooms) { /* 존재검증은 아래 별도 루프에서(전체 등록 후) */ }
            }
            foreach (var r in copy)
                foreach (var nx in r.NextRooms)
                    if (!byId.ContainsKey(nx))
                        throw new ArgumentException($"NextRoom '{nx}' not found in graph (room '{r.Id}')", nameof(rooms));

            Path = ComputePath(byId, entry);
        }

        // NextRooms[0]을 따라 terminal까지. 사이클은 방문집합으로 차단(그래프 데이터 오류 방어).
        private static IReadOnlyList<RoomNode> ComputePath(Dictionary<RoomId, RoomNode> byId, RoomId entry)
        {
            var path = new List<RoomNode>();
            if (byId.Count == 0) return path;
            if (!byId.TryGetValue(entry, out var cur))
                throw new ArgumentException($"Entry room '{entry}' not found in graph", nameof(entry));

            var visited = new HashSet<RoomId>();
            while (cur != null)
            {
                if (!visited.Add(cur.Id))
                    throw new VnRuntimeException($"Cycle detected in RoomGraph at '{cur.Id}'");
                path.Add(cur);
                if (cur.NextRooms.Count == 0) break;
                cur = byId[cur.NextRooms[0]];
            }
            return path;
        }

        // 방 "내용물" 목록을 받아 r0->r1->...->rN 선형 체인으로 링크한 그래프. Path == 입력 순서.
        public static RoomGraph Linear(IReadOnlyList<RoomNode> contentRooms)
        {
            if (contentRooms == null) throw new ArgumentNullException(nameof(contentRooms));
            var nodes = new List<RoomNode>(contentRooms.Count);
            for (int i = 0; i < contentRooms.Count; i++)
            {
                var id = new RoomId("r" + i);
                var next = i + 1 < contentRooms.Count
                    ? new List<RoomId> { new RoomId("r" + (i + 1)) }
                    : (IReadOnlyList<RoomId>)Array.Empty<RoomId>();
                nodes.Add(new RoomNode(id, contentRooms[i].Defenders, contentRooms[i].HasTrap, next));
            }
            return new RoomGraph(nodes, contentRooms.Count == 0 ? default : new RoomId("r0"));
        }
    }
}
