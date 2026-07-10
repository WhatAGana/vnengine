using System;

namespace VNEngine
{
    // 포획 트리거(비트플래그). 처치의 "마지막 타격"이 어떤 조건에서 났는지.
    [Flags]
    public enum CaptureTrigger { None = 0, Trap = 1, HeroSubdue = 2, CapturingMonster = 4 }

    // 처치 시점에 존재한 트리거 집합.
    public readonly struct CaptureContext
    {
        public CaptureTrigger Present { get; }
        public CaptureContext(CaptureTrigger present) { Present = present; }
    }

    // 포획 규칙(데이터 주도). 포획가능 개체가 다음 중 하나로 처치될 때 처치 대신 포획:
    //   (1) 함정방 AND 포획몹(둘 다 성립) — 함정방의 포획몹이 격퇴. (함정만/포획몹만으로는 포획 안 됨.)
    //   (2) 주인공 제압(HeroSubdue) — 코어앞1칸 유인 제압.
    // Enabled 마스크가 각 트리거의 활성 여부를 데이터로 통제(예: HeroSubdue 끄기, 몹포획경로 끄기).
    public sealed class CaptureRule
    {
        public CaptureTrigger Enabled { get; }
        public CaptureRule(CaptureTrigger enabled) { Enabled = enabled; }

        public static CaptureRule Default()
            => new CaptureRule(CaptureTrigger.Trap | CaptureTrigger.HeroSubdue | CaptureTrigger.CapturingMonster);

        public bool ShouldCapture(bool canBeCaptured, CaptureContext ctx)
        {
            if (!canBeCaptured) return false;
            var active = Enabled & ctx.Present; // 활성 트리거만 유효
            bool monsterCapture = (active & CaptureTrigger.Trap) != CaptureTrigger.None
                                  && (active & CaptureTrigger.CapturingMonster) != CaptureTrigger.None;
            bool subdueCapture = (active & CaptureTrigger.HeroSubdue) != CaptureTrigger.None;
            return monsterCapture || subdueCapture;
        }
    }
}
