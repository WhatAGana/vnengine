using System.Collections.Generic;

namespace VNEngine.Tests
{
    public sealed class FakeStageView : IStageView
    {
        public readonly List<string> Log = new List<string>();
        public void SetBackground(string name) => Log.Add($"bg:{name}");
        public void ShowCharacter(string name, string position) => Log.Add($"show:{name}:{position}");
        public void HideCharacter(string name) => Log.Add($"hide:{name}");
    }
}
