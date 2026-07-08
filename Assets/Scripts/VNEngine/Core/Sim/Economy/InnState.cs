namespace VNEngine
{
    // 여관 상태(메타 귀속 — 회차 넘어 유지). 직원/내구도(Decor)/메뉴레벨. 불변: 변경은 새 InnState 반환.
    // 골드 소모(고용/수리/개발 비용)는 07-C. 여기선 상태 보관 + 수급 규칙(InnIncomeRule)의 입력.
    public sealed class InnState
    {
        public int Staff { get; }
        public int Decor { get; }
        public int MenuLevel { get; }

        public static readonly InnState Empty = new InnState(0, 0, 0);

        public InnState(int staff, int decor, int menuLevel)
        {
            if (staff < 0) throw new System.ArgumentException("staff must be non-negative", nameof(staff));
            if (decor < 0) throw new System.ArgumentException("decor must be non-negative", nameof(decor));
            if (menuLevel < 0) throw new System.ArgumentException("menuLevel must be non-negative", nameof(menuLevel));
            Staff = staff; Decor = decor; MenuLevel = menuLevel;
        }

        // 내구도만 교체(자연감소/수리용). 나머지 필드 보존.
        public InnState WithDecor(int decor) => new InnState(Staff, decor, MenuLevel);
    }
}
