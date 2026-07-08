using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 웨이브 전투 해결(순수함수). 입력(RunState/HeroStats/rooms/wave)은 전부 불변으로 취급하며
    // HP 감소는 로컬 변수로만 추적한다 — 원본 Attacker/RoomNode/WaveDef 스냅샷은 절대 고치지 않는다.
    // 같은 시드+같은 배치+같은 웨이브 -> 같은 CombatResult(rng 호출 순서 고정, 방은 선형 순회).
    public static class CombatResolver
    {
        // 주인공은 무병종 공격자 취급(ClassMatchup 등록 대상 아님) -> 상성배수 항상 중립(100).
        private const int HeroMatchupPct = ClassMatchup.Neutral;

        // 명중판정 롤 범위(HitParams.HitRollMax). 방어몹 요격은 DamageFormula.Apply(결정론, rng 미사용)만
        // 쓰므로 이 상수는 주인공의 코어앞1칸 요격에만 쓰인다.
        // 퍼센트 스케일 롤 관례(0~100) — 추후 크리티컬 배수와 함께 전투 상수 데이터소스로 편입될 후보.
        private const int HeroHitRollMax = 100;

        // 이 슬라이스에는 "치명타 피해량" 데이터소스가 아직 없다(07-B/C 예산/밸런싱 비스코프) ->
        // 100(=배수 없음)으로 취급(하드코딩 분기 아님 — 미도입 데이터의 중립값). 치명타 발생 여부는
        // CritRating으로 이미 모델링되어 있으므로 크리티컬 "판정"은 중립이 아니지만 "배수"만 중립이다.
        // DamageFormula.Apply가 크리티컬 배수 곱셈 전에 이미 Math.Max(1, ...)로 명중타를 바닥처리하므로
        // 이 값이 100이어도(>=100이기만 하면) "명중한 타격이 0으로 굴러떨어지는" 문제는 발생하지 않는다.
        private const int HeroCritMultiplierPct = 100;

        // 이 슬라이스에는 "배치 몹 레벨" 개념이 아직 없다(07-B 예산/배치 로직 비스코프) ->
        // ThreatFormula.Compute 의 avgPlacedMonsterLevel 항은 0으로 취급(하드코딩 분기 아님 — 미도입 데이터의 중립값).
        private const int AvgPlacedMonsterLevelUnmodeled = 0;

        public static CombatResult ResolveWave(
            RunState run,
            WaveDef wave,
            RoomGraph graph,
            HeroStats hero,
            StatCombatWeights statWeights,
            ThreatWeights threatWeights,
            IReadOnlyList<UnitClassDef> classCatalog,
            ClassMatchup matchup,
            CaptureRule captureRule,
            int dungeonLevel,
            int loopCount,
            IRandom rng)
        {
            if (run == null) throw new ArgumentNullException(nameof(run)); // run: 브리프 시그니처가 요구해 보존됨 — 이 슬라이스 로직에서는 소비되지 않음.
            if (wave == null) throw new ArgumentNullException(nameof(wave));
            if (graph == null) throw new ArgumentNullException(nameof(graph));
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (statWeights == null) throw new ArgumentNullException(nameof(statWeights));
            if (threatWeights == null) throw new ArgumentNullException(nameof(threatWeights));
            if (classCatalog == null) throw new ArgumentNullException(nameof(classCatalog));
            if (matchup == null) throw new ArgumentNullException(nameof(matchup));
            if (captureRule == null) throw new ArgumentNullException(nameof(captureRule));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            var classLookup = new Dictionary<UnitClassId, UnitClassDef>(classCatalog.Count);
            foreach (var cls in classCatalog)
            {
                if (cls == null) throw new ArgumentException("classCatalog must not contain null entries", nameof(classCatalog));
                classLookup[cls.Id] = cls;
            }

            var heroProfile = statWeights.Derive(hero);
            var heroPower = ThreatFormula.HeroPowerOf(statWeights, hero);
            var threatBase = ThreatFormula.Compute(threatWeights, heroPower, loopCount, AvgPlacedMonsterLevelUnmodeled, dungeonLevel);

            var heroHitParams = new HitParams
            {
                HitRating = heroProfile.HitRating,
                Evasion = 0, // 침입자(Attacker)에는 회피 스탯이 없음 — 주인공 공격에는 항상 0.
                CritRating = heroProfile.CritRating,
                CritMultiplierPct = HeroCritMultiplierPct,
                HitRollMax = HeroHitRollMax,
            };

            var killed = new List<Attacker>();
            var captured = new List<Attacker>();
            var coreHit = false;

            // 웨이브 항목 순서 -> 항목 내 수량 순서로 침입자를 하나씩 생성 즉시 해결(생성-해결 인터리브,
            // rng 호출 순서 고정: 생성(3콜) -> [방 통과: rng 미사용] -> 주인공 요격(1~2콜)).
            foreach (var entry in wave.Intruders)
            {
                if (!classLookup.TryGetValue(entry.ClassId, out var cls))
                    throw new VnRuntimeException($"Unknown UnitClassId in wave: {entry.ClassId}");

                for (var i = 0; i < entry.Count; i++)
                {
                    var attacker = AttackerFactory.Create(cls, threatBase, rng);
                    var outcome = ResolveIntruder(attacker, graph.Path, heroProfile, heroHitParams, matchup, captureRule, rng);

                    switch (outcome.Result)
                    {
                        case IntruderOutcome.Killed:
                            killed.Add(outcome.Final);
                            break;
                        case IntruderOutcome.Captured:
                            captured.Add(outcome.Final);
                            break;
                        case IntruderOutcome.ReachedCore:
                            coreHit = true;
                            break;
                    }
                }
            }

            return new CombatResult(coreHit, killed, captured);
        }

        private enum IntruderOutcome
        {
            Killed,
            Captured,
            ReachedCore,
        }

        private readonly struct IntruderResolution
        {
            public readonly IntruderOutcome Result;
            public readonly Attacker Final;

            public IntruderResolution(IntruderOutcome result, Attacker final)
            {
                Result = result;
                Final = final;
            }
        }

        // 침입자 1체가 방1 -> ... -> 코어앞1칸(주인공)까지 선형 통과하는 과정을 해결한다.
        // HP 감소는 로컬 변수(hp)로만 추적 — 인자로 받은 attacker(및 rooms) 는 절대 변경하지 않는다.
        private static IntruderResolution ResolveIntruder(
            Attacker attacker,
            IReadOnlyList<RoomNode> rooms,
            HeroCombatProfile heroProfile,
            HitParams heroHitParams,
            ClassMatchup matchup,
            CaptureRule captureRule,
            IRandom rng)
        {
            var hp = attacker.Hp;

            foreach (var room in rooms)
            {
                foreach (var defender in room.Defenders)
                {
                    var pct = matchup.Multiplier(defender.ClassId, attacker.ClassId);
                    var dmg = DamageFormula.Apply(defender.Atk, attacker.Def, pct);
                    hp -= dmg;

                    if (hp <= 0)
                    {
                        var trigger = CaptureTrigger.None;
                        if (room.HasTrap) trigger |= CaptureTrigger.Trap;
                        if (defender.IsCapturingMonster) trigger |= CaptureTrigger.CapturingMonster;
                        var captured = captureRule.ShouldCapture(attacker.CanBeCaptured, new CaptureContext(trigger));
                        return new IntruderResolution(
                            captured ? IntruderOutcome.Captured : IntruderOutcome.Killed,
                            attacker);
                    }
                }
            }

            // 코어앞1칸: 주인공 단독 요격(1회 타격만 — 생존시 코어 도달).
            // 주인공은 이 슬라이스에서 순수 요격자(interceptor)로만 모델링됨 — 주인공 HP/Defense 소모는
            // 의도적으로 미구현(비스코프)이며, 침입자 반격으로 인한 주인공 피해는 이 함수에 없다.
            var heroDmg = DamageFormula.Resolve(heroProfile.PhysicalAttack, attacker.Def, HeroMatchupPct, heroHitParams, rng);
            hp -= heroDmg;

            if (hp <= 0)
            {
                // 주인공 코어앞 제압(HeroSubdue) — §4.5 "심부유인+주인공 제압" 트리거. 함정과 무관하게
                // 포획가능 침입자를 주인공이 직접 처치하면 포획으로 이어질 수 있다(CaptureRule에 위임).
                var captured = captureRule.ShouldCapture(attacker.CanBeCaptured, new CaptureContext(CaptureTrigger.HeroSubdue));
                return new IntruderResolution(captured ? IntruderOutcome.Captured : IntruderOutcome.Killed, attacker);
            }

            return new IntruderResolution(IntruderOutcome.ReachedCore, attacker);
        }
    }
}
