namespace VNEngine
{
    // 스탯 정의(데이터). 전투 역할은 여기 없음 — 07-A2 공식이 Id 로 간접 참조. 순수 값 정의만.
    public sealed class StatDef
    {
        public StatId Id { get; }
        public string DisplayName { get; }
        public int StartValue { get; }
        public int Cap { get; }

        public StatDef(StatId id, string displayName, int startValue, int cap)
        {
            Id = id;
            DisplayName = displayName;
            StartValue = startValue;
            Cap = cap;
        }
    }
}
