namespace VNEngine
{
    // 침입자 개체(값 타입, 불변 데이터 컨테이너). 전투 중 HP 감소는 이 값을 직접 고치지 않고
    // 호출자(resolver)가 로컬 변수로 추적한다 — 원본 Attacker 는 언제나 생성 시점의 스냅샷.
    public readonly struct Attacker
    {
        public UnitClassId ClassId { get; }
        public int Hp { get; }
        public int Atk { get; }
        public int Def { get; }
        public bool CanBeCaptured { get; }
        public bool IsCapturingMonster { get; }   // 방어측 몹이 포획형(서큐버스류)인가. 침입자는 항상 false.
        public bool IsNamed { get; }              // 네임드(히로인 훅) vs 잡졸(감옥 경제) 구분. 07-C 인과율 수급 전제.

        public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured, bool isCapturingMonster, bool isNamed)
        {
            ClassId = classId;
            Hp = hp;
            Atk = atk;
            Def = def;
            CanBeCaptured = canBeCaptured;
            IsCapturingMonster = isCapturingMonster;
            IsNamed = isNamed;
        }

        public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured, bool isCapturingMonster)
            : this(classId, hp, atk, def, canBeCaptured, isCapturingMonster, false) { }

        // 편의: 포획형 아님(기존 호출부·침입자 기본).
        public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured)
            : this(classId, hp, atk, def, canBeCaptured, false, false) { }
    }
}
