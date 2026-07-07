using System.Collections.Generic;

namespace VNEngine
{
    // 1편 기본 8스탯 데이터 테이블. **초기 추정 튜닝값** — 확장판/2편은 다른 테이블을 주입해 스탯을 늘린다.
    // Core 로직(HeroStats/StatUpgrade)은 이 목록을 하드코딩 참조하지 않고 주어진 StatDef 들을 순회할 뿐.
    // StartValue: STR/INT/DEX/AGI/DEF/LUK=5, HP=50, MP=30 (데이터). Cap=999.
    public static class StatCatalog
    {
        public const int DefaultCap = 999;

        public static IReadOnlyList<StatDef> Default() => new List<StatDef>
        {
            new StatDef(StatIds.STR, "STR", 5, DefaultCap),
            new StatDef(StatIds.INT, "INT", 5, DefaultCap),
            new StatDef(StatIds.DEX, "DEX", 5, DefaultCap),
            new StatDef(StatIds.AGI, "AGI", 5, DefaultCap),
            new StatDef(StatIds.HP, "HP", 50, DefaultCap),
            new StatDef(StatIds.MP, "MP", 30, DefaultCap),
            new StatDef(StatIds.DEF, "DEF", 5, DefaultCap),
            new StatDef(StatIds.LUK, "LUK", 5, DefaultCap),
        };
    }
}
