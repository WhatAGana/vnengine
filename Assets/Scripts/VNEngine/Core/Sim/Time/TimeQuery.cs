namespace VNEngine
{
    // 90일 시간구조 순수 질의 — 페이즈/세이브일/다음웨이브. 스킵·자동 진행이 참조.
    // 90일 = 9주기 × 10일. 각 주기 1~9일 정비 / 10일 웨이브. 전부 순수·결정론.
    public static class TimeQuery
    {
        public const int MaxDay = 90;
        public const int WaveInterval = 10;
        public const int Cycles = 9;
        public const int SaveDayInCycle = 9; // 각 주기 9일차(웨이브 전날) 세이브 확인

        public static DayPhase GetPhase(int day)
            => IsWaveDay(day) ? DayPhase.Wave : DayPhase.Maintenance;

        public static bool IsWaveDay(int day)
        {
            Require(day);
            return day % WaveInterval == 0 && day <= MaxDay;
        }

        public static bool IsSaveDay(int day)
        {
            Require(day);
            return day % WaveInterval == SaveDayInCycle;
        }

        // 현재 일자에서 다음 웨이브까지 남은 일수. 마지막 웨이브(90일) 이후엔 0.
        public static int DaysUntilNextWave(int day)
        {
            Require(day);
            if (day >= MaxDay) return 0;
            int nextWave = (day / WaveInterval + 1) * WaveInterval;
            return nextWave - day;
        }

        private static void Require(int day)
        {
            if (day < 1)
                throw new VnRuntimeException($"day must be >= 1: {day}");
        }
    }
}
