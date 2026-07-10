using System;

namespace VNEngine
{
    // 함정 데미지 공식(순수, 정수). Damage = Base + Level*PerLevel.
    public static class TrapRule
    {
        public static int Damage(TrapDef def, int level)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (level < 0) throw new ArgumentException("level must be non-negative", nameof(level));
            return def.Base + level * def.PerLevel;
        }
    }
}
