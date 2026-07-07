using System;

namespace VNEngine
{
    // StatCombatWeights.Derive() 의 산출물. 역할별 정수 파생치.
    public readonly struct HeroCombatProfile
    {
        public int PhysicalAttack { get; }
        public int MagicAttack { get; }
        public int Defense { get; }
        public int HitRating { get; }
        public int CritRating { get; }
        public int Evasion { get; }
        public int Health { get; }
        public int SkillResource { get; }
        public int CombatPower { get; }

        public HeroCombatProfile(
            int physicalAttack,
            int magicAttack,
            int defense,
            int hitRating,
            int critRating,
            int evasion,
            int health,
            int skillResource,
            int combatPower)
        {
            PhysicalAttack = physicalAttack;
            MagicAttack = magicAttack;
            Defense = defense;
            HitRating = hitRating;
            CritRating = critRating;
            Evasion = evasion;
            Health = health;
            SkillResource = skillResource;
            CombatPower = combatPower;
        }

        public int Get(CombatRole role)
        {
            switch (role)
            {
                case CombatRole.PhysicalAttack: return PhysicalAttack;
                case CombatRole.MagicAttack: return MagicAttack;
                case CombatRole.Defense: return Defense;
                case CombatRole.HitRating: return HitRating;
                case CombatRole.CritRating: return CritRating;
                case CombatRole.Evasion: return Evasion;
                case CombatRole.Health: return Health;
                case CombatRole.SkillResource: return SkillResource;
                case CombatRole.CombatPower: return CombatPower;
                default: throw new VnRuntimeException($"Unknown combat role: {role}");
            }
        }
    }
}
