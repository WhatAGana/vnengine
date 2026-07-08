using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 웨이브 침입자 구성. 크기 생성곡선(5~60)은 비스코프 — 여기선 주어진 구성(병종별 수량)만 다룬다.
    public sealed class WaveDef
    {
        // 병종별 침입자 수량 한 항목. ClassMatchup.Entry 와 동일 패턴(값타입, 공개 필드).
        public struct Entry
        {
            public UnitClassId ClassId;
            public int Count;
            public bool IsNamed; // 네임드(히로인 훅) vs 잡졸(감옥 경제). 미지정 시 false(기존 initializer 무변경).
        }

        public IReadOnlyList<Entry> Intruders { get; }

        public WaveDef(IReadOnlyList<Entry> intruders)
        {
            if (intruders == null) throw new ArgumentNullException(nameof(intruders));

            foreach (var e in intruders)
            {
                if (e.Count <= 0) throw new ArgumentException($"Entry.Count must be positive (classId={e.ClassId}, count={e.Count})", nameof(intruders));
            }

            Intruders = new List<Entry>(intruders); // 방어적 복사
        }
    }
}
