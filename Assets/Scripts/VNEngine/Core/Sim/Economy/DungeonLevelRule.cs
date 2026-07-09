namespace VNEngine
{
    // 던전 레벨업 비용 — 07 §5.2 지수곡선 base=120, exp=1.32.
    // 코어 정수전용이라 float pow 대신 공식을 사전계산한 정수 테이블(데이터). 초기 추정 튜닝값.
    // 방 개수 천장은 Economy/DungeonRoomRule.cs 담당 — 여기선 레벨업 골드 비용만.
    public static class DungeonLevelRule
    {
        public const int MaxTabulatedLevel = 20;

        // index = dungeonLevel, [0] unused. floor(120 * dl^1.32).
        private static readonly int[] _cost =
        {
            0, 120, 299, 511, 747, 1004, 1277, 1565, 1867, 2181, 2507,
            2843, 3189, 3544, 3909, 4281, 4662, 5050, 5446, 5849, 6259
        };

        public static int LevelUpCost(int dungeonLevel)
        {
            if (dungeonLevel < 1)
                throw new VnRuntimeException($"dungeonLevel must be >= 1: {dungeonLevel}");
            int i = dungeonLevel > MaxTabulatedLevel ? MaxTabulatedLevel : dungeonLevel;
            return _cost[i];
        }
    }
}
