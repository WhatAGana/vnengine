namespace VNEngine
{
    // AdvanceDay 결과. RegressPending일 때 Campaign은 처리 전(day 90) 캠페인이며 caller가 StartNewLoop.
    public readonly struct AdvanceResult
    {
        public CampaignState Campaign { get; }
        public DayPhase Phase { get; }
        public bool RegressPending { get; }
        public bool WaveResolved { get; }
        public WaveOutcome Wave { get; } // WaveResolved==true 일 때만 유효

        public AdvanceResult(CampaignState campaign, DayPhase phase, bool regressPending,
                             bool waveResolved, WaveOutcome wave)
        {
            Campaign = campaign;
            Phase = phase;
            RegressPending = regressPending;
            WaveResolved = waveResolved;
            Wave = wave;
        }
    }
}
