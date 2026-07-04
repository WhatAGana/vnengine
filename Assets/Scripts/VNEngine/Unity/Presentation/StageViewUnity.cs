using System.Collections.Generic;
using UnityEngine;

namespace VNEngine.Unity
{
    public class StageViewUnity : MonoBehaviour, IStageView
    {
        [System.Serializable] public class CharacterEntry { public string name; public Sprite sprite; }
        [System.Serializable] public class BackgroundEntry { public string name; public Sprite sprite; }

        [Header("Slots (world-space empty GameObjects)")]
        public Transform leftSlot;
        public Transform centerSlot;
        public Transform rightSlot;

        [Header("Background")]
        public SpriteRenderer background;
        public List<BackgroundEntry> backgrounds = new List<BackgroundEntry>();

        [Header("Characters")]
        public List<CharacterEntry> characters = new List<CharacterEntry>();
        public int characterSortingOrder = 5;
        public float characterScale = 0.35f;

        private readonly Dictionary<string, GameObject> _active = new Dictionary<string, GameObject>();
        private string _currentBackground;

        public void SetBackground(string name)
        {
            _currentBackground = name;
            var entry = backgrounds.Find(b => b != null && b.name == name);
            if (entry != null && entry.sprite != null && background != null)
                background.sprite = entry.sprite; // optional swap; full transitions are P1
        }

        public void ShowCharacter(string name, string position)
        {
            var entry = characters.Find(c => c != null && c.name == name);
            if (entry == null) { Debug.LogWarning($"[StageView] character '{name}' not registered"); return; }
            if (entry.sprite == null) { Debug.LogWarning($"[StageView] character '{name}' has no sprite"); return; }

            Transform slot = GetSlot(position);
            if (slot == null) { Debug.LogWarning($"[StageView] slot for '{position}' not assigned"); return; }

            // Evict any other character currently standing in this slot.
            var toRemove = new List<string>();
            foreach (var kv in _active)
                if (kv.Key != name && kv.Value != null && kv.Value.transform.parent == slot)
                    toRemove.Add(kv.Key);
            foreach (var n in toRemove) { if (_active[n] != null) Destroy(_active[n]); _active.Remove(n); }

            if (_active.TryGetValue(name, out var go) && go != null)
            {
                go.transform.SetParent(slot, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localScale = Vector3.one * characterScale;
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr != null) { sr.sprite = entry.sprite; sr.sortingOrder = characterSortingOrder; }
            }
            else
            {
                var newGo = new GameObject($"Char_{name}");
                newGo.transform.SetParent(slot, false);
                newGo.transform.localPosition = Vector3.zero;
                newGo.transform.localScale = Vector3.one * characterScale;
                var sr = newGo.AddComponent<SpriteRenderer>();
                sr.sprite = entry.sprite;
                sr.sortingOrder = characterSortingOrder;
                _active[name] = newGo;
            }
        }

        public void HideCharacter(string name)
        {
            if (_active.TryGetValue(name, out var go))
            {
                if (go != null) Destroy(go);
                _active.Remove(name);
            }
        }

        private Transform GetSlot(string position)
        {
            switch ((position ?? "").ToLowerInvariant())
            {
                case "left": return leftSlot;
                case "center": return centerSlot;
                case "right": return rightSlot;
                default: Debug.LogWarning($"[StageView] unknown position '{position}'"); return null;
            }
        }
    }
}
