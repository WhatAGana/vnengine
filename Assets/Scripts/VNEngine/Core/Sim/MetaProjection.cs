using System.Collections.Generic;

namespace VNEngine
{
    // 커널 → VN 단방향·읽기전용 투영. Core 는 테마 중립이라 변수명은 주입받는다.
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

        // 주인공 개별 스탯을 주입된 변수명으로 읽기전용 투영. 없는 스탯은 0.
        public static void ProjectHeroStats(HeroStats heroes, GameState state, IReadOnlyDictionary<StatId, string> statVars)
        {
            if (heroes == null) throw new System.ArgumentNullException(nameof(heroes));
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (statVars == null) throw new System.ArgumentNullException(nameof(statVars));

            foreach (var kv in statVars)
            {
                if (string.IsNullOrEmpty(kv.Value))
                    throw new System.ArgumentException("stat variable name required", nameof(statVars));
                int val = heroes.TryGet(kv.Key, out var v) ? v : 0;
                state.Set(kv.Value, VnValue.Int(val));
            }
        }

        // 총스탯합(전투력 근사)을 변수로 투영.
        public static void ProjectHeroTotal(HeroStats heroes, GameState state, string totalVar)
        {
            if (heroes == null) throw new System.ArgumentNullException(nameof(heroes));
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(totalVar))
                throw new System.ArgumentException("totalVar required", nameof(totalVar));

            int sum = 0;
            foreach (var kv in heroes.Values) sum += kv.Value;
            state.Set(totalVar, VnValue.Int(sum));
        }

        // 인과율 잔고를 주입된 변수명으로 읽기전용 투영.
        public static void ProjectKarmaBank(MetaState meta, GameState state, string karmaVar)
        {
            if (meta == null) throw new System.ArgumentNullException(nameof(meta));
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (string.IsNullOrEmpty(karmaVar))
                throw new System.ArgumentException("karmaVar required", nameof(karmaVar));

            state.Set(karmaVar, VnValue.Int(meta.KarmaBank));
        }

        // 런 자원(자원id → 변수명)을 주입된 변수명으로 읽기전용 투영. 없는 자원은 0.
        public static void ProjectResources(RunState run, GameState state, IReadOnlyDictionary<string, string> resourceVars)
        {
            if (run == null) throw new System.ArgumentNullException(nameof(run));
            if (state == null) throw new System.ArgumentNullException(nameof(state));
            if (resourceVars == null) throw new System.ArgumentNullException(nameof(resourceVars));

            foreach (var kv in resourceVars)
            {
                if (string.IsNullOrEmpty(kv.Value))
                    throw new System.ArgumentException("resource variable name required", nameof(resourceVars));
                int val = run.Resources.TryGetValue(kv.Key, out var v) ? v : 0;
                state.Set(kv.Value, VnValue.Int(val));
            }
        }
    }
}
