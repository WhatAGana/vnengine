using System;

namespace VNEngine
{
    // 감옥 방출 결과(값 타입, 불변). 방출된 포로 수와 획득 인과율, 갱신된 Run/Meta 스냅샷을 함께 반환.
    public readonly struct PrisonReleaseResult
    {
        public RunState Run { get; }
        public MetaState Meta { get; }
        public int Released { get; }
        public int KarmaGained { get; }

        public PrisonReleaseResult(RunState run, MetaState meta, int released, int karmaGained)
        {
            Run = run;
            Meta = meta;
            Released = released;
            KarmaGained = karmaGained;
        }
    }

    // 감옥 방출 규칙(순수·결정론). 07-B에서 미룬 항목 마감: 잡아둔 포로 전원을 풀어주고
    // 인과율(KarmaBank)로 환전한다. 계수는 잠정 튜닝값(플레이테스트 실측 조정 대상).
    public static class PrisonRule
    {
        public const int ReleaseKarmaPerCaptive = 3; // 잠정 튜닝값

        public static PrisonReleaseResult ReleaseAll(RunState run, MetaState meta)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (meta == null) throw new ArgumentNullException(nameof(meta));

            int released = run.Captives.Count;
            int karmaGained = released * ReleaseKarmaPerCaptive;

            var newRun = new RunState(run.Day, run.Resources, Array.Empty<Captive>(), run.PullsThisLoop);
            var newMeta = new MetaState(meta.LoopCount, meta.Heroes, meta.Inn, meta.KarmaBank + karmaGained, meta.DungeonLevel);

            return new PrisonReleaseResult(newRun, newMeta, released, karmaGained);
        }
    }
}
