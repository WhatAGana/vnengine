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

    // 포획 규칙(데이터 주도). 포획가능 개체가 활성 트리거 중 하나로 처치되면 처치 대신 포획.
    // 1편 Default = Trap/HeroSubdue/CapturingMonster 전부 활성(예시 트리거 — 실측/서사로 조정).
    public sealed class CaptureRule
    {
        public CaptureTrigger Enabled { get; }
        public CaptureRule(CaptureTrigger enabled) { Enabled = enabled; }

        public static CaptureRule Default()
            => new CaptureRule(CaptureTrigger.Trap | CaptureTrigger.HeroSubdue | CaptureTrigger.CapturingMonster);

        public bool ShouldCapture(bool canBeCaptured, CaptureContext ctx)
            => canBeCaptured && (Enabled & ctx.Present) != CaptureTrigger.None;
    }
}
