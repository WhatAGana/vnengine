using System.Collections.Generic;

namespace VNEngine
{
    // 주인공 스탯 상태. 하드코딩 필드 없이 Dictionary<StatId,int> 로 보관(데이터 주도).
    // 불변: 모든 변경은 새 HeroStats 반환. 생성자는 방어적 복사(입력 딕셔너리와 참조 분리).
    public sealed class HeroStats
    {
        private readonly Dictionary<StatId, int> _values;

        public static readonly HeroStats Empty = new HeroStats(new Dictionary<StatId, int>());

        public HeroStats(IReadOnlyDictionary<StatId, int> values)
        {
            if (values == null) throw new System.ArgumentNullException(nameof(values));
            _values = new Dictionary<StatId, int>(values.Count);
            foreach (var kv in values) _values[kv.Key] = kv.Value; // 방어적 복사
        }

        public IReadOnlyDictionary<StatId, int> Values => _values;

        public bool Has(StatId id) => _values.ContainsKey(id);

        public bool TryGet(StatId id, out int value) => _values.TryGetValue(id, out value);

        public int Get(StatId id)
        {
            if (!_values.TryGetValue(id, out var v))
                throw new VnRuntimeException($"Unknown stat: {id}");
            return v;
        }

        public HeroStats WithStat(StatId id, int value)
        {
            var copy = new Dictionary<StatId, int>(_values);
            copy[id] = value;
            return new HeroStats(copy);
        }

        // StatDef 목록의 StartValue 로 초기화(데이터 주도 시딩).
        public static HeroStats FromDefs(IEnumerable<StatDef> defs)
        {
            if (defs == null) throw new System.ArgumentNullException(nameof(defs));
            var dict = new Dictionary<StatId, int>();
            foreach (var d in defs) dict[d.Id] = d.StartValue;
            return new HeroStats(dict);
        }
    }
}
