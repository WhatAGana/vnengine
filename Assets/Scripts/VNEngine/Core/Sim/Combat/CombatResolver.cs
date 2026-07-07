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
        private const int HeroHitRollMax = 100;

        // 치명타는 항상 보너스(>=100) -> 명중한 타격이 0으로 굴러떨어지는 일이 없도록 보장.
        private const int HeroCritMultiplierPct = 200;

        // 이 슬라이스에는 "배치 몹 레벨" 개념이 아직 없다(07-B 예산/배치 로직 비스코프) ->
        // ThreatFormula.Compute 의 avgPlacedMonsterLevel 항은 0으로 취급(하드코딩 분기 아님 — 미도입 데이터의 중립값).
        private const int AvgPlacedMonsterLevelUnmodeled = 0;

        public static CombatResult ResolveWave(
            RunState run,
            WaveDef wave,
            IReadOnlyList<RoomNode> rooms,
            HeroStats hero,
            StatCombatWeights statWeights,
            ThreatWeights threatWeights,
            IReadOnlyList<UnitClassDef> classCatalog,
            ClassMatchup matchup,
            int dungeonLevel,
            int loopCount,
            IRandom rng)
        {
            if (run == null) throw new ArgumentNullException(nameof(run));
            if (wave == null) throw new ArgumentNullException(nameof(wave));
            if (rooms == null) throw new ArgumentNullException(nameof(rooms));
            if (hero == null) throw new ArgumentNullException(nameof(hero));
            if (statWeights == null) throw new ArgumentNullException(nameof(statWeights));
            if (threatWeights == null) throw new ArgumentNullException(nameof(threatWeights));
            if (classCatalog == null) throw new ArgumentNullException(nameof(classCatalog));
            if (matchup == null) throw new ArgumentNullException(nameof(matchup));
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
                    var outcome = ResolveIntruder(attacker, rooms, heroProfile, heroHitParams, matchup, rng);

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
                        var captured = attacker.CanBeCaptured && room.HasTrap;
                        return new IntruderResolution(
                            captured ? IntruderOutcome.Captured : IntruderOutcome.Killed,
                            attacker);
                    }
                }
            }

            // 코어앞1칸: 주인공 단독 요격(1회 타격만 — 생존시 코어 도달).
            var heroDmg = DamageFormula.Resolve(heroProfile.PhysicalAttack, attacker.Def, HeroMatchupPct, heroHitParams, rng);
            hp -= heroDmg;

            if (hp <= 0)
                return new IntruderResolution(IntruderOutcome.Killed, attacker);

            return new IntruderResolution(IntruderOutcome.ReachedCore, attacker);
        }
    }
}
