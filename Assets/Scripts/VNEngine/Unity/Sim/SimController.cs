using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine.Unity
{
    // 05 시뮬 하버스 = 검증 도구(정본 게임 UI 아님). 예쁠 필요 없음 — 표시값은 전부 실제 Core 상태(CampaignState).
    //
    // ★ 2단계(07-C 배선 완료판): 07-C에서 전투→약탈→골드/포획/인과율/여관수급 배선과 소비(스탯강화·레벨업·가챠)가
    //   되었으므로, "웨이브 실행" 버튼이 실제 CampaignWaveRule.ResolveWave 를 호출한다. 결과(처치·포획·약탈골드·
    //   포획인과율·여관수급)가 실제 자원/메타에 반영되는 걸 화면으로 확인한다.
    //
    // 웨이브 시나리오는 코드에 박은 고정 픽스처(제국병 처치 + 광신도 포획, HighDemon 함정방)지만,
    // 그 결과 수치(골드/인과율/포로)는 전부 Core 순수함수가 계산한 실값이다 — 가짜 숫자 없음.
    public sealed class SimController : MonoBehaviour
    {
        [Header("Definitions (ScriptableObjects) — 있으면 사용, 없어도 gold/manaStone 자동 주입")]
        [SerializeField] private List<ResourceDefinitionSO> resources = new List<ResourceDefinitionSO>();
        [SerializeField] private List<CommandDefinitionSO> commands = new List<CommandDefinitionSO>();

        [Header("UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button buttonPrefab;
        [SerializeField] private Button newLoopButton; // "새 회차" — 없으면 자동 생성
        [SerializeField] private Button saveButton;    // "세이브" — 없으면 무시
        [SerializeField] private Button loadButton;    // "로드" — 없으면 무시
        [SerializeField] private int saveSlot = 0;

        [Header("경제 자원 id (Core는 테마중립 — id 주입)")]
        [SerializeField] private string goldResourceId = "gold";
        [SerializeField] private string manaResourceId = "manaStone";

        [Header("검증 시딩 (실제 데이터 타입으로 볼거리 확보 — 끄면 진짜 초기상태)")]
        [SerializeField] private bool seedOnStart = true;
        [SerializeField] private int seedGold = 500;
        [SerializeField] private int seedMana = 50;
        [SerializeField] private int seedKarmaBank = 200;
        [SerializeField] private int seedInnStaff = 3;
        [SerializeField] private int seedInnDecor = 10;
        [SerializeField] private int seedInnMenuLevel = 2;
        [SerializeField] private int levelUpKarmaCost = 10; // 던전 레벨업 인과율 요구(검증용 소액)

        private TurnEngine _turnEngine;
        private LoopEngine _loop;
        private CampaignState _campaign;

        private int _waveSeed = 1;        // 웨이브마다 증가 → 전투 롤이 조금씩 바뀜(결정론은 유지)
        private int _statUpgradeIndex;    // "스탯 강화"를 8스탯 순환하며 적용
        private string _lastAction = "(대기 중 — 버튼을 눌러보세요)";

        private void Start()
        {
            // 자원 정의: SO에서 읽되, 경제에 필요한 gold/manaStone 이 없으면 코드로 주입(씬 세팅 없이도 동작).
            var byId = new Dictionary<string, ResourceDef>();
            var resDefs = new List<ResourceDef>();
            foreach (var r in resources)
            {
                var d = r.ToDef();
                if (!byId.ContainsKey(d.Id)) { byId[d.Id] = d; resDefs.Add(d); }
            }
            EnsureResource(resDefs, byId, goldResourceId, "골드", seedGold);
            EnsureResource(resDefs, byId, manaResourceId, "마석", seedMana);

            var cmdDefs = new List<CommandDef>(commands.Count);
            foreach (var c in commands) cmdDefs.Add(c.ToDef());

            _turnEngine = new TurnEngine(resDefs, cmdDefs); // 배선 오류면 여기서 VnRuntimeException → 콘솔 에러
            _loop = new LoopEngine(_turnEngine);
            _campaign = _loop.CreateInitialCampaign(); // 07-C: 주인공 8스탯 라이브 시딩됨

            if (seedOnStart) SeedForDebug();

            BuildButtons();

            if (saveButton != null)
            {
                saveButton.onClick.RemoveAllListeners();
                saveButton.onClick.AddListener(OnSave);
            }
            if (loadButton != null)
            {
                loadButton.onClick.RemoveAllListeners();
                loadButton.onClick.AddListener(OnLoad);
            }

            Refresh();
        }

        private static void EnsureResource(List<ResourceDef> defs, Dictionary<string, ResourceDef> byId,
                                           string id, string displayName, int startValue)
        {
            if (string.IsNullOrEmpty(id) || byId.ContainsKey(id)) return;
            var d = new ResourceDef(id, displayName, startValue);
            byId[id] = d;
            defs.Add(d);
        }

        // 초기 캠페인은 여관이 비어 있고(Decor=0 게이트닫힘) 인과율/골드도 최소다.
        // 검증용으로 여관을 seed 값으로, 인과율 bank 를 seed 값으로 채운다. 주인공 8스탯은 이미 라이브 시딩됨(유지).
        private void SeedForDebug()
        {
            var m = _campaign.Meta;
            var inn = new InnState(seedInnStaff, seedInnDecor, seedInnMenuLevel);
            var meta = new MetaState(m.LoopCount, m.Heroes, inn, seedKarmaBank, m.DungeonLevel);
            _campaign = new CampaignState(meta, _campaign.Run);
        }

        private void BuildButtons()
        {
            if (buttonPrefab == null || buttonContainer == null)
            {
                Debug.LogError("[SimController] buttonPrefab or buttonContainer not assigned");
                return;
            }

            SpawnButton("웨이브 실행", OnWave);
            SpawnButton("다음 날", OnNextDay);
            if (newLoopButton != null)
            {
                newLoopButton.onClick.RemoveAllListeners();
                newLoopButton.onClick.AddListener(OnNewLoop);
            }
            else
            {
                SpawnButton("새 회차(회귀)", OnNewLoop);
            }
            SpawnButton("스탯 강화(인과율)", OnUpgradeStat);
            SpawnButton("가챠 pull(마석)", OnGachaPull);
            SpawnButton("던전 레벨업(골드+인과)", OnLevelUp);

            // 기존 SO 자원 커맨드(raid/build 등)도 유지 — 자원 델타 커맨드 검증용.
            foreach (var c in _turnEngine.Commands)
            {
                string commandId = c.Id; // capture
                SpawnButton(c.DisplayName, () => OnCommand(commandId));
            }
        }

        private void SpawnButton(string label, UnityEngine.Events.UnityAction onClick)
        {
            Button btn = Instantiate(buttonPrefab, buttonContainer);
            btn.name = $"Btn_{label}";
            var tmp = btn.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null) tmp.text = label;
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(onClick);
        }

        // ================= 버튼 핸들러 (전부 실제 Core 호출) =================

        // 웨이브 실행: 실제 CampaignWaveRule.ResolveWave. 고정 시나리오지만 결과 수치는 Core가 계산한 실값.
        private void OnWave()
        {
            var outcome = RunDebugWave();
            _campaign = outcome.Campaign;
            var sb = new StringBuilder();
            sb.Append("웨이브: 처치 ").Append(outcome.Combat.Killed.Count)
              .Append(" · 포획 ").Append(outcome.Combat.Captured.Count)
              .Append(" | 약탈 +").Append(outcome.GoldGained).Append("골드")
              .Append(" · 포획인과 +").Append(outcome.CaptureKarmaGained)
              .Append(" | 여관 +").Append(outcome.InnGoldGained).Append("골드/+")
              .Append(outcome.InnKarmaGained).Append("인과");
            if (outcome.Combat.CoreHit) sb.Append(" | ⚠️코어피격");
            _lastAction = sb.ToString();
            Refresh();
        }

        // 고정 검증 시나리오: 제국병 3(포획불가→처치) + 광신도 2(포획가능→포획), HighDemon 함정방(r0)에서 전원 즉사/포획.
        // 주인공은 r2(코어앞1칸)에 배치(placement 게이트 통과 조건). threatBase=10 고정.
        private WaveOutcome RunDebugWave()
        {
            var soldier = new UnitClassDef(new UnitClassId("ImperialSoldier"), "제국병", 100, 100, 100, canBeCaptured: false);
            var zealot = new UnitClassDef(new UnitClassId("Zealot"), "광신도", 100, 100, 100, canBeCaptured: true);
            var catalog = new List<UnitClassDef> { soldier, zealot };

            var wave = new WaveDef(new List<WaveDef.Entry>
            {
                new WaveDef.Entry { ClassId = soldier.Id, Count = 3 },
                new WaveDef.Entry { ClassId = zealot.Id, Count = 2 },
            });

            var graph = RoomGraph.Linear(new List<RoomNode>
            {
                new RoomNode(new List<Attacker>(), hasTrap: true),  // r0 함정방 + HighDemon 배치
                new RoomNode(new List<Attacker>(), hasTrap: false), // r1
                new RoomNode(new List<Attacker>(), hasTrap: false), // r2 코어앞1칸(주인공)
            });

            var plan = new PlacementPlan
            {
                Monsters = new List<MonsterPlacement>
                {
                    new MonsterPlacement { Room = new RoomId("r0"), Monster = MonsterIds.HighDemon },
                },
                HasHero = true,
                HeroRoom = new RoomId("r2"),
            };

            var threatWeights = new ThreatWeights(wHero: 0, wLoop: 0, wPlaced: 0, wDungeon: 0, baseOffset: 10);

            return CampaignWaveRule.ResolveWave(
                _campaign, plan, wave, graph, MonsterCatalog.Default(),
                _campaign.Meta.Heroes, StatCombatWeights.Default(), threatWeights, catalog,
                new ClassMatchup(new List<ClassMatchup.Entry>()), CaptureRule.Default(),
                _campaign.Meta.DungeonLevel, goldResourceId, new SeededRandom(_waveSeed++));
        }

        // "다음 날": 하루 경과 + 여관 내구도 자연감소(InnUpkeepRule.Decay). 메타(인과율/던전레벨)·런(pull카운터)은 보존.
        private void OnNextDay()
        {
            var m = _campaign.Meta;
            var decayedInn = InnUpkeepRule.Decay(m.Inn);
            var meta = new MetaState(m.LoopCount, m.Heroes, decayedInn, m.KarmaBank, m.DungeonLevel);
            var r = _campaign.Run;
            var run = new RunState(r.Day + 1, r.Resources, r.Captives, r.PullsThisLoop);
            _campaign = new CampaignState(meta, run);
            _lastAction = "다음 날: 여관 내구 감소(-1), 런 상태 유지";
            Refresh();
        }

        // "새 회차(회귀)": LoopEngine.StartNewLoop. 런(골드/마석/포로/pull카운터) 리셋 + 메타(인과율bank·주인공스탯·던전레벨·여관) 유지 확인용.
        private void OnNewLoop()
        {
            int karmaBefore = _campaign.Meta.KarmaBank;
            int dlBefore = _campaign.Meta.DungeonLevel;
            _campaign = _loop.StartNewLoop(_campaign);
            _lastAction = $"새 회차 → 회차{_campaign.Meta.LoopCount}: 런 리셋(골드/마석/포로/pull=0), 메타 유지(인과율 {karmaBefore}, 던전Lv {dlBefore})";
            Refresh();
        }

        // "스탯 강화": EconomySpend.UpgradeHeroStat — 인과율 bank로 한 스탯을 올린다(8스탯 순환).
        private void OnUpgradeStat()
        {
            var defs = StatCatalog.Default();
            var def = defs[_statUpgradeIndex % defs.Count];
            _statUpgradeIndex++;

            var m = _campaign.Meta;
            int before = m.Heroes.TryGet(def.Id, out var bv) ? bv : 0;
            int bankBefore = m.KarmaBank;
            var newMeta = EconomySpend.UpgradeHeroStat(m, def, StatCostCurve.Default());
            _campaign = new CampaignState(newMeta, _campaign.Run);
            int after = newMeta.Heroes.TryGet(def.Id, out var av) ? av : 0;
            int spent = bankBefore - newMeta.KarmaBank;

            _lastAction = spent > 0
                ? $"스탯 강화: {def.DisplayName} {before}→{after} (인과율 {spent} 소비, bank {newMeta.KarmaBank})"
                : $"스탯 강화 실패: 인과율 부족 (bank {bankBefore})";
            Refresh();
        }

        // "가챠 pull": EconomySpend.GachaPull — 마석 소비 + pull카운터 증가(뽑을수록 비싸짐).
        private void OnGachaPull()
        {
            var res = EconomySpend.GachaPull(_campaign.Run, manaResourceId);
            _campaign = new CampaignState(_campaign.Meta, res.Run);
            _lastAction = res.Pulled
                ? $"가챠 pull 성공: 비용 {res.Cost}마석, 이번회차 pull수 {res.Run.PullsThisLoop} (다음 비용 {GachaRule.GachaCost(res.Run.PullsThisLoop)})"
                : $"가챠 pull 실패: 마석 부족 (필요 {res.Cost})";
            Refresh();
        }

        // "던전 레벨업": EconomySpend.LevelUpDungeon — 골드 + 인과율 소비, 던전레벨 +1.
        private void OnLevelUp()
        {
            int cost = DungeonLevelRule.LevelUpCost(_campaign.Meta.DungeonLevel);
            var res = EconomySpend.LevelUpDungeon(_campaign, goldResourceId, levelUpKarmaCost);
            _campaign = res.Campaign;
            _lastAction = res.Leveled
                ? $"던전 레벨업 → Lv{_campaign.Meta.DungeonLevel} (골드 {cost} + 인과율 {levelUpKarmaCost} 소비)"
                : $"던전 레벨업 실패: 골드({cost}) 또는 인과율({levelUpKarmaCost}) 부족";
            Refresh();
        }

        // ---- SO 자원 커맨드(기존) ----
        private void OnCommand(string commandId)
        {
            _campaign = _loop.ExecuteCommand(_campaign, commandId);
            _lastAction = $"커맨드 실행: {commandId}";
            Refresh();
        }

        // ---- 세이브/로드(기존) ----
        private void OnSave()
        {
            CampaignSaveSystem.Write(saveSlot, CampaignSave.Capture(_campaign));
            _lastAction = $"세이브(slot {saveSlot}): 회차{_campaign.Meta.LoopCount}/일차{_campaign.Run.Day}";
            Refresh();
        }

        private void OnLoad()
        {
            var data = CampaignSaveSystem.Read(saveSlot);
            if (data == null) { _lastAction = $"로드 실패: slot {saveSlot} 비어있음"; Refresh(); return; }
            _campaign = CampaignSave.Restore(data);
            _lastAction = $"로드(slot {saveSlot}) 완료";
            Refresh();
        }

        // ================= 한 화면 상태 표시(전부 실제 Core 상태 읽기) =================

        private void Refresh()
        {
            if (statusText == null) return;
            var meta = _campaign.Meta;
            var run = _campaign.Run;
            var sb = new StringBuilder();

            sb.Append("=== 검증 대시보드 (실제 Core 상태) ===\n");
            sb.Append("회차 ").Append(meta.LoopCount)
              .Append(" · 일차 ").Append(run.Day)
              .Append(" · 던전 Lv ").Append(meta.DungeonLevel)
              .Append(" · 가챠 pull(이번회차) ").Append(run.PullsThisLoop).Append('\n');

            sb.Append("자원: ");
            bool first = true;
            foreach (var r in _turnEngine.Resources)
            {
                if (!first) sb.Append(" · ");
                sb.Append(r.DisplayName).Append(' ').Append(run.Resources.TryGetValue(r.Id, out var rv) ? rv : 0);
                first = false;
            }
            sb.Append("  ||  인과율 bank ").Append(meta.KarmaBank).Append('\n');

            sb.Append("주인공 8스탯: ");
            foreach (var d in StatCatalog.Default())
            {
                sb.Append(d.DisplayName).Append(' ')
                  .Append(meta.Heroes.TryGet(d.Id, out var v) ? v : 0).Append("  ");
            }
            sb.Append('\n');

            // 여관: 현재 상태 + InnIncomeRule 수급 미리보기(읽기 전용 계산 — 웨이브 실행 시 실제 반영).
            var inn = meta.Inn;
            var income = InnIncomeRule.Compute(inn);
            sb.Append("여관: 직원 ").Append(inn.Staff)
              .Append(" · 내구 ").Append(inn.Decor)
              .Append(" · 메뉴 ").Append(inn.MenuLevel)
              .Append("  → 웨이브당 수급 손님 ").Append(income.Guests)
              .Append(" / 인과 ").Append(income.Karma)
              .Append(" / 골드 ").Append(income.Gold);
            if (inn.Decor <= 0) sb.Append(" (내구0 게이트닫힘)");
            sb.Append('\n');

            sb.Append("감옥 포로(").Append(run.Captives.Count).Append("): ")
              .Append(SummarizeCaptives(run.Captives)).Append('\n');

            sb.Append("── 마지막 동작: ").Append(_lastAction);

            statusText.text = sb.ToString();
        }

        // 포로를 병종+네임드 기준으로 집계("광신도(잡졸)×2").
        private static string SummarizeCaptives(IReadOnlyList<Captive> captives)
        {
            if (captives.Count == 0) return "(없음)";
            var order = new List<string>();
            var count = new Dictionary<string, int>();
            foreach (var c in captives)
            {
                string key = c.ClassId.Value + (c.IsNamed ? "(네임드)" : "(잡졸)");
                if (!count.ContainsKey(key)) { count[key] = 0; order.Add(key); }
                count[key]++;
            }
            var sb = new StringBuilder();
            for (int i = 0; i < order.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(order[i]).Append('×').Append(count[order[i]]);
            }
            return sb.ToString();
        }
    }
}
