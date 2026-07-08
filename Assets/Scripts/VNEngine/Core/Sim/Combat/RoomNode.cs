using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 던전 경로상의 방 노드(불변 스냅샷). 배치된 방어몹(Attacker) + 함정 플래그 + 그래프 링크(NextRooms).
    // 선형 배치는 NextRooms가 1개인 특수케이스. 배치/코스트/포획 규칙은 07-B의 다른 타입이 담당.
    public sealed class RoomNode
    {
        public RoomId Id { get; }
        public IReadOnlyList<Attacker> Defenders { get; }
        public bool HasTrap { get; }
        public IReadOnlyList<RoomId> NextRooms { get; }

        public RoomNode(RoomId id, IReadOnlyList<Attacker> defenders, bool hasTrap, IReadOnlyList<RoomId> nextRooms)
        {
            if (defenders == null) throw new ArgumentNullException(nameof(defenders));
            if (nextRooms == null) throw new ArgumentNullException(nameof(nextRooms));
            Id = id;
            Defenders = new List<Attacker>(defenders);       // 방어적 복사
            HasTrap = hasTrap;
            NextRooms = new List<RoomId>(nextRooms);         // 방어적 복사
        }

        // 편의 생성자: 방 "내용물"만(그래프 링크 없음). RoomGraph.Linear가 Id/NextRooms를 재구성한다.
        public RoomNode(IReadOnlyList<Attacker> defenders, bool hasTrap)
            : this(default, defenders, hasTrap, System.Array.Empty<RoomId>()) { }
    }
}
