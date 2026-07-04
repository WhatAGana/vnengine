using System.Collections.Generic;

namespace VNEngine
{
    public interface IDialogueView
    {
        void ShowLine(string speakerName, string colorHex, string text);
        bool IsLineComplete { get; }
        void ShowChoices(IReadOnlyList<string> labels);
        bool HasChoice { get; }
        int ChosenIndex { get; }
        void ClearChoices();
    }
}
