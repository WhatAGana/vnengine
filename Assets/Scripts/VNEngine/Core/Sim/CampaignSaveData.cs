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

    // Run.Captives 원소 1건 평면화. resetPolicy는 ResetPolicy enum을 int로(JsonUtility enum 직렬화 관례).
    [System.Serializable]
    public sealed class CaptiveEntry
    {
        public string classId;   // UnitClassId.Value
        public bool isNamed;
        public int resetPolicy;  // (int)ResetPolicy
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

        // Meta.KarmaBank / Run.PullsThisLoop (additive: 구세이브는 누락 int→JsonUtility 기본 0). 버전 불변.
        public int karmaBank;
        public int pullsThisLoop;

        // Run.Captives 평면화(additive: 구세이브는 누락 리스트→JsonUtility 기본 null→빈 리스트로 복원). 버전 불변.
        public List<CaptiveEntry> captives = new List<CaptiveEntry>();
    }
}
