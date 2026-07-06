using System.IO;
using UnityEngine;

namespace VNEngine.Unity
{
    // CampaignSaveData 디스크 영속화. 기존 SaveSystem과 독립 파일(campaign_N.json).
    // 모바일 안전: Application.persistentDataPath 만 사용.
    public static class CampaignSaveSystem
    {
        private static string Dir => Path.Combine(Application.persistentDataPath, "saves");
        public static string SlotPath(int slot) => Path.Combine(Dir, $"campaign_{slot}.json");

        public static void Write(int slot, CampaignSaveData data)
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(SlotPath(slot), JsonUtility.ToJson(data, true));
        }

        public static CampaignSaveData Read(int slot)
        {
            string path = SlotPath(slot);
            if (!File.Exists(path)) return null;
            try { return JsonUtility.FromJson<CampaignSaveData>(File.ReadAllText(path)); }
            catch (System.Exception e)
            {
                Debug.LogError($"[CampaignSaveSystem] failed to read slot {slot}: {e.Message}");
                return null;
            }
        }

        public static bool Exists(int slot) => File.Exists(SlotPath(slot));

        public static void Delete(int slot)
        {
            string path = SlotPath(slot);
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
