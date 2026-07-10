using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VNEngine.Unity
{
    // Smoke-test harness: builds a full, playable VN rig in code (camera, EventSystem,
    // background renderer, character slots, dialogue canvas + TMP texts) and wires
    // VNRunner + StageViewUnity + DialogueViewUnity so that pressing Play renders
    // Assets/Resources/opening/opening_1회차.vns on screen. Purpose = verify the
    // vns -> screen pipeline (background, standing, click-to-advance, expression swap).
    //
    // Name -> image mapping (per request):
    //   bg  inn        -> Resources/img/Background/inn
    //   미갈무표정      -> Resources/img/character/Michal/front_expressionless_sample
    //   미갈미소        -> Resources/img/character/Michal/front_light_smile_sample
    //   미갈난처        -> Resources/img/character/Michal/front_annoyed_sample
    public class OpeningSmokeBootstrap : MonoBehaviour
    {
        [Tooltip("Korean-capable TMP font (assign NotoSansKR-Regular SDF). If null, Korean text renders as boxes.")]
        public TMP_FontAsset koreanFont;

        [Tooltip("Auto-advance dialogue on a timer instead of waiting for clicks (for automated smoke capture). Leave OFF for manual click-through.")]
        public bool autoAdvance = false;
        public float autoAdvanceInterval = 1.4f;

        [Header("Stage tuning")]
        public float cameraOrthoSize = 5.12f;   // half of bg height (1024px / 100ppu = 10.24u)
        public float backgroundScale = 1.25f;   // cover a 16:9 view with the 1536x1024 bg
        public float characterScale = 0.8f;      // 868px / 100ppu * 0.8 ≈ 6.9u tall

        private DialogueViewUnity _dialogue;

        private void Awake()
        {
            // Build the whole rig under an inactive root so child Awake()/Start() run
            // only AFTER we've wired every serialized reference. DialogueViewUnity and
            // VNRunner both validate their refs in Awake/Start and bail if unset.
            var rig = new GameObject("VNRig");
            rig.SetActive(false);

            BuildCamera();
            BuildEventSystem();

            var bgRenderer = BuildBackground(rig.transform);
            var (left, center, right) = BuildSlots(rig.transform);
            var (panel, speaker, dialogueText) = BuildDialogueCanvas(rig.transform);

            // --- StageViewUnity ---
            var stage = rig.AddComponent<StageViewUnity>();
            stage.background = bgRenderer;
            stage.leftSlot = left;
            stage.centerSlot = center;
            stage.rightSlot = right;
            stage.characterScale = characterScale;
            stage.backgrounds = new List<StageViewUnity.BackgroundEntry>
            {
                Bg("inn", "img/Background/inn"),
            };
            stage.characters = new List<StageViewUnity.CharacterEntry>
            {
                Char("미갈무표정", "img/character/Michal/front_expressionless_sample"),
                Char("미갈미소",   "img/character/Michal/front_light_smile_sample"),
                Char("미갈난처",   "img/character/Michal/front_annoyed_sample"),
            };

            // --- DialogueViewUnity ---
            _dialogue = rig.AddComponent<DialogueViewUnity>();
            _dialogue.dialoguePanel = panel;
            _dialogue.speakerText = speaker;
            _dialogue.dialogueText = dialogueText;
            _dialogue.typingSpeed = 0.02f;

            // --- VNRunner ---
            var runner = rig.AddComponent<VNRunner>();
            runner.dialogueView = _dialogue;
            runner.stageView = stage;
            runner.scriptsResourcesFolder = "opening";
            runner.entryLabel = "오프닝_시작";

            // Everything wired: activate -> Awake(views) -> Start(runner) runs the script.
            rig.SetActive(true);

            if (autoAdvance) StartCoroutine(AutoAdvance());
        }

        private IEnumerator AutoAdvance()
        {
            var wait = new WaitForSeconds(autoAdvanceInterval);
            while (true)
            {
                yield return wait;
                // OnAdvanceClick is private; SendMessage reaches it. First hit finishes
                // typing, a follow-up advances — fire twice to reliably step one line.
                if (_dialogue != null)
                {
                    _dialogue.SendMessage("OnAdvanceClick", SendMessageOptions.DontRequireReceiver);
                    yield return new WaitForSeconds(0.25f);
                    _dialogue.SendMessage("OnAdvanceClick", SendMessageOptions.DontRequireReceiver);
                }
            }
        }

        // ---- builders ----

        private void BuildCamera()
        {
            if (Camera.main != null) return;
            var go = new GameObject("Main Camera");
            go.tag = "MainCamera";
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = cameraOrthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            go.transform.position = new Vector3(0f, 0f, -10f);
        }

        private void BuildEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        private SpriteRenderer BuildBackground(Transform parent)
        {
            var go = new GameObject("Background");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, 0f, 10f);
            go.transform.localScale = Vector3.one * backgroundScale;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 0;
            return sr;
        }

        private (Transform, Transform, Transform) BuildSlots(Transform parent)
        {
            Transform Slot(string name, float x)
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = new Vector3(x, -0.6f, 0f);
                return go.transform;
            }
            return (Slot("LeftSlot", -5f), Slot("CenterSlot", 0f), Slot("RightSlot", 5f));
        }

        private (GameObject panel, TMP_Text speaker, TMP_Text body) BuildDialogueCanvas(Transform parent)
        {
            var canvasGo = new GameObject("Canvas");
            canvasGo.transform.SetParent(parent, false);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            // Dialogue panel: bottom strip, semi-transparent dark.
            var panelGo = new GameObject("DialoguePanel");
            panelGo.transform.SetParent(canvasGo.transform, false);
            var panelImg = panelGo.AddComponent<Image>();
            panelImg.color = new Color(0f, 0f, 0f, 0.72f);
            panelImg.raycastTarget = false; // let world clicks reach DialogueView.Update()
            var pr = panelGo.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(0f, 0f);
            pr.anchorMax = new Vector2(1f, 0f);
            pr.pivot = new Vector2(0.5f, 0f);
            pr.sizeDelta = new Vector2(0f, 300f);
            pr.anchoredPosition = new Vector2(0f, 0f);

            var speaker = BuildText(panelGo.transform, "Speaker", 40f, FontStyles.Bold);
            var sr = speaker.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(1f, 1f);
            sr.pivot = new Vector2(0f, 1f);
            sr.sizeDelta = new Vector2(-80f, 52f);
            sr.anchoredPosition = new Vector2(40f, -14f);
            speaker.alignment = TextAlignmentOptions.TopLeft;

            var body = BuildText(panelGo.transform, "Dialogue", 34f, FontStyles.Normal);
            var br = body.rectTransform;
            br.anchorMin = new Vector2(0f, 0f);
            br.anchorMax = new Vector2(1f, 1f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.offsetMin = new Vector2(40f, 24f);
            br.offsetMax = new Vector2(-40f, -70f);
            body.alignment = TextAlignmentOptions.TopLeft;

            return (panelGo, speaker, body);
        }

        private TMP_Text BuildText(Transform parent, string name, float size, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = style;
            t.color = Color.white;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            if (koreanFont != null) t.font = koreanFont;
            return t;
        }

        private static StageViewUnity.BackgroundEntry Bg(string name, string resPath)
            => new StageViewUnity.BackgroundEntry { name = name, sprite = LoadSprite(resPath) };

        private static StageViewUnity.CharacterEntry Char(string name, string resPath)
            => new StageViewUnity.CharacterEntry { name = name, sprite = LoadSprite(resPath) };

        private static Sprite LoadSprite(string resPath)
        {
            var s = Resources.Load<Sprite>(resPath);
            if (s == null) Debug.LogError($"[OpeningSmoke] sprite not found at Resources/{resPath}");
            return s;
        }
    }
}
