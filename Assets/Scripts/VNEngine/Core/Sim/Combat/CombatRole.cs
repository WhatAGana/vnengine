namespace VNEngine
{
    // 전투 역할은 전투 규칙상 고정 집합(콘텐츠 확장 대상 아님)이므로 enum 허용.
    // 스탯 쪽(StatId)은 여전히 데이터 주도 — 이 enum과 혼동하지 말 것.
    public enum CombatRole
    {
        PhysicalAttack,
        MagicAttack,
        Defense,
        HitRating,
        CritRating,
        Evasion,
        Health,
        SkillResource,
        CombatPower,
    }
}
