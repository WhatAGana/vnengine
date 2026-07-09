using System;
using System.Collections.Generic;

namespace VNEngine
{
    // CombatResult.Captured -> RunState.Captives 누적(순수함수). 입력 run/result는 절대 고치지 않는다.
    public static class CaptiveLedger
    {
        public static RunState Accumulate(RunState run, CombatResult result)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (result == null) throw new ArgumentNullException(nameof(result));

            var merged = new List<Captive>(run.Captives);
            foreach (var c in result.Captured)
            {
                merged.Add(new Captive(c.ClassId, c.IsNamed, ResetPolicy.Unspecified));
            }

            return new RunState(run.Day, run.Resources, merged, run.PullsThisLoop);
        }
    }
}
