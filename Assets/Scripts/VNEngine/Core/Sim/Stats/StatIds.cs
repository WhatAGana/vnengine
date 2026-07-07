namespace VNEngine
{
    // 자주 참조하는 1편 스탯 id 상수. **스탯 목록의 소스가 아님** — 소스는 StatDef 데이터 테이블(StatCatalog).
    // 매직스트링("STR")을 코드 곳곳에 흩뿌리지 않기 위한 참조 편의일 뿐.
    public static class StatIds
    {
        public static readonly StatId STR = new StatId("STR");
        public static readonly StatId INT = new StatId("INT");
        public static readonly StatId DEX = new StatId("DEX");
        public static readonly StatId AGI = new StatId("AGI");
        public static readonly StatId HP = new StatId("HP");
        public static readonly StatId MP = new StatId("MP");
        public static readonly StatId DEF = new StatId("DEF");
        public static readonly StatId LUK = new StatId("LUK");
    }
}
