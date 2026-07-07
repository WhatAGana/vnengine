using System;

namespace VNEngine
{
    // 침입자 강도 기준선(ThreatBase) 계산. heroPower 는 반드시 StatCombatWeights.Derive(...).CombatPower
    // 스칼라여야 한다(raw 스탯합 절대 금지) — HeroPowerOf 가 그 환산 경로를 명시적으로 강제한다.
    public static class ThreatFormula
    {
        public static int Compute(ThreatWeights w, int heroPower, int loopCount, int avgPlacedMonsterLevel, int dungeonLevel)
        {
            if (w == null) throw new ArgumentNullException(nameof(w));

            var raw = w.WHero * heroPower
                + w.WLoop * (loopCount - 1)
                + w.WPlaced * avgPlacedMonsterLevel
                + w.WDungeon * dungeonLevel
                + w.BaseOffset;

            return Math.Max(1, raw);
        }

        // heroPower 입력을 CombatPower 스칼라로 환산하는 편의 래퍼. 호출자가 Task3(StatCombatWeights)를
        // 경유했음을 명시하고, raw 스탯합을 heroPower 로 넘기는 실수를 구조적으로 막는다.
        public static int HeroPowerOf(StatCombatWeights weights, HeroStats hero)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            return weights.Derive(hero).CombatPower;
        }
    }
}
