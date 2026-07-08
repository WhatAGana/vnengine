namespace VNEngine
{
    // 여관 내구도 자연감소(웨이브/정비일 단위, 데이터 상수). 0 미만 방지. 수리(골드 소모)는 07-C.
    // Compute(수급)와 순서: C가 배선 시 게이트 판정(Decor>0) 후 Decay 적용이 의도(D는 각 규칙 순수, 배선 안 함).
    public static class InnUpkeepRule
    {
        public const int DecorDecayPerTick = 1;

        public static InnState Decay(InnState inn)
        {
            if (inn == null) throw new System.ArgumentNullException(nameof(inn));
            return inn.WithDecor(System.Math.Max(0, inn.Decor - DecorDecayPerTick));
        }
    }
}
