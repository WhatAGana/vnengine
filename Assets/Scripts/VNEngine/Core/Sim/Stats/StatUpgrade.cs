namespace VNEngine
{
    public readonly struct StatUpgradeResult
    {
        public HeroStats Stats { get; }
        public int KarmaSpent { get; }
        public int PointsGained { get; }

        public StatUpgradeResult(HeroStats stats, int karmaSpent, int pointsGained)
        {
            Stats = stats;
            KarmaSpent = karmaSpent;
            PointsGained = pointsGained;
        }
    }

    // 인과율(karma) -> 스탯 강화. 순수 함수: 입력 HeroStats 불변, 새 결과 반환.
    // Cap 은 StatDef, 비용은 StatCostCurve 로 데이터 주입. karma 수급/저금은 07-C.
    public static class StatUpgrade
    {
        public static StatUpgradeResult Upgrade(HeroStats stats, StatDef def, StatCostCurve curve, int karmaAvailable)
        {
            if (stats == null) throw new System.ArgumentNullException(nameof(stats));
            if (def == null) throw new System.ArgumentNullException(nameof(def));
            if (curve == null) throw new System.ArgumentNullException(nameof(curve));

            int cur = stats.TryGet(def.Id, out var v) ? v : def.StartValue;
            int spent = 0;
            int gained = 0;

            while (cur < def.Cap)
            {
                int cost = curve.CostAt(cur);
                if (karmaAvailable - spent < cost) break; // 남은 karma 로 다음 포인트 못 삼
                spent += cost;
                cur += 1;
                gained += 1;
            }

            var newStats = gained > 0 ? stats.WithStat(def.Id, cur) : stats;
            return new StatUpgradeResult(newStats, spent, gained);
        }
    }
}
