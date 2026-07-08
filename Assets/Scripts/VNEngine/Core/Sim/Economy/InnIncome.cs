namespace VNEngine
{
    // 여관 웨이브당 산출(불변 결과). Gold=부차, Karma=주 산출(인과율 엔진), Guests=산출 근거(검증/디버그).
    public readonly struct InnIncome
    {
        public int Gold { get; }
        public int Karma { get; }
        public int Guests { get; }

        public InnIncome(int gold, int karma, int guests) { Gold = gold; Karma = karma; Guests = guests; }

        public static readonly InnIncome Zero = new InnIncome(0, 0, 0);
    }
}
