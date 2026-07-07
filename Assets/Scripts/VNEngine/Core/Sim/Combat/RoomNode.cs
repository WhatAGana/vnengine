using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 던전 경로상의 방 하나. 배치된 방어몹(Attacker, 이미 생성완료된 값)과 함정 여부만 담는다.
    // 예산/코스트 배치 로직은 07-B 스코프 — 여기선 "배치 결과"를 그대로 받아 불변 스냅샷으로 보관한다.
    // 함정(HasTrap)은 방 자체에 대미지원이 아니라 "포획 트리거" 플래그(최소구현) — 이 방에서
    // 방어몹의 공격으로 침입자가 처치되면, 포획 가능 개체는 Killed 대신 Captured 로 분류된다.
    public sealed class RoomNode
    {
        public IReadOnlyList<Attacker> Defenders { get; }
        public bool HasTrap { get; }

        public RoomNode(IReadOnlyList<Attacker> defenders, bool hasTrap)
        {
            if (defenders == null) throw new ArgumentNullException(nameof(defenders));

            var copy = new List<Attacker>(defenders); // 방어적 복사
            Defenders = copy;
            HasTrap = hasTrap;
        }
    }
}
