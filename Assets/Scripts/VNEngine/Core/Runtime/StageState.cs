using System.Collections.Generic;

namespace VNEngine
{
    // Logical stage state the interpreter maintains alongside firing view
    // calls, so a save can snapshot what is on screen and a load can rebuild it.
    public sealed class StageState
    {
        public string Background;
        // position ("left"/"center"/"right") -> character name
        public readonly Dictionary<string, string> Slots = new Dictionary<string, string>();

        public void SetBackground(string name) => Background = name;

        public void Show(string character, string position)
        {
            // A character occupies at most one slot: remove it from any slot
            // it currently stands in, then place it (evicting whoever is here).
            string current = null;
            foreach (var kv in Slots)
                if (kv.Value == character) { current = kv.Key; break; }
            if (current != null) Slots.Remove(current);
            Slots[position] = character;
        }

        public void Hide(string character)
        {
            string at = null;
            foreach (var kv in Slots)
                if (kv.Value == character) { at = kv.Key; break; }
            if (at != null) Slots.Remove(at);
        }
    }
}
