using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 웨이브 해결 결과. 골드/인과율 환산은 없음(07-C 스코프). 침입자 스냅샷(Attacker)만 분류해 담는다.
    public sealed class CombatResult
    {
        // 웨이브 내 어느 침입자든 하나라도 코어까지 생존 도달하면 true(그 침입자에 한해 발생한 사건을
        // 웨이브 결과 단일 플래그로 집계 — 개별 침입자 단위 결과는 Killed/Captured/코어도달로 3분류됨).
        public bool CoreHit { get; }
        public IReadOnlyList<Attacker> Killed { get; }
        public IReadOnlyList<Attacker> Captured { get; }

        public CombatResult(bool coreHit, IReadOnlyList<Attacker> killed, IReadOnlyList<Attacker> captured)
        {
            if (killed == null) throw new ArgumentNullException(nameof(killed));
            if (captured == null) throw new ArgumentNullException(nameof(captured));

            CoreHit = coreHit;
            Killed = new List<Attacker>(killed); // 방어적 복사
            Captured = new List<Attacker>(captured); // 방어적 복사
        }
    }
}
