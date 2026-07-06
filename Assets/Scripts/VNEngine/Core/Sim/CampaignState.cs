namespace VNEngine
{
    public sealed class CampaignState
    {
        public MetaState Meta { get; }
        public RunState Run { get; }

        public CampaignState(MetaState meta, RunState run)
        {
            Meta = meta;
            Run = run;
        }
    }
}
