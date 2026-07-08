namespace VNEngine
{
    // ResetPolicy는 서사 미확정 — 플래그 자리만 마련해 둔다. 이 슬라이스는 어떤 값이든
    // 로직으로 강제하지 않는다(회차 리셋/영속 여부는 07-C 이후 서사 결정에 위임).
    public enum ResetPolicy
    {
        Unspecified,
        ResetEachLoop,
        PersistAcrossLoops,
    }

    // 포획된 침입자 1체의 원장 기록(값 타입, 불변). 네임드=히로인 훅, 잡졸=감옥 경제(07-C 인과율 수급 전제).
    public readonly struct Captive
    {
        public UnitClassId ClassId { get; }
        public bool IsNamed { get; }
        public ResetPolicy ResetPolicy { get; }

        public Captive(UnitClassId classId, bool isNamed, ResetPolicy resetPolicy)
        {
            ClassId = classId;
            IsNamed = isNamed;
            ResetPolicy = resetPolicy;
        }

        // 편의: ResetPolicy 미지정 시 Unspecified(플래그 자리만 — 로직 강제 금지).
        public Captive(UnitClassId classId, bool isNamed)
            : this(classId, isNamed, ResetPolicy.Unspecified) { }
    }
}
