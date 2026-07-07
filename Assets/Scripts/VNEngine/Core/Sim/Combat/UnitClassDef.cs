using System;

namespace VNEngine
{
    // 병종 정의(데이터). 능력치 배수는 정수 퍼센트(ThreatBase*pct/100) — 부동소수점 금지.
    public sealed class UnitClassDef
    {
        public UnitClassId Id { get; }
        public string DisplayName { get; }
        public int HpPct { get; }
        public int AtkPct { get; }
        public int DefPct { get; }
        public bool CanBeCaptured { get; }

        public UnitClassDef(UnitClassId id, string displayName, int hpPct, int atkPct, int defPct, bool canBeCaptured)
        {
            if (string.IsNullOrEmpty(id.Value)) throw new ArgumentException("UnitClassId.Value must not be null or empty", nameof(id));
            if (hpPct < 0) throw new ArgumentException("hpPct must not be negative", nameof(hpPct));
            if (atkPct < 0) throw new ArgumentException("atkPct must not be negative", nameof(atkPct));
            if (defPct < 0) throw new ArgumentException("defPct must not be negative", nameof(defPct));

            Id = id;
            DisplayName = displayName;
            HpPct = hpPct;
            AtkPct = atkPct;
            DefPct = defPct;
            CanBeCaptured = canBeCaptured;
        }
    }
}
