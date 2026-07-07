using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 병종 간 상성 배수(데이터, 정수%). 미등록 쌍은 기본 100(중립) — 하드코딩 상수가 아니라
    // "미등록=중립" 규칙 자체가 도메인 규칙이다. 주인공(무병종) 공격은 등록되지 않으므로 항상 100.
    public sealed class ClassMatchup
    {
        public const int Neutral = 100;

        public struct Entry
        {
            public UnitClassId Atk;
            public UnitClassId Def;
            public int Percent;
        }

        private readonly Dictionary<(UnitClassId, UnitClassId), int> _table;

        public ClassMatchup(IReadOnlyList<Entry> entries)
        {
            if (entries == null) throw new ArgumentNullException(nameof(entries));

            _table = new Dictionary<(UnitClassId, UnitClassId), int>(entries.Count);
            foreach (var e in entries)
            {
                if (e.Percent < 0) throw new ArgumentException($"Percent must not be negative (atk={e.Atk}, def={e.Def}, percent={e.Percent})", nameof(entries));

                var key = (e.Atk, e.Def);
                if (_table.ContainsKey(key))
                    throw new ArgumentException($"Duplicate matchup entry for (atk={e.Atk}, def={e.Def})", nameof(entries));

                _table[key] = e.Percent;
            }
        }

        public int Multiplier(UnitClassId atk, UnitClassId def)
        {
            return _table.TryGetValue((atk, def), out var pct) ? pct : Neutral;
        }

        // 1편 초기추정 상성 예시. 나머지 쌍은 전부 중립(100).
        public static ClassMatchup Default() => new ClassMatchup(new List<Entry>
        {
            new Entry { Atk = UnitClassIds.Archer, Def = UnitClassIds.Mage, Percent = 150 },
            new Entry { Atk = UnitClassIds.Archer, Def = UnitClassIds.Tank, Percent = 70 },
            new Entry { Atk = UnitClassIds.Mage, Def = UnitClassIds.Tank, Percent = 70 },
            new Entry { Atk = UnitClassIds.Paladin, Def = UnitClassIds.Mage, Percent = 130 },
        });
    }
}
