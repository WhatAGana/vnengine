using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CombatResolverTests
    {
        private static HeroStats Stats(params (StatId id, int val)[] entries)
        {
            var d = new Dictionary<StatId, int>();
            foreach (var e in entries) d[e.id] = e.val;
            return new HeroStats(d);
        }

        private static UnitClassDef ClassOf(string name, int hpPct, int atkPct, int defPct, bool canBeCaptured)
            => new UnitClassDef(new UnitClassId(name), name, hpPct, atkPct, defPct, canBeCaptured);

        private static ClassMatchup NeutralMatchup() => new ClassMatchup(new List<ClassMatchup.Entry>());

        // wHero/wLoop/wPlaced/wDungeon 전부 0 -> threatBase 는 baseOffset 으로만 고정(hero power 등
        // 다른 항에서 독립시켜 시나리오를 결정론적으로 통제하기 위한 테스트 전용 가중치).
        private static ThreatWeights FixedThreat(int baseOffset)
            => new ThreatWeights(wHero: 0, wLoop: 0, wPlaced: 0, wDungeon: 0, baseOffset: baseOffset);

        private static RunState EmptyRun() => new RunState(1, new Dictionary<string, int>());

        private static WaveDef OneWave(UnitClassId classId, int count)
            => new WaveDef(new List<WaveDef.Entry> { new WaveDef.Entry { ClassId = classId, Count = count } });

        // ---- 결정론 ----

        [Test]
        public void SameSeedSameInputsProduceIdenticalCombatResult()
        {
            var grunt = ClassOf("Grunt", hpPct: 100, atkPct: 100, defPct: 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 5);
            // 미약한 방어몹(거의 대미지 없음) + 명중 50% 주인공 -> 결과가 실제로 rng 에 좌우되는 시나리오.
            var room = new RoomNode(new List<Attacker> { new Attacker(UnitClassIds.Tank, hp: 999, atk: 3, def: 5, canBeCaptured: false) }, hasTrap: true);
            var rooms = new List<RoomNode> { room };
            var hero = Stats((StatIds.STR, 50), (StatIds.DEX, 50)); // PhysicalAttack=50, HitRating=50(경계)
            var statWeights = StatCombatWeights.Default();
            var threatWeights = FixedThreat(30);
            var matchup = NeutralMatchup();

            var a = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, statWeights, threatWeights, catalog, matchup, dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(777));
            var b = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, statWeights, threatWeights, catalog, matchup, dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(777));

            Assert.AreEqual(a.CoreHit, b.CoreHit);
            Assert.AreEqual(a.Killed.Count, b.Killed.Count);
            Assert.AreEqual(a.Captured.Count, b.Captured.Count);
            CollectionAssert.AreEqual(a.Killed, b.Killed);
            CollectionAssert.AreEqual(a.Captured, b.Captured);
        }

        // ---- 주인공 코어앞1칸 요격 ----

        [Test]
        public void EmptyRoomsStrongHeroKillsBeforeCore()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);
            var rooms = new List<RoomNode>(); // 방을 빈 채로
            // STR=1000 -> PhysicalAttack=1000(항상 치명적), DEX=100 -> HitRating=100(항상 명중, 회피0).
            var hero = Stats((StatIds.STR, 1000), (StatIds.DEX, 100));
            var threatWeights = FixedThreat(50); // 침입자 def/hp <= 55 로 고정 -> 1000 데미지에 항상 즉사.

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, StatCombatWeights.Default(), threatWeights, catalog, NeutralMatchup(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(1));

            Assert.IsFalse(result.CoreHit);
            Assert.AreEqual(1, result.Killed.Count);
            Assert.AreEqual(0, result.Captured.Count);
        }

        [Test]
        public void EmptyRoomsWeakHeroReachesCore()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);
            var rooms = new List<RoomNode>(); // 방을 빈 채로
            var hero = Stats(); // 스탯 전무 -> HitRating=0 -> 항상 빗나감(회피0, roll>=1이므로 threshold=0 초과).
            var threatWeights = FixedThreat(50);

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, StatCombatWeights.Default(), threatWeights, catalog, NeutralMatchup(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(1));

            Assert.IsTrue(result.CoreHit);
            Assert.AreEqual(0, result.Killed.Count);
            Assert.AreEqual(0, result.Captured.Count);
        }

        // ---- 포획 분기 ----

        [Test]
        public void CapturableIntruderFinishedInTrapRoomIsCaptured()
        {
            var capturable = ClassOf("Capturable", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, count: 1);
            // 압도적 화력의 방어몹(즉사 보장) + 함정방.
            var trapRoom = new RoomNode(new List<Attacker> { new Attacker(UnitClassIds.Tank, hp: 999, atk: 100000, def: 10, canBeCaptured: false) }, hasTrap: true);
            var rooms = new List<RoomNode> { trapRoom };
            var threatWeights = FixedThreat(10); // 침입자 hp/def <= 15 로 고정 -> 100000 데미지에 항상 즉사.

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), Stats(), StatCombatWeights.Default(), threatWeights, catalog, NeutralMatchup(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(2));

            Assert.AreEqual(1, result.Captured.Count);
            Assert.AreEqual(0, result.Killed.Count);
            Assert.IsFalse(result.CoreHit);
        }

        [Test]
        public void NonCapturableIntruderInTrapRoomIsStillKilled()
        {
            var nonCapturable = ClassOf("NonCapturable", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { nonCapturable };
            var wave = OneWave(nonCapturable.Id, count: 1);
            var trapRoom = new RoomNode(new List<Attacker> { new Attacker(UnitClassIds.Tank, hp: 999, atk: 100000, def: 10, canBeCaptured: false) }, hasTrap: true);
            var rooms = new List<RoomNode> { trapRoom };
            var threatWeights = FixedThreat(10);

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), Stats(), StatCombatWeights.Default(), threatWeights, catalog, NeutralMatchup(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(2));

            Assert.AreEqual(1, result.Killed.Count);
            Assert.AreEqual(0, result.Captured.Count);
        }

        // ---- 가중 전투력 경유(A1 권고): raw 스탯합이 아니라 HeroCombatProfile 파생치를 씀을 end-to-end 로 검증 ----

        [Test]
        public void StrOnlyIncreaseRaisesKillsButHpOnlyIncreaseDoesNot()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 5);
            var rooms = new List<RoomNode>(); // 방 없음 -> 전원 코어앞1칸(주인공)까지 도달.
            var threatWeights = FixedThreat(50); // 침입자 def/hp 항상 45..55 범위로 고정.
            var statWeights = StatCombatWeights.Default();
            // DEX=100 -> HitRating=100(항상 명중) 은 세 변형 모두 공통 고정 -> 명중여부는 STR/HP 에 의존하지 않음.
            var baseline = Stats((StatIds.DEX, 100), (StatIds.STR, 10)); // PhysicalAttack=10(제곱분기, hp를 절대 못 넘음)
            var strBoosted = Stats((StatIds.DEX, 100), (StatIds.STR, 1000)); // PhysicalAttack=1000(항상 즉사)
            var hpBoosted = Stats((StatIds.DEX, 100), (StatIds.STR, 10), (StatIds.HP, 5000)); // PhysicalAttack 은 STR 만 반영 -> baseline 과 동일해야 함.

            var baselineResult = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), baseline, statWeights, threatWeights, catalog, NeutralMatchup(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(9));
            var strResult = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), strBoosted, statWeights, threatWeights, catalog, NeutralMatchup(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(9));
            var hpResult = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hpBoosted, statWeights, threatWeights, catalog, NeutralMatchup(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(9));

            Assert.AreEqual(0, baselineResult.Killed.Count, "약한 STR 은 침입자를 절대 못 죽여야 함(제곱분기 데미지 << hp)");
            Assert.AreEqual(5, strResult.Killed.Count, "STR 대폭 상승 -> PhysicalAttack 상승 -> 전원 즉사");
            Assert.Greater(strResult.Killed.Count, baselineResult.Killed.Count);
            Assert.AreEqual(baselineResult.Killed.Count, hpResult.Killed.Count, "HP 만 올려도 처치력(PhysicalAttack)은 그대로여야 함 — raw 스탯합이 아니라 프로파일을 써야 함");
        }

        // ---- 코어 도달 ----

        [Test]
        public void NoDefendersAndPowerlessHeroReachesCore()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);
            var rooms = new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), hasTrap: false),
                new RoomNode(new List<Attacker>(), hasTrap: false),
            };
            var hero = Stats(); // 무력한 주인공: PhysicalAttack=0, HitRating=0(항상 빗나감).
            var threatWeights = FixedThreat(50);

            var result = CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, StatCombatWeights.Default(), threatWeights, catalog, NeutralMatchup(), dungeonLevel: 1, loopCount: 1, rng: new SeededRandom(3));

            Assert.IsTrue(result.CoreHit);
            Assert.AreEqual(0, result.Killed.Count);
            Assert.AreEqual(0, result.Captured.Count);
        }

        // ---- 인자 검증 ----

        [Test]
        public void NullArgumentsThrowArgumentNullException()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);
            var rooms = new List<RoomNode>();
            var hero = Stats();
            var statWeights = StatCombatWeights.Default();
            var threatWeights = FixedThreat(10);
            var matchup = NeutralMatchup();

            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(null, wave, RoomGraph.Linear(rooms), hero, statWeights, threatWeights, catalog, matchup, 1, 1, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(EmptyRun(), null, RoomGraph.Linear(rooms), hero, statWeights, threatWeights, catalog, matchup, 1, 1, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(EmptyRun(), wave, null, hero, statWeights, threatWeights, catalog, matchup, 1, 1, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), null, statWeights, threatWeights, catalog, matchup, 1, 1, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, null, threatWeights, catalog, matchup, 1, 1, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, statWeights, null, catalog, matchup, 1, 1, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, statWeights, threatWeights, null, matchup, 1, 1, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, statWeights, threatWeights, catalog, null, 1, 1, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, statWeights, threatWeights, catalog, matchup, 1, 1, null));
        }

        [Test]
        public void UnknownClassIdInWaveThrowsVnRuntimeException()
        {
            var known = ClassOf("Known", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { known };
            var wave = OneWave(new UnitClassId("Unknown"), count: 1);
            var rooms = new List<RoomNode>();

            Assert.Throws<VnRuntimeException>(() => CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), Stats(), StatCombatWeights.Default(), FixedThreat(10), catalog, NeutralMatchup(), 1, 1, new SeededRandom(1)));
        }

        // ---- 순수함수 / 불변성 ----

        [Test]
        public void ResolveWaveDoesNotMutateInputHeroStats()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 3);
            var rooms = new List<RoomNode> { new RoomNode(new List<Attacker> { new Attacker(UnitClassIds.Tank, hp: 999, atk: 20, def: 5, canBeCaptured: false) }, hasTrap: true) };
            var hero = Stats((StatIds.STR, 20), (StatIds.DEX, 20));
            var snapshotBefore = new Dictionary<StatId, int>(hero.Values);

            CombatResolver.ResolveWave(EmptyRun(), wave, RoomGraph.Linear(rooms), hero, StatCombatWeights.Default(), FixedThreat(30), catalog, NeutralMatchup(), 1, 1, new SeededRandom(4));

            CollectionAssert.AreEquivalent(snapshotBefore, hero.Values);
        }

        // ---- RoomNode / WaveDef / CombatResult: 방어적 복사 + null 가드 ----

        [Test]
        public void RoomNodeDefensivelyCopiesDefenders()
        {
            var defenders = new List<Attacker> { new Attacker(UnitClassIds.Tank, 10, 10, 10, false) };
            var room = new RoomNode(defenders, hasTrap: true);
            defenders.Add(new Attacker(UnitClassIds.Mage, 20, 20, 20, true));

            Assert.AreEqual(1, room.Defenders.Count, "생성 이후 원본 리스트를 고쳐도 RoomNode 내부는 영향받지 않아야 함");
        }

        [Test]
        public void RoomNodeNullDefendersThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new RoomNode(null, false));
        }

        [Test]
        public void WaveDefDefensivelyCopiesIntruders()
        {
            var entries = new List<WaveDef.Entry> { new WaveDef.Entry { ClassId = UnitClassIds.Tank, Count = 1 } };
            var wave = new WaveDef(entries);
            entries.Add(new WaveDef.Entry { ClassId = UnitClassIds.Mage, Count = 2 });

            Assert.AreEqual(1, wave.Intruders.Count);
        }

        [Test]
        public void WaveDefNullIntrudersThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new WaveDef(null));
        }

        [Test]
        public void WaveDefNonPositiveCountThrows()
        {
            var entries = new List<WaveDef.Entry> { new WaveDef.Entry { ClassId = UnitClassIds.Tank, Count = 0 } };
            Assert.Throws<ArgumentException>(() => new WaveDef(entries));
        }

        [Test]
        public void CombatResultDefensivelyCopiesKilledAndCaptured()
        {
            var killed = new List<Attacker> { new Attacker(UnitClassIds.Tank, 1, 1, 1, false) };
            var captured = new List<Attacker> { new Attacker(UnitClassIds.Mage, 1, 1, 1, true) };
            var result = new CombatResult(false, killed, captured);
            killed.Clear();
            captured.Clear();

            Assert.AreEqual(1, result.Killed.Count);
            Assert.AreEqual(1, result.Captured.Count);
        }

        [Test]
        public void CombatResultNullListsThrow()
        {
            Assert.Throws<ArgumentNullException>(() => new CombatResult(false, null, new List<Attacker>()));
            Assert.Throws<ArgumentNullException>(() => new CombatResult(false, new List<Attacker>(), null));
        }
    }
}
