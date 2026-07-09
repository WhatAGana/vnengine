namespace VNEngine
{
    // 가챠 비용 — 07 §6.3. pullsThisLoop는 RunState(런) 소속 → 회차 리셋. 초기 추정.
    public static class GachaRule
    {
        public const int GachaBaseCost = 2;

        public static int GachaCost(int pullsThisLoop)
        {
            if (pullsThisLoop < 0)
                throw new VnRuntimeException($"pullsThisLoop must be >= 0: {pullsThisLoop}");
            return GachaBaseCost + pullsThisLoop / 3;
        }
    }
}
