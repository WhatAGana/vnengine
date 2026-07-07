using System;

namespace VNEngine
{
    // 명중/치명/회피 판정에 쓰는 데이터 계수 묶음(전부 정수, 데이터 주도 — 호출자가 주입).
    public struct HitParams
    {
        public int HitRating;
        public int Evasion;
        public int CritRating;
        public int CritMultiplierPct;
        public int HitRollMax;
    }

    // 데미지 공식: 분기형 원시데미지 + 상성 배수 + 명중/치명/회피. 전부 정수 연산(부동소수점 금지).
    public static class DamageFormula
    {
        // 분기형 원시데미지. ATK>=DEF 이면 선형(atk*2-def, ATK=DEF일 때도 atk 자체로 항상 양수),
        // ATK<DEF 이면 제곱/방어(atk*atk/def, 정수나눗셈 — 0이 될 수 있음. floor는 Apply 몫).
        public static int Raw(int atk, int def)
        {
            if (def <= 0) throw new ArgumentException("def must be positive", nameof(def));
            return atk >= def ? atk * 2 - def : atk * atk / def;
        }

        // 상성 배수 적용 후 최소 1 보장.
        public static int Apply(int atk, int def, int matchupPct)
        {
            return Math.Max(1, Raw(atk, def) * matchupPct / 100);
        }

        // 명중판정 -> 빗나가면 0(min1 미적용, 의도된 예외) -> 명중이면 Apply 후 치명배수.
        // rng 호출 순서 고정(결정론 재현): 명중 롤, (명중시에만) 치명 롤.
        public static int Resolve(int atk, int def, int matchupPct, HitParams hitParams, IRandom rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var hitRoll = rng.Range(1, hitParams.HitRollMax);
            if (hitRoll > hitParams.HitRating - hitParams.Evasion) return 0; // 빗나감

            var dmg = Apply(atk, def, matchupPct);

            var critRoll = rng.Range(1, hitParams.HitRollMax);
            if (critRoll <= hitParams.CritRating)
                dmg = dmg * hitParams.CritMultiplierPct / 100;

            return dmg;
        }
    }
}
