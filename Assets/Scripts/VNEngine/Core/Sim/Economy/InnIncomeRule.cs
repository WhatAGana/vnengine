namespace VNEngine
{
    // 여관 수급 산식(순수·결정론·데이터주도). 완화(제곱근)+상한(cap)으로 인플레 방지(시뮬6차).
    // ★ 손님수는 여관규모(Staff/MenuLevel)에만 연동 — dungeonLevel 파라미터 없음(시뮬5차 폭주 교훈).
    // ★ 새 수입원엔 기존과 같은 완화/상한 → 여관 골드비중 목표 <10%. innKarma=guests(성장 주축).
    // 계수는 전부 초기 추정 튜닝값(플레이테스트 실측 조정 대상).
    public static class InnIncomeRule
    {
        public const int GuestsPerStaff = 2;
        public const int MaxGuests = 25;
        public const int GoldPerSqrtGuest = 8;
        public const int GoldPerMenuLevel = 3;
        public const int MaxGold = 300;

        public static InnIncome Compute(InnState inn)
        {
            if (inn == null) throw new System.ArgumentNullException(nameof(inn));
            if (inn.Decor <= 0) return InnIncome.Zero;  // 내구도 게이트: 손님 안 받음(수입 0)

            int guests = System.Math.Min(inn.Staff * GuestsPerStaff + inn.MenuLevel, MaxGuests);
            int gold = System.Math.Min(IntMath.Isqrt(guests) * GoldPerSqrtGuest + inn.MenuLevel * GoldPerMenuLevel, MaxGold);
            int karma = guests;
            return new InnIncome(gold, karma, guests);
        }
    }
}
