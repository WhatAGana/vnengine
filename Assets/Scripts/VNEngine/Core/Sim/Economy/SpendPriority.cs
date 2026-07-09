using System.Collections.Generic;

namespace VNEngine
{
    // 소비 카테고리 — 07 §6.4.
    public enum SpendCategory
    {
        DurabilityRepair,
        LevelUp,
        InnInvest,
        MobUpgrade,
        Gacha,
    }

    // 07 §6.4 안전망 기본 소비 우선순위: 내구도 수리 > 레벨업 > 여관 투자 > 몹 강화 > 가챠.
    // 이번 슬라이스는 순서만 노출한다 — 자동 소비 로직 없음, 실제 선택은 유저 몫.
    public static class SpendPriority
    {
        public static readonly IReadOnlyList<SpendCategory> Order = new[]
        {
            SpendCategory.DurabilityRepair,
            SpendCategory.LevelUp,
            SpendCategory.InnInvest,
            SpendCategory.MobUpgrade,
            SpendCategory.Gacha,
        };
    }
}
