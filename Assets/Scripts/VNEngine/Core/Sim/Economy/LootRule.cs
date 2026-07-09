namespace VNEngine
{
    // 약탈 골드/포획 인과율 — 07 §6.2 제곱근 완화. 계수는 초기 추정(sim_economy4.py).
    public static class LootRule
    {
        public const int GoldBase = 5;
        public const int GoldThreatK = 3;

        public static int LootGold(int threatBase, bool isCapture)
        {
            int raw = System.Math.Max(1, GoldBase + IntMath.Isqrt(threatBase) * GoldThreatK);
            return isCapture ? raw / 2 : raw;
        }

        // 포획 즉시 인과율(성장 재료) — loot와 병렬. 방면 인과율은 PrisonRule.
        public static int CaptureKarma(int threatBase) => IntMath.Isqrt(threatBase);
    }
}
