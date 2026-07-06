namespace VNEngine
{
    // 커널 → VN 단방향·읽기전용 투영. Core는 테마 중립이라 변수명은 주입받는다.
    public static class MetaProjection
    {
        public static void Project(MetaState meta, GameState state, string loopCountVar)
        {
            if (meta == null) throw new System.ArgumentNullException(nameof(meta));
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(loopCountVar))
                throw new System.ArgumentException("loopCountVar required", nameof(loopCountVar));

            state.Set(loopCountVar, VnValue.Int(meta.LoopCount));
        }
    }
}
