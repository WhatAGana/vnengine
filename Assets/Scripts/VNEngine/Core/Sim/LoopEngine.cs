namespace VNEngine
{
    // 캠페인 층 파사드. TurnEngine(회차 내 규칙)을 소유하고
    // 모든 전이는 새 CampaignState 를 반환한다(불변).
    public sealed class LoopEngine
    {
        private readonly TurnEngine _turnEngine;

        public LoopEngine(TurnEngine turnEngine)
        {
            _turnEngine = turnEngine ?? throw new System.ArgumentNullException(nameof(turnEngine));
        }

        // 캠페인 시작: LoopCount=1, Run=초기 Run(Day=1, 자원 StartValue).
        public CampaignState CreateInitialCampaign()
        {
            return new CampaignState(new MetaState(1), _turnEngine.CreateInitialState());
        }

        // 회차 내 커맨드: Run 만 진행(Day+1, 델타 적용), Meta 는 그대로 통과.
        public CampaignState ExecuteCommand(CampaignState campaign, string commandId)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            var newRun = _turnEngine.ExecuteCommand(campaign.Run, commandId);
            return new CampaignState(campaign.Meta, newRun);
        }

        // 회차 전이(최소): LoopCount+1, Run 은 새 초기 Run 으로 리셋.
        // 주인공 성장과 여관은 메타 — 회차를 넘어 유지(Heroes/Inn 캐리포워드). 계승·편지 등 '내용' 갱신은 이후 슬라이스(Regress).
        public CampaignState StartNewLoop(CampaignState campaign)
        {
            if (campaign == null) throw new System.ArgumentNullException(nameof(campaign));
            var newMeta = new MetaState(campaign.Meta.LoopCount + 1, campaign.Meta.Heroes, campaign.Meta.Inn, campaign.Meta.KarmaBank);
            return new CampaignState(newMeta, _turnEngine.CreateInitialState());
        }
    }
}
