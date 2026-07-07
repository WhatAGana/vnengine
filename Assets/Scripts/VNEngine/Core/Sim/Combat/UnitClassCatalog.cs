using System.Collections.Generic;

namespace VNEngine
{
    // 자주 참조하는 1편 병종 id 상수. **병종 목록의 소스가 아님** — 소스는 UnitClassDef 데이터 테이블(UnitClassCatalog).
    public static class UnitClassIds
    {
        public static readonly UnitClassId Tank = new UnitClassId("Tank");
        public static readonly UnitClassId Mage = new UnitClassId("Mage");
        public static readonly UnitClassId Paladin = new UnitClassId("Paladin");
        public static readonly UnitClassId Archer = new UnitClassId("Archer");
        public static readonly UnitClassId Priest = new UnitClassId("Priest");
    }

    // 1편 기본 5병종 데이터 테이블. **초기 추정 튜닝값**.
    public static class UnitClassCatalog
    {
        public static IReadOnlyList<UnitClassDef> Default() => new List<UnitClassDef>
        {
            new UnitClassDef(UnitClassIds.Tank, "탱커", 150, 60, 150, false),
            new UnitClassDef(UnitClassIds.Mage, "마법사", 70, 150, 60, true),
            new UnitClassDef(UnitClassIds.Paladin, "성기사", 100, 100, 100, false),
            new UnitClassDef(UnitClassIds.Archer, "궁수", 70, 100, 70, true),
            new UnitClassDef(UnitClassIds.Priest, "성직", 60, 60, 60, true),
        };
    }
}
