namespace VNEngine
{
    // 정수 전용 수학 유틸(부동소수 금지 원칙). 현재 필요최소 — Isqrt(내림 정수제곱근)만.
    public static class IntMath
    {
        // floor(sqrt(n)). n>=0. 이진탐색 — Math.Sqrt/부동소수 미사용(결정론), mid*mid 대신 mid<=n/mid 로 오버플로 회피.
        public static int Isqrt(int n)
        {
            if (n < 0) throw new System.ArgumentOutOfRangeException(nameof(n), "n must be non-negative");
            if (n < 2) return n;
            int lo = 1, hi = n, ans = 1;
            while (lo <= hi)
            {
                int mid = lo + (hi - lo) / 2;
                if (mid <= n / mid) { ans = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return ans;
        }
    }
}
