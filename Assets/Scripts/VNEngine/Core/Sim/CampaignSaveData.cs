using System.Collections.Generic;

namespace VNEngine
{
    [System.Serializable]
    public sealed class ResEntry
    {
        public string id;
        public int value;
    }

    [System.Serializable]
    public sealed class StatEntry
    {
        public string id;      // StatId.Value 로 평면화
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
        public List<StatEntry> stats = new List<StatEntry>();   // Meta.Heroes 평면화(StatId→string). additive: 구세이브는 빈 리스트→빈 Heroes.

        // Meta.Inn 평면화(additive: 구세이브는 누락 int→JsonUtility 기본 0→Decor=0 게이트닫힘). 버전 불변.
        public int innStaff;
        public int innDecor;
        public int innMenuLevel;
    }
}
