using System.Collections.Generic;

namespace VNEngine
{
    [System.Serializable]
    public sealed class ResEntry
    {
        public string id;
        public int value;
    }

    // JsonUtility 호환: 딕셔너리 대신 리스트, 원시 타입만.
    [System.Serializable]
    public sealed class CampaignSaveData
    {
        public const int CampaignSaveVersion = 1;

        public int version;
        public int loopCount;                 // Meta.LoopCount
        public int day;                       // Run.Day
        public List<ResEntry> resources = new List<ResEntry>(); // Run.Resources 평면화
    }
}
