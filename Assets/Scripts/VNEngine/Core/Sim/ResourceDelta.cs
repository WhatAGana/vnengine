namespace VNEngine
{
    public readonly struct ResourceDelta
    {
        public string ResourceId { get; }
        public int Amount { get; }

        public ResourceDelta(string resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }
    }
}
