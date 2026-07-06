namespace VNEngine
{
    public sealed class ResourceDef
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int StartValue { get; }

        public ResourceDef(string id, string displayName, int startValue)
        {
            Id = id;
            DisplayName = displayName;
            StartValue = startValue;
        }
    }
}
