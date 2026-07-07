using System.Collections.Generic;

namespace VNEngine
{
    // 구간지수 비용곡선(데이터). 값 v -> v+1 로 올리는 비용 = CostAt(v).
    // 밴드는 오름차순 UpperExclusive 경계 — current < UpperExclusive 인 첫 밴드의 Cost.
    // 마지막 밴드는 상한 없음(UpperExclusive=int.MaxValue). 경계·비용은 전부 데이터(튜닝대상).
    public sealed class StatCostCurve
    {
        public readonly struct Band
        {
            public int UpperExclusive { get; }
            public int Cost { get; }
            public Band(int upperExclusive, int cost)
            {
                UpperExclusive = upperExclusive;
                Cost = cost;
            }
        }

        private readonly Band[] _bands;

        public StatCostCurve(IReadOnlyList<Band> bands)
        {
            if (bands == null || bands.Count == 0)
                throw new System.ArgumentException("bands required", nameof(bands));
            _bands = new Band[bands.Count];
            for (int i = 0; i < bands.Count; i++) _bands[i] = bands[i];
        }

        public int CostAt(int currentValue)
        {
            for (int i = 0; i < _bands.Length; i++)
                if (currentValue < _bands[i].UpperExclusive)
                    return _bands[i].Cost;
            return _bands[_bands.Length - 1].Cost; // 안전망
        }

        // 1편 초기 추정 곡선(튜닝대상): <100->1, <250->2, <450->3, <650->5, <800->9, <950->16, >=950->28.
        public static StatCostCurve Default() => new StatCostCurve(new List<Band>
        {
            new Band(100, 1),
            new Band(250, 2),
            new Band(450, 3),
            new Band(650, 5),
            new Band(800, 9),
            new Band(950, 16),
            new Band(int.MaxValue, 28),
        });
    }
}
