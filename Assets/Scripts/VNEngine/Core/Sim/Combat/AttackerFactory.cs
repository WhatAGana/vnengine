using System;

namespace VNEngine
{
    // 침입자 개체 능력치 생성: ThreatBase * 병종 프로파일(%) + 시드결정론 편차(±5), min1 클램프.
    public static class AttackerFactory
    {
        private const int DeviationMin = -5;
        private const int DeviationMax = 5;

        public static Attacker Create(UnitClassDef cls, int threatBase, bool isNamed, IRandom rng)
        {
            if (cls == null) throw new ArgumentNullException(nameof(cls));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            // 호출 순서 고정(결정론 재현): HP, ATK, DEF. isNamed는 rng를 소비하지 않는다(결정론 순서 불변).
            var hp = Math.Max(1, threatBase * cls.HpPct / 100 + rng.Range(DeviationMin, DeviationMax));
            var atk = Math.Max(1, threatBase * cls.AtkPct / 100 + rng.Range(DeviationMin, DeviationMax));
            var def = Math.Max(1, threatBase * cls.DefPct / 100 + rng.Range(DeviationMin, DeviationMax));

            return new Attacker(cls.Id, hp, atk, def, cls.CanBeCaptured, false, isNamed);
        }
    }
}
