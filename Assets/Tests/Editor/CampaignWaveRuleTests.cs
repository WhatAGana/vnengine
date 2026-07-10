using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VNEngine.Tests
{
    public class CampaignWaveRuleTests
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

        // 모든 항을 0으로 눌러 baseOffset만 남긴다 — threatBase를 시나리오에서 완전히 통제하기 위한 테스트 전용 가중치.
        private static ThreatWeights FixedThreat(int baseOffset)
            => new ThreatWeights(wHero: 0, wLoop: 0, wPlaced: 0, wDungeon: 0, baseOffset: baseOffset);

        private static IReadOnlyList<MonsterDef> MonsterCat() => MonsterCatalog.Default();

        private static CampaignState Campaign(int loopCount = 1, int gold = 0, int karmaBank = 0)
            => new CampaignState(
                new MetaState(loopCount, HeroStats.Empty, InnState.Empty, karmaBank),
                new RunState(1, new Dictionary<string, int> { { "gold", gold } }));

        // 코어앞1칸에 방어몹 없는 3방 선형 그래프(r0,r1,r2). 배치예산 = 3방x3 = 9.
        private static RoomGraph ThreeEmptyRooms()
            => RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false),
                new RoomNode(new List<Attacker>(), false),
            });

        private static PlacementPlan ValidHeroOnlyPlan()
            => new PlacementPlan { Monsters = new List<MonsterPlacement>(), HasHero = true, HeroRoom = new RoomId("r2") };

        private static PlacementPlan InvalidHeroPlan()
            => new PlacementPlan { Monsters = new List<MonsterPlacement>(), HasHero = true, HeroRoom = new RoomId("r0") };

        // r0=trap(방어몹 배치 대상), r1=빈방, r2=코어앞1칸(주인공). PlacementBuilder.ValidateAndApply는
        // baseGraph의 기존 Defenders를 전부 버리고 plan.Monsters로만 재구성하므로, 방어몹을 낀 시나리오는
        // 반드시 plan.Monsters를 통해 실체화해야 한다(직접 RoomNode에 Attacker를 심어도 사라짐).
        private static RoomGraph ThreeRoomsWithTrapAtR0()
            => RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), hasTrap: true),
                new RoomNode(new List<Attacker>(), hasTrap: false),
                new RoomNode(new List<Attacker>(), hasTrap: false),
            });

        // 서큐버스(포획몹, atk60, cost3)를 함정방 r0에 + 주인공을 r2(코어앞1칸)에 배치.
        // (포획몹은 함정방에만 배치 가능 — 새 배치규칙.) 함정방+포획몹 격퇴 → 포획.
        private static PlacementPlan HeroPlusTrapDefenderPlan()
            => new PlacementPlan
            {
                Monsters = new List<MonsterPlacement> { new MonsterPlacement { Room = new RoomId("r0"), Monster = MonsterIds.Succubus } },
                HasHero = true,
                HeroRoom = new RoomId("r2"),
            };

        private static WaveDef OneWave(UnitClassId classId, int count)
            => new WaveDef(new List<WaveDef.Entry> { new WaveDef.Entry { ClassId = classId, Count = count } });

        [Test]
        public void ResolveWave_KillsCreditFullLoot_CapturesCreditHalfLootPlusKarma()
        {
            // 강한 주인공(즉사 화력, 항상 명중) + 방 없음(전원 코어앞1칸 도달) + 포획불가 침입자 -> 전원 사살.
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 5);
            var hero = Stats((StatIds.STR, 1000), (StatIds.DEX, 100));
            var threatWeights = FixedThreat(50); // 항상 threatBase=50 고정 -> Isqrt(50)=7.
            var campaign = Campaign();

            var outcome = CampaignWaveRule.ResolveWave(
                campaign, ValidHeroOnlyPlan(), wave, ThreeEmptyRooms(), MonsterCat(),
                hero, StatCombatWeights.Default(), threatWeights, catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(1));

            Assert.AreEqual(5, outcome.Combat.Killed.Count);
            Assert.AreEqual(0, outcome.Combat.Captured.Count);
            var expectedGold = 5 * LootRule.LootGold(50, false);
            Assert.AreEqual(expectedGold, outcome.GoldGained);
            Assert.AreEqual(0, outcome.CaptureKarmaGained);
            Assert.AreEqual(0, outcome.Campaign.Meta.KarmaBank - campaign.Meta.KarmaBank);
        }

        [Test]
        public void ResolveWave_CapturesCreditHalfLootPlusKarma()
        {
            // 고위마족 방어몹(atk130) + 함정방 -> 전원 포획(포획가능 침입자, threatBase=50 -> hp~45..55, 함정15 생존 후 방어몹에 즉사).
            var capturable = ClassOf("Capturable", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, count: 3);
            var threatWeights = FixedThreat(50);
            var campaign = Campaign();

            var outcome = CampaignWaveRule.ResolveWave(
                campaign, HeroPlusTrapDefenderPlan(), wave, ThreeRoomsWithTrapAtR0(), MonsterCat(),
                Stats(), StatCombatWeights.Default(), threatWeights, catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(2));

            Assert.AreEqual(3, outcome.Combat.Captured.Count);
            Assert.AreEqual(0, outcome.Combat.Killed.Count);
            var expectedGold = 3 * LootRule.LootGold(50, true);
            var expectedKarma = 3 * LootRule.CaptureKarma(50);
            Assert.AreEqual(expectedGold, outcome.GoldGained);
            Assert.AreEqual(expectedKarma, outcome.CaptureKarmaGained);
            Assert.AreEqual(expectedKarma, outcome.Campaign.Meta.KarmaBank - campaign.Meta.KarmaBank, "KarmaBank가 정확히 CaptureKarmaGained 만큼 증가");
        }

        [Test]
        public void ResolveWave_AccumulatesCaptivesIntoRun()
        {
            var capturable = ClassOf("Capturable", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, count: 2);
            var campaign = Campaign();

            var outcome = CampaignWaveRule.ResolveWave(
                campaign, HeroPlusTrapDefenderPlan(), wave, ThreeRoomsWithTrapAtR0(), MonsterCat(),
                Stats(), StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(3));

            Assert.AreEqual(outcome.Combat.Captured.Count, outcome.Campaign.Run.Captives.Count);
            Assert.AreEqual(2, outcome.Campaign.Run.Captives.Count);
        }

        [Test]
        public void ResolveWave_AddsGoldToResource()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 2);
            var hero = Stats((StatIds.STR, 1000), (StatIds.DEX, 100));
            var campaign = Campaign(gold: 40);

            var outcome = CampaignWaveRule.ResolveWave(
                campaign, ValidHeroOnlyPlan(), wave, ThreeEmptyRooms(), MonsterCat(),
                hero, StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(4));

            Assert.AreEqual(40 + outcome.GoldGained, outcome.Campaign.Run.Resources["gold"]);
        }

        [Test]
        public void ResolveWave_InvalidPlacement_Throws()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);

            var ex = Assert.Throws<VnRuntimeException>(() => CampaignWaveRule.ResolveWave(
                Campaign(), InvalidHeroPlan(), wave, ThreeEmptyRooms(), MonsterCat(),
                Stats(), StatCombatWeights.Default(), FixedThreat(10), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(1)));
            StringAssert.Contains("HeroRoomNotCoreFront", ex.Message);
        }

        [Test]
        public void ResolveWave_Deterministic()
        {
            // 포획 경로(함정방+즉사 방어몹)를 두 번 동일 시드로 돌려 GoldGained/카르마/포로수/자원이 완전히 같은지 확인.
            var capturable = ClassOf("Capturable", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { capturable };
            var wave = OneWave(capturable.Id, count: 5);
            var hero = Stats((StatIds.STR, 50), (StatIds.DEX, 50));

            var a = CampaignWaveRule.ResolveWave(
                Campaign(), HeroPlusTrapDefenderPlan(), wave, ThreeRoomsWithTrapAtR0(), MonsterCat(),
                hero, StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(777));
            var b = CampaignWaveRule.ResolveWave(
                Campaign(), HeroPlusTrapDefenderPlan(), wave, ThreeRoomsWithTrapAtR0(), MonsterCat(),
                hero, StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(777));

            Assert.AreEqual(a.GoldGained, b.GoldGained);
            Assert.AreEqual(a.CaptureKarmaGained, b.CaptureKarmaGained);
            Assert.AreEqual(a.Campaign.Run.Captives.Count, b.Campaign.Run.Captives.Count);
            Assert.AreEqual(a.Campaign.Run.Resources["gold"], b.Campaign.Run.Resources["gold"]);
        }

        [Test]
        public void ResolveWave_DoesNotMutateInputCampaign()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 3);
            var hero = Stats((StatIds.STR, 1000), (StatIds.DEX, 100));
            var campaign = Campaign(gold: 10, karmaBank: 5);
            var goldBefore = campaign.Run.Resources["gold"];
            var captivesBefore = campaign.Run.Captives.Count;
            var karmaBefore = campaign.Meta.KarmaBank;

            CampaignWaveRule.ResolveWave(
                campaign, ValidHeroOnlyPlan(), wave, ThreeEmptyRooms(), MonsterCat(),
                hero, StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(9));

            Assert.AreEqual(goldBefore, campaign.Run.Resources["gold"]);
            Assert.AreEqual(captivesBefore, campaign.Run.Captives.Count);
            Assert.AreEqual(karmaBefore, campaign.Meta.KarmaBank);
        }

        [Test]
        public void ResolveWave_PreservesPullsThisLoop()
        {
            // 07-C review fix: CaptiveLedger.Accumulate가 PullsThisLoop를 0으로 초기화하던 회귀 방지(엔드투엔드).
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 2);
            var hero = Stats((StatIds.STR, 1000), (StatIds.DEX, 100));
            var campaign = new CampaignState(
                new MetaState(1, HeroStats.Empty, InnState.Empty, 0),
                new RunState(1, new Dictionary<string, int> { { "gold", 0 } }, new List<Captive>(), pullsThisLoop: 3));

            var outcome = CampaignWaveRule.ResolveWave(
                campaign, ValidHeroOnlyPlan(), wave, ThreeEmptyRooms(), MonsterCat(),
                hero, StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(5));

            Assert.AreEqual(3, outcome.Campaign.Run.PullsThisLoop, "가챠 카운터가 웨이브 해결로 리셋되면 안 된다");
        }

        [Test]
        public void ResolveWave_InnIncomeAddsGoldAndKarma()
        {
            // 여관(Decor=5,Staff=3,MenuLevel=2)이 전투와 별개로 매 웨이브 수급을 더한다. 순수 규칙과 동일 계산으로 기대값 산출.
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 2);
            var hero = Stats((StatIds.STR, 1000), (StatIds.DEX, 100));
            var inn = new InnState(staff: 3, decor: 5, menuLevel: 2);
            var campaign = new CampaignState(
                new MetaState(1, HeroStats.Empty, inn, karmaBank: 0),
                new RunState(1, new Dictionary<string, int> { { "gold", 0 } }));
            var expectedIncome = InnIncomeRule.Compute(inn);

            var outcome = CampaignWaveRule.ResolveWave(
                campaign, ValidHeroOnlyPlan(), wave, ThreeEmptyRooms(), MonsterCat(),
                hero, StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(11));

            Assert.AreEqual(expectedIncome.Gold, outcome.InnGoldGained);
            Assert.AreEqual(expectedIncome.Karma, outcome.InnKarmaGained);
            Assert.AreEqual(outcome.GoldGained + expectedIncome.Gold, outcome.Campaign.Run.Resources["gold"]);
            Assert.AreEqual(outcome.CaptureKarmaGained + expectedIncome.Karma, outcome.Campaign.Meta.KarmaBank);
            Assert.AreEqual(4, outcome.Campaign.Meta.Inn.Decor, "DecorDecayPerTick=1 만큼 감쇠");
        }

        [Test]
        public void ResolveWave_InnGateBeforeDecay_DecorOne_StillEarnsThenDecaysToZero()
        {
            // Decor=1: 게이트가 감쇠 전 Decor로 판정되어야 수입이 발생한다(감쇠를 먼저 하면 Decor=0이 되어 수입 0으로 잘못됨).
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);
            var hero = Stats((StatIds.STR, 1000), (StatIds.DEX, 100));
            var inn = new InnState(staff: 3, decor: 1, menuLevel: 2);
            var campaign = new CampaignState(
                new MetaState(1, HeroStats.Empty, inn, karmaBank: 0),
                new RunState(1, new Dictionary<string, int> { { "gold", 0 } }));
            var expectedIncome = InnIncomeRule.Compute(inn);

            var outcome = CampaignWaveRule.ResolveWave(
                campaign, ValidHeroOnlyPlan(), wave, ThreeEmptyRooms(), MonsterCat(),
                hero, StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(12));

            Assert.Greater(expectedIncome.Gold, 0, "테스트 전제: Decor=1에서 Compute는 수입을 낸다");
            Assert.AreEqual(expectedIncome.Gold, outcome.InnGoldGained);
            Assert.AreEqual(expectedIncome.Karma, outcome.InnKarmaGained);
            Assert.AreEqual(0, outcome.Campaign.Meta.Inn.Decor);
        }

        [Test]
        public void ResolveWave_InnDecorZero_NoInnIncome()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);
            var hero = Stats((StatIds.STR, 1000), (StatIds.DEX, 100));
            var inn = new InnState(staff: 3, decor: 0, menuLevel: 2);
            var campaign = new CampaignState(
                new MetaState(1, HeroStats.Empty, inn, karmaBank: 0),
                new RunState(1, new Dictionary<string, int> { { "gold", 0 } }));

            var outcome = CampaignWaveRule.ResolveWave(
                campaign, ValidHeroOnlyPlan(), wave, ThreeEmptyRooms(), MonsterCat(),
                hero, StatCombatWeights.Default(), FixedThreat(50), catalog, NeutralMatchup(),
                CaptureRule.Default(), dungeonLevel: 1, goldResourceId: "gold", rng: new SeededRandom(13));

            Assert.AreEqual(0, outcome.InnGoldGained);
            Assert.AreEqual(0, outcome.InnKarmaGained);
            Assert.AreEqual(0, outcome.Campaign.Meta.Inn.Decor);
        }

        [Test]
        public void ResolveWave_NullArgumentsThrow()
        {
            var grunt = ClassOf("Grunt", 100, 100, 100, canBeCaptured: false);
            var catalog = new List<UnitClassDef> { grunt };
            var wave = OneWave(grunt.Id, count: 1);
            var campaign = Campaign();
            var hero = Stats();
            var statWeights = StatCombatWeights.Default();
            var threatWeights = FixedThreat(10);
            var matchup = NeutralMatchup();
            var captureRule = CaptureRule.Default();
            var graph = ThreeEmptyRooms();
            var plan = ValidHeroOnlyPlan();

            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(null, plan, wave, graph, MonsterCat(), hero, statWeights, threatWeights, catalog, matchup, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, null, graph, MonsterCat(), hero, statWeights, threatWeights, catalog, matchup, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, null, MonsterCat(), hero, statWeights, threatWeights, catalog, matchup, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, null, hero, statWeights, threatWeights, catalog, matchup, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, MonsterCat(), null, statWeights, threatWeights, catalog, matchup, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, MonsterCat(), hero, null, threatWeights, catalog, matchup, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, MonsterCat(), hero, statWeights, null, catalog, matchup, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, MonsterCat(), hero, statWeights, threatWeights, null, matchup, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, MonsterCat(), hero, statWeights, threatWeights, catalog, null, captureRule, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, MonsterCat(), hero, statWeights, threatWeights, catalog, matchup, null, 1, "gold", new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, MonsterCat(), hero, statWeights, threatWeights, catalog, matchup, captureRule, 1, null, new SeededRandom(1)));
            Assert.Throws<ArgumentNullException>(() => CampaignWaveRule.ResolveWave(campaign, plan, wave, graph, MonsterCat(), hero, statWeights, threatWeights, catalog, matchup, captureRule, 1, "gold", null));
        }
    }
}
