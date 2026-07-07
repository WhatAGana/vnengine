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

        public Attacker(UnitClassId classId, int hp, int atk, int def, bool canBeCaptured)
        {
            ClassId = classId;
            Hp = hp;
            Atk = atk;
            Def = def;
            CanBeCaptured = canBeCaptured;
        }
    }
}
