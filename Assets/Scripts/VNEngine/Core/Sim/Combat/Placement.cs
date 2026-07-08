using System.Collections.Generic;

namespace VNEngine
{
    // 배치 1건: 특정 방에 특정 몹종을 배치. 값타입, 공개필드(테스트/데이터 조립 편의).
    public struct MonsterPlacement
    {
        public RoomId Room;
        public UnitClassId Monster;
    }

    // 배치 플랜(검증 전 입력). HeroRoom은 HasHero=true일 때만 유효 — nullable 회피 위해 bool 플래그.
    public struct PlacementPlan
    {
        public IReadOnlyList<MonsterPlacement> Monsters;
        public bool HasHero;
        public RoomId HeroRoom;
    }

    public enum PlacementError
    {
        None,
        OverBudget,
        UnknownMonster,
        InvalidRoom,
        HeroRoomNotCoreFront,
    }

    // 검증 결과(순수 데이터). UI 게이트용이라 예외가 아니라 Error 코드로 반환.
    public readonly struct PlacementResult
    {
        public bool IsValid { get; }
        public int Budget { get; }
        public int TotalCost { get; }
        public PlacementError Error { get; }

        public PlacementResult(bool isValid, int budget, int totalCost, PlacementError error)
        {
            IsValid = isValid;
            Budget = budget;
            TotalCost = totalCost;
            Error = error;
        }
    }
}
