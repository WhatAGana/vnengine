namespace VNEngine
{
    // 던전레벨 = 지을 수 있는 방 개수 천장. 실제 건설(골드 소모)은 07-C — 여기선 상한 규칙만(순수).
    public static class DungeonRoomRule
    {
        public const int BaseRooms = 3;
        public const int RoomsPerLevel = 2;

        public static int RoomsCap(int dungeonLevel)
        {
            if (dungeonLevel < 1) throw new System.ArgumentOutOfRangeException(nameof(dungeonLevel));
            return BaseRooms + dungeonLevel * RoomsPerLevel;
        }

        public static bool CanBuildRoom(int currentRoomCount, int dungeonLevel)
            => currentRoomCount < RoomsCap(dungeonLevel);
    }
}
