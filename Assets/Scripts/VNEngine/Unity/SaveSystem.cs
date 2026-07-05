using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VNEngine.Unity
{
    // Disk persistence for SaveData. Mobile-safe: only Application.persistentDataPath.
    public static class SaveSystem
    {
        private static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        public static string SlotPath(int slot) => Path.Combine(Dir, $"slot_{slot}.json");

        public static void Write(int slot, SaveData data)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
        }

        public static SaveData Read(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<SaveData>(File.ReadAllText(path)); }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveSystem] failed to read slot {slot}: {e.Message}");
                return null;
            }
        }

        public static bool Exists(int slot) => File.Exists(SlotPath(slot));

        public static void Delete(int slot)
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }

        public static IEnumerable<int> ListSlots()
        {
            if (!Directory.Exists(Dir)) yield break;
            foreach (var file in Directory.GetFiles(Dir, "slot_*.json"))
            {
                string name = Path.GetFileNameWithoutExtension(file); // slot_N
                if (int.TryParse(name.Substring("slot_".Length), out int n))
                    yield return n;
            }
        }

        public static bool IsCompatible(SaveData data, string programHash)
            => data != null && data.version == SaveData.SaveFormatVersion && data.programHash == programHash;
    }
}
