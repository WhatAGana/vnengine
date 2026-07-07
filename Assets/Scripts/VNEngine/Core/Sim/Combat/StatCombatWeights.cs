using System;
using System.Collections.Generic;

namespace VNEngine
{
    // 주인공 8스탯 -> 전투 역할 파생 테이블(밸런스 핵심). ⚠️ ThreatBase 가중치와 절대 섞지 말 것 —
    // 이 파일은 오직 "스탯 -> CombatRole" 만 다룬다. 부재 스탯은 TryGet 으로 0 기여(하드코딩 분기 금지).
    public sealed class StatCombatWeights
    {
        private readonly Dictionary<CombatRole, Dictionary<StatId, int>> _weights;

        public StatCombatWeights(IReadOnlyDictionary<CombatRole, Dictionary<StatId, int>> weights)
        {
            if (weights == null) throw new ArgumentNullException(nameof(weights));

            _weights = new Dictionary<CombatRole, Dictionary<StatId, int>>(weights.Count);
            foreach (var kv in weights)
            {
                if (kv.Value == null) throw new ArgumentException($"Weight map for role {kv.Key} must not be null", nameof(weights));
                _weights[kv.Key] = new Dictionary<StatId, int>(kv.Value); // 방어적 복사
            }
        }

        public HeroCombatProfile Derive(HeroStats hero)
        {
            if (hero == null) throw new ArgumentNullException(nameof(hero));

            return new HeroCombatProfile(
                physicalAttack: DeriveRole(CombatRole.PhysicalAttack, hero),
                magicAttack: DeriveRole(CombatRole.MagicAttack, hero),
                defense: DeriveRole(CombatRole.Defense, hero),
                hitRating: DeriveRole(CombatRole.HitRating, hero),
                critRating: DeriveRole(CombatRole.CritRating, hero),
                evasion: DeriveRole(CombatRole.Evasion, hero),
                health: DeriveRole(CombatRole.Health, hero),
                skillResource: DeriveRole(CombatRole.SkillResource, hero),
                combatPower: DeriveRole(CombatRole.CombatPower, hero));
        }

        private int DeriveRole(CombatRole role, HeroStats hero)
        {
            if (!_weights.TryGetValue(role, out var statWeights)) return 0;

            var sum = 0;
            foreach (var kv in statWeights)
            {
                hero.TryGet(kv.Key, out var statValue); // 부재 스탯 = 0 기여
                sum += statValue * kv.Value / 100;
            }
            return sum;
        }

        // 1편 초기추정(튜닝대상) 스탯->전투역할 가중치.
        public static StatCombatWeights Default() => new StatCombatWeights(
            new Dictionary<CombatRole, Dictionary<StatId, int>>
            {
                [CombatRole.PhysicalAttack] = new Dictionary<StatId, int>
                {
                    { StatIds.STR, 100 },
                },
                [CombatRole.MagicAttack] = new Dictionary<StatId, int>
                {
                    { StatIds.INT, 100 },
                },
                [CombatRole.Defense] = new Dictionary<StatId, int>
                {
                    { StatIds.DEF, 100 },
                },
                [CombatRole.HitRating] = new Dictionary<StatId, int>
                {
                    { StatIds.DEX, 100 },
                    { StatIds.LUK, 50 },
                },
                [CombatRole.CritRating] = new Dictionary<StatId, int>
                {
                    { StatIds.DEX, 50 },
                    { StatIds.LUK, 100 },
                },
                [CombatRole.Evasion] = new Dictionary<StatId, int>
                {
                    { StatIds.AGI, 100 },
                },
                [CombatRole.Health] = new Dictionary<StatId, int>
                {
                    { StatIds.HP, 100 },
                },
                [CombatRole.SkillResource] = new Dictionary<StatId, int>
                {
                    { StatIds.MP, 100 },
                },
                [CombatRole.CombatPower] = new Dictionary<StatId, int>
                {
                    { StatIds.STR, 30 },
                    { StatIds.INT, 30 },
                    { StatIds.DEX, 20 },
                    { StatIds.AGI, 20 },
                    { StatIds.DEF, 30 },
                    { StatIds.HP, 10 },
                    { StatIds.MP, 10 },
                    { StatIds.LUK, 10 },
                },
            });
    }
}
