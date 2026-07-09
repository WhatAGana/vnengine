namespace VNEngine
{
    // 진행 컨트롤러(모듈): AdvanceDay를 어떤 리듬으로/언제까지 호출하느냐. 코어 무변경으로 확장.
    // ★스킵은 정비 구간만 전진하고 웨이브 전날에서 멈춘다(웨이브는 Step으로만 해소) — 스킵=정산방식.
    public static class TimeController
    {
        public static AdvanceResult Step(CampaignState c, DayContext ctx, IRandom rng)
            => CampaignDayRule.AdvanceDay(c, ctx, rng);

        public static SkipResult SkipToNextWave(CampaignState c, DayContext ctx, IRandom rng)
        {
            if (c == null) throw new System.ArgumentNullException(nameof(c));
            if (ctx == null) throw new System.ArgumentNullException(nameof(ctx));
            if (rng == null) throw new System.ArgumentNullException(nameof(rng));

            int advanced = 0;
            while (c.Run.Day < TimeQuery.MaxDay
                   && TimeQuery.GetPhase(c.Run.Day + 1) == DayPhase.Maintenance)
            {
                c = CampaignDayRule.AdvanceDay(c, ctx, rng).Campaign;
                advanced++;
            }
            return new SkipResult(c, advanced);
        }

        public static SkipResult SkipToDay(CampaignState c, DayContext ctx, IRandom rng, int targetDay)
        {
            if (c == null) throw new System.ArgumentNullException(nameof(c));
            if (ctx == null) throw new System.ArgumentNullException(nameof(ctx));
            if (rng == null) throw new System.ArgumentNullException(nameof(rng));
            if (targetDay < 1 || targetDay > TimeQuery.MaxDay)
                throw new VnRuntimeException($"targetDay out of range [1,{TimeQuery.MaxDay}]: {targetDay}");

            int advanced = 0;
            while (c.Run.Day < targetDay
                   && c.Run.Day < TimeQuery.MaxDay
                   && TimeQuery.GetPhase(c.Run.Day + 1) == DayPhase.Maintenance)
            {
                c = CampaignDayRule.AdvanceDay(c, ctx, rng).Campaign;
                advanced++;
            }
            return new SkipResult(c, advanced);
        }
    }

    public readonly struct SkipResult
    {
        public CampaignState Campaign { get; }
        public int DaysAdvanced { get; }
        public SkipResult(CampaignState campaign, int daysAdvanced)
        {
            Campaign = campaign;
            DaysAdvanced = daysAdvanced;
        }
    }
}
