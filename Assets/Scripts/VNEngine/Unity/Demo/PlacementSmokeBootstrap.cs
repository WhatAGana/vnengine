using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VNEngine.Unity
{
    // Smoke-test harness for 07-B placement (budget system, NOT fixed slots).
    // Builds a click-driven placement UI in code and edits a LOCAL PlacementPlan,
    // re-validating with PlacementValidator on every action. No RunState changes,
    // no PlacementBuilder.Apply — verify only "mobs stack into rooms and the budget
    // bar ticks down/up." (See docs/engine/ui_placement_spec.md, option B.)
    //
    // Flow: click a mob in the inventory (select) -> click a room's "+배치" header
    // (place, if within budget) -> click a placed mob (remove). Budget = rooms x 3.
    public class PlacementSmokeBootstrap : MonoBehaviour
    {
        [Tooltip("Korean-capable TMP font (assign NotoSansKR-Regular SDF). If null, Korean renders as boxes.")]
        public TMP_FontAsset koreanFont;

        [Tooltip("Number of placeable rooms for this smoke (budget = rooms * 3).")]
        public int roomCount = 3;

        // --- core state (local, not persisted) ---
        private IReadOnlyList<MonsterDef> _catalog;
        private Dictionary<UnitClassId, MonsterDef> _defById;
        private RoomGraph _graph;
        private List<MonsterPlacement> _plan = new List<MonsterPlacement>();
        private int _selected = -1;                 // index into _catalog, -1 = none

        // --- wave/combat context (Path B: reuse SimController's fixed scenario) ---
        [Tooltip("Intruder toughness (threatBase). Hero is seeded ~0 dmg, so empty placement always breaches the core; higher = defenders must stack more to kill. 55 gives a clean gradient within the budget-9 economy.")]
        public int threatBaseOffset = 55;
        private const string GoldId = "gold";
        private CampaignState _campaign;
        private IReadOnlyList<UnitClassDef> _classCatalog;
        private WaveDef _wave;
        private int _totalIntruders;
        private StatCombatWeights _statWeights;
        private ThreatWeights _threatWeights;
        private ClassMatchup _matchup;
        private CaptureRule _captureRule;
        private int _waveSeed = 1;
        private TMP_Text _resultText;

        // --- view refs ---
        private TMP_Text _budgetText;
        private TMP_Text _statusText;
        private readonly List<Transform> _roomMobLists = new List<Transform>(); // per placeable room
        private readonly List<RoomId> _roomIds = new List<RoomId>();
        private readonly List<Button> _invButtons = new List<Button>();

        private static readonly Color BtnColor     = new Color(0.20f, 0.20f, 0.28f, 0.95f);
        private static readonly Color BtnSelected  = new Color(0.85f, 0.65f, 0.20f, 1f);
        private static readonly Color MobColor     = new Color(0.24f, 0.30f, 0.40f, 0.95f);
        private static readonly Color RoomBg       = new Color(1f, 1f, 1f, 0.06f);
        private static readonly Color CoreBg       = new Color(0.45f, 0.20f, 0.20f, 0.5f);

        private void Awake()
        {
            _catalog = MonsterCatalog.Default();
            _defById = new Dictionary<UnitClassId, MonsterDef>();
            foreach (var m in _catalog) _defById[m.Id] = m;

            // 3 empty placeable rooms via the real 07-B graph builder.
            var content = new List<RoomNode>();
            for (int i = 0; i < roomCount; i++)
                content.Add(new RoomNode(System.Array.Empty<Attacker>(), hasTrap: i == 0)); // 방1 = 함정방 → 포획 트리거
            _graph = RoomGraph.Linear(content);
            foreach (var r in _graph.Rooms) _roomIds.Add(r.Id);

            BuildCombatContext();

            BuildCamera();
            BuildEventSystem();
            var canvas = BuildCanvas();
            BuildTopBar(canvas.transform);
            BuildRoomsArea(canvas.transform);
            BuildInventory(canvas.transform);

            Render();
        }

        // ---- actions ----

        private void OnSelectMob(int index)
        {
            _selected = index;
            SetStatus($"선택: {_catalog[index].DisplayName} (C{_catalog[index].Cost}) — 방을 클릭해 배치");
            Render();
        }

        private void OnPlaceInRoom(RoomId room)
        {
            if (_selected < 0) { SetStatus("먼저 보유몹을 선택하세요"); return; }
            var mob = _catalog[_selected];

            var tentative = new List<MonsterPlacement>(_plan) { new MonsterPlacement { Room = room, Monster = mob.Id } };
            var r = PlacementValidator.Validate(MakePlan(tentative), _graph, _catalog);
            if (r.IsValid)
            {
                _plan = tentative;
                SetStatus($"배치: {mob.DisplayName} → {RoomLabel(room)}  (예산 {r.TotalCost}/{r.Budget})");
            }
            else if (r.Error == PlacementError.OverBudget)
            {
                SetStatus($"예산 초과! 배치 거부 — {mob.DisplayName} 넣으면 {r.TotalCost} > {r.Budget}");
            }
            else
            {
                SetStatus($"배치 거부: {r.Error}");
            }
            Render();
        }

        private void OnRemovePlacement(int planIndex)
        {
            if (planIndex < 0 || planIndex >= _plan.Count) return;
            var removed = _defById[_plan[planIndex].Monster];
            _plan.RemoveAt(planIndex);
            var r = PlacementValidator.Validate(MakePlan(_plan), _graph, _catalog);
            SetStatus($"제거: {removed.DisplayName}  (예산 {r.TotalCost}/{r.Budget})");
            Render();
        }

        private static PlacementPlan MakePlan(List<MonsterPlacement> monsters)
            => new PlacementPlan { Monsters = monsters, HasHero = false };

        // Fixed wave + combat context, copied from SimController (제국병 3 + 광신도 2).
        private void BuildCombatContext()
        {
            var soldier = new UnitClassDef(new UnitClassId("ImperialSoldier"), "제국병", 100, 100, 100, canBeCaptured: false);
            var zealot  = new UnitClassDef(new UnitClassId("Zealot"), "광신도", 100, 100, 100, canBeCaptured: true);
            _classCatalog = new List<UnitClassDef> { soldier, zealot };
            _wave = new WaveDef(new List<WaveDef.Entry>
            {
                new WaveDef.Entry { ClassId = soldier.Id, Count = 3 },
                new WaveDef.Entry { ClassId = zealot.Id,  Count = 2 },
            });
            _totalIntruders = 5;
            _statWeights = StatCombatWeights.Default();
            _threatWeights = new ThreatWeights(wHero: 0, wLoop: 0, wPlaced: 0, wDungeon: 0, baseOffset: threatBaseOffset);
            _matchup = new ClassMatchup(new List<ClassMatchup.Entry>());
            _captureRule = CaptureRule.Default();
            _campaign = MakeFreshCampaign();
        }

        private CampaignState MakeFreshCampaign()
        {
            var run = new RunState(1, new Dictionary<string, int> { [GoldId] = 0 });
            var meta = new MetaState(1, HeroStats.FromDefs(StatCatalog.Default()), InnState.Empty, 0, 1);
            return new CampaignState(meta, run);
        }

        // 웨이브 실행: 로컬 _plan을 PlacementPlan으로 감싸 실제 CampaignWaveRule.ResolveWave 호출.
        // 결과는 Core가 계산한 실값(집계). 방별 상세는 Path A(후속). 매 실행 fresh 캠페인 → 결과=이번 배치만 반영.
        private void OnRunWave()
        {
            _campaign = MakeFreshCampaign();
            try
            {
                var outcome = CampaignWaveRule.ResolveWave(
                    _campaign, MakePlan(_plan), _wave, _graph, _catalog,
                    _campaign.Meta.Heroes, _statWeights, _threatWeights, _classCatalog,
                    _matchup, _captureRule, _campaign.Meta.DungeonLevel, GoldId,
                    new SeededRandom(_waveSeed++));
                _campaign = outcome.Campaign;
                int killed = outcome.Combat.Killed.Count;
                int captured = outcome.Combat.Captured.Count;
                int reached = _totalIntruders - killed - captured;
                string core = outcome.Combat.CoreHit ? "   [코어 피격!]" : "";
                _resultText.text = $"결과:  처치 {killed} · 포획 {captured} · 코어통과 {reached}{core}"
                                 + $"    |  약탈 +{outcome.GoldGained}골드 · 인과 +{outcome.CaptureKarmaGained}";
                _resultText.color = outcome.Combat.CoreHit ? new Color(1f, 0.5f, 0.4f) : new Color(0.5f, 1f, 0.6f);
            }
            catch (VnRuntimeException e)
            {
                _resultText.text = $"웨이브 무효: {e.Message}";
                _resultText.color = new Color(1f, 0.5f, 0.4f);
            }
        }

        private string RoomLabel(RoomId id)
        {
            int idx = _roomIds.IndexOf(id);
            return idx >= 0 ? $"방{idx + 1}" : id.Value;
        }

        // ---- render ----

        private void Render()
        {
            var result = PlacementValidator.Validate(MakePlan(_plan), _graph, _catalog);
            _budgetText.text = $"던전레벨 L1   ·   예산  {result.TotalCost} / {result.Budget}   ·   방 {_roomIds.Count}칸";
            _budgetText.color = result.TotalCost >= result.Budget ? new Color(1f, 0.55f, 0.4f) : Color.white;

            // Rebuild each room's placed-mob list from the plan.
            for (int ri = 0; ri < _roomMobLists.Count; ri++)
            {
                var list = _roomMobLists[ri];
                for (int c = list.childCount - 1; c >= 0; c--) Destroy(list.GetChild(c).gameObject);

                var roomId = _roomIds[ri];
                for (int pi = 0; pi < _plan.Count; pi++)
                {
                    if (!_plan[pi].Room.Equals(roomId)) continue;
                    var def = _defById[_plan[pi].Monster];
                    int captured = pi;
                    var b = MakeButton(list, $"{def.DisplayName}  C{def.Cost}   ✕", 0f, 40f, () => OnRemovePlacement(captured));
                    b.GetComponent<Image>().color = MobColor;
                }
            }

            // Inventory selection highlight.
            for (int i = 0; i < _invButtons.Count; i++)
                _invButtons[i].GetComponent<Image>().color = (i == _selected) ? BtnSelected : BtnColor;
        }

        private void SetStatus(string s) { if (_statusText != null) _statusText.text = s; }

        // ---- builders ----

        private void BuildCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.09f, 0.12f);
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void BuildEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private Canvas BuildCanvas()
        {
            var go = new GameObject("Canvas");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private void BuildTopBar(Transform canvas)
        {
            var bar = NewPanel("TopBar", canvas, new Color(0f, 0f, 0f, 0.4f));
            Stretch(bar.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 200f), new Vector2(0f, 0f));

            _budgetText = BuildText(bar.transform, "Budget", 38f, FontStyles.Bold, TextAlignmentOptions.Left);
            Stretch(_budgetText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-360f, 48f), new Vector2(28f, -10f));

            _statusText = BuildText(bar.transform, "Status", 25f, FontStyles.Normal, TextAlignmentOptions.Left);
            _statusText.color = new Color(0.8f, 0.85f, 1f);
            Stretch(_statusText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-360f, 40f), new Vector2(28f, -62f));

            _resultText = BuildText(bar.transform, "Result", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
            Stretch(_resultText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(-40f, 48f), new Vector2(28f, -112f));
            _resultText.text = "";

            var waveBtn = MakeButton(bar.transform, "웨이브 실행", 300f, 92f, OnRunWave);
            waveBtn.GetComponent<Image>().color = new Color(0.55f, 0.18f, 0.18f, 0.98f);
            Stretch(waveBtn.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(300f, 92f), new Vector2(-28f, -16f));

            SetStatus("보유몹 선택 → 방 클릭 배치 → ⚔ 웨이브 실행");
        }

        private void BuildRoomsArea(Transform canvas)
        {
            var area = NewPanel("RoomsArea", canvas, new Color(0f, 0f, 0f, 0f));
            Stretch(area.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-40f, 0f), new Vector2(0f, 0f));
            // leave room for top bar (130) and inventory (170)
            var art = (RectTransform)area.transform;
            art.offsetMax = new Vector2(art.offsetMax.x, -220f);
            art.offsetMin = new Vector2(art.offsetMin.x, 190f);

            var row = area.AddComponent<HorizontalLayoutGroup>();
            row.spacing = 22f;
            row.childAlignment = TextAnchor.UpperCenter;
            row.childControlWidth = true; row.childControlHeight = true;
            row.childForceExpandWidth = false; row.childForceExpandHeight = true;

            for (int i = 0; i < _roomIds.Count; i++)
            {
                var roomId = _roomIds[i];
                var panel = NewPanel($"Room{i}", area.transform, RoomBg);
                var ple = panel.AddComponent<LayoutElement>(); ple.preferredWidth = 260f;
                var vlg = panel.AddComponent<VerticalLayoutGroup>();
                vlg.spacing = 6f; vlg.padding = new RectOffset(10, 10, 10, 10);
                vlg.childAlignment = TextAnchor.UpperCenter;
                vlg.childControlWidth = true; vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

                var header = MakeButton(panel.transform, $"방{i + 1}   ＋배치", 0f, 56f, () => OnPlaceInRoom(roomId));
                header.GetComponent<Image>().color = new Color(0.18f, 0.28f, 0.22f, 0.95f);

                var mobList = new GameObject("MobList", typeof(RectTransform));
                mobList.transform.SetParent(panel.transform, false);
                var mlvlg = mobList.AddComponent<VerticalLayoutGroup>();
                mlvlg.spacing = 4f; mlvlg.childAlignment = TextAnchor.UpperCenter;
                mlvlg.childControlWidth = true; mlvlg.childControlHeight = true;
                mlvlg.childForceExpandWidth = true; mlvlg.childForceExpandHeight = false;
                var mle = mobList.AddComponent<LayoutElement>(); mle.flexibleHeight = 1f;
                _roomMobLists.Add(mobList.transform);
            }

            // Core marker (display only).
            var core = NewPanel("Core", area.transform, CoreBg);
            var cle = core.AddComponent<LayoutElement>(); cle.preferredWidth = 130f;
            var coreTxt = BuildText(core.transform, "CoreLabel", 32f, FontStyles.Bold, TextAlignmentOptions.Center);
            coreTxt.text = "코어\n(표시만)";
            Stretch(coreTxt.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private void BuildInventory(Transform canvas)
        {
            var inv = NewPanel("Inventory", canvas, new Color(0f, 0f, 0f, 0.35f));
            Stretch(inv.transform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 170f), new Vector2(0f, 0f));
            var hlg = inv.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 12f; hlg.padding = new RectOffset(20, 20, 16, 16);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;

            for (int i = 0; i < _catalog.Count; i++)
            {
                var def = _catalog[i];
                int captured = i;
                var b = MakeButton(inv.transform, $"{def.DisplayName}\nC{def.Cost}", 150f, 110f, () => OnSelectMob(captured));
                _invButtons.Add(b);
            }
        }

        // ---- UI primitives ----

        private GameObject NewPanel(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = color.a > 0.001f;
            return go;
        }

        private Button MakeButton(Transform parent, string label, float prefW, float prefH, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Btn", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = BtnColor;
            var le = go.AddComponent<LayoutElement>();
            if (prefW > 0f) le.preferredWidth = prefW;
            le.preferredHeight = prefH; le.minHeight = prefH;
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(onClick);

            var t = BuildText(go.transform, "Label", 26f, FontStyles.Normal, TextAlignmentOptions.Center);
            t.text = label;
            Stretch(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            return btn;
        }

        private TMP_Text BuildText(Transform parent, string name, float size, FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = align;
            t.color = Color.white;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            if (koreanFont != null) t.font = koreanFont;
            return t;
        }

        private static void Stretch(Transform tr, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Vector2 anchoredPos)
        {
            var rt = (RectTransform)tr;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.sizeDelta = sizeDelta; rt.anchoredPosition = anchoredPos;
        }
    }
}
