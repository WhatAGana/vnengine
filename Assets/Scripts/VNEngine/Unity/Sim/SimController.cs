using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace VNEngine.Unity
{
    public sealed class SimController : MonoBehaviour
    {
        [Header("Definitions (ScriptableObjects)")]
        [SerializeField] private List<ResourceDefinitionSO> resources = new List<ResourceDefinitionSO>();
        [SerializeField] private List<CommandDefinitionSO> commands = new List<CommandDefinitionSO>();

        [Header("UI")]
        [SerializeField] private TMP_Text statusText;
        [SerializeField] private Transform buttonContainer;
        [SerializeField] private Button buttonPrefab;
        [SerializeField] private Button newLoopButton; // "새 회차" — 없으면 무시
        [SerializeField] private Button saveButton;    // "세이브" — 없으면 무시
        [SerializeField] private Button loadButton;    // "로드" — 없으면 무시
        [SerializeField] private int saveSlot = 0;

        private TurnEngine _turnEngine;
        private LoopEngine _loop;
        private CampaignState _campaign;

        private void Start()
        {
            var resDefs = new List<ResourceDef>(resources.Count);
            foreach (var r in resources) resDefs.Add(r.ToDef());

            var cmdDefs = new List<CommandDef>(commands.Count);
            foreach (var c in commands) cmdDefs.Add(c.ToDef());

            _turnEngine = new TurnEngine(resDefs, cmdDefs); // 배선 오류면 여기서 VnRuntimeException → 콘솔 에러
            _loop = new LoopEngine(_turnEngine);
            _campaign = _loop.CreateInitialCampaign();

            BuildButtons();

            if (newLoopButton != null)
            {
                newLoopButton.onClick.RemoveAllListeners();
                newLoopButton.onClick.AddListener(OnNewLoop);
            }
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

        private void BuildButtons()
        {
            if (buttonPrefab == null || buttonContainer == null)
            {
                Debug.LogError("[SimController] buttonPrefab or buttonContainer not assigned");
                return;
            }
            foreach (var c in _turnEngine.Commands)
            {
                string commandId = c.Id; // capture
                Button btn = Instantiate(buttonPrefab, buttonContainer);
                btn.name = $"CommandButton_{c.Id}";
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) tmp.text = c.DisplayName;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnCommand(commandId));
            }
        }

        private void OnCommand(string commandId)
        {
            _campaign = _loop.ExecuteCommand(_campaign, commandId);
            Refresh();
        }

        private void OnNewLoop()
        {
            _campaign = _loop.StartNewLoop(_campaign);
            Refresh();
        }

        private void OnSave()
        {
            CampaignSaveSystem.Write(saveSlot, CampaignSave.Capture(_campaign));
            Debug.Log($"[SimController] saved slot {saveSlot}: 회차{_campaign.Meta.LoopCount}/일차{_campaign.Run.Day}");
        }

        private void OnLoad()
        {
            var data = CampaignSaveSystem.Read(saveSlot);
            if (data == null) { Debug.Log($"[SimController] no save in slot {saveSlot}"); return; }
            _campaign = CampaignSave.Restore(data);
            Refresh();
        }

        private void Refresh()
        {
            if (statusText == null) return;
            var sb = new StringBuilder();
            sb.Append("회차: ").Append(_campaign.Meta.LoopCount);
            sb.Append("    일차: ").Append(_campaign.Run.Day);
            foreach (var r in _turnEngine.Resources)
                sb.Append("    ").Append(r.DisplayName).Append(": ").Append(_campaign.Run.Resources[r.Id]);
            statusText.text = sb.ToString();
        }
    }
}
