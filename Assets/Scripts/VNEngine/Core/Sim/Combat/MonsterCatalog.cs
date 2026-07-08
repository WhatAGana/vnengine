using System.Collections.Generic;

namespace VNEngine
{
    // 방어측 몹 id 상수. **소스는 MonsterCatalog 데이터 테이블**(이 상수는 참조 편의).
    public static class MonsterIds
    {
        public static readonly UnitClassId Imp = new UnitClassId("Imp");
        public static readonly UnitClassId Goblin = new UnitClassId("Goblin");
        public static readonly UnitClassId Orc = new UnitClassId("Orc");
        public static readonly UnitClassId HighOrc = new UnitClassId("HighOrc");
        public static readonly UnitClassId Succubus = new UnitClassId("Succubus");
        public static readonly UnitClassId DeathKnight = new UnitClassId("DeathKnight");
        public static readonly UnitClassId HighDemon = new UnitClassId("HighDemon");
    }

    // 1편 방어몹 데이터. Cost=레어도, Base*=배치시 방어 Attacker 능력치(초기 추정 — 실측 튜닝 대상).
    // Succubus만 포획형(마지막 타격 시 포획 트리거 — 아그네스 해금 훅). Skills[]는 08 스코프.
    // 값은 구조검증용 초기 추정, 플레이테스트 실측 튜닝.
    public static class MonsterCatalog
    {
        public static IReadOnlyList<MonsterDef> Default() => new List<MonsterDef>
        {
            new MonsterDef(MonsterIds.Imp,         "임프",     1,  30,  15, 10, false),
            new MonsterDef(MonsterIds.Goblin,      "고블린",   1,  35,  18, 12, false),
            new MonsterDef(MonsterIds.Orc,         "오크",     2,  70,  35, 25, false),
            new MonsterDef(MonsterIds.HighOrc,     "하이오크", 3, 110,  55, 40, false),
            new MonsterDef(MonsterIds.Succubus,    "서큐버스", 3,  80,  60, 30, true),
            new MonsterDef(MonsterIds.DeathKnight, "데스나이트", 4, 160,  90, 70, false),
            new MonsterDef(MonsterIds.HighDemon,   "고위마족", 5, 220, 130, 90, false),
        };
    }
}
