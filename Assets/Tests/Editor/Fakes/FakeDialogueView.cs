using System.Collections.Generic;

namespace VNEngine.Tests
{
    public sealed class FakeDialogueView : IDialogueView
    {
        public sealed class Shown { public string Speaker; public string Color; public string Text; }

        public readonly List<Shown> Lines = new List<Shown>();
        public readonly List<List<string>> ChoiceSets = new List<List<string>>();

        private readonly Queue<int> _answers;
        private bool _hasChoice;
        private int _chosen;

        public FakeDialogueView(params int[] answers) { _answers = new Queue<int>(answers); }

        public void ShowLine(string speakerName, string colorHex, string text)
            => Lines.Add(new Shown { Speaker = speakerName, Color = colorHex, Text = text });

        public bool IsLineComplete => true;

        public void ShowChoices(IReadOnlyList<string> labels)
        {
            ChoiceSets.Add(new List<string>(labels));
            _chosen = _answers.Count > 0 ? _answers.Dequeue() : 0;
            _hasChoice = true;
        }

        public bool HasChoice => _hasChoice;
        public int ChosenIndex => _chosen;
        public void ClearChoices() => _hasChoice = false;
    }
}
