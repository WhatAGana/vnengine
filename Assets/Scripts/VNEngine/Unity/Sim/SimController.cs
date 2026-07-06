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

        private TurnEngine _engine;
        private RunState _state;

        private void Start()
        {
            var resDefs = new List<ResourceDef>(resources.Count);
            foreach (var r in resources) resDefs.Add(r.ToDef());

            var cmdDefs = new List<CommandDef>(commands.Count);
            foreach (var c in commands) cmdDefs.Add(c.ToDef());

            _engine = new TurnEngine(resDefs, cmdDefs); // 배선 오류면 여기서 VnRuntimeException → 콘솔 에러
            _state = _engine.CreateInitialState();

            BuildButtons();
            Refresh();
        }

        private void BuildButtons()
        {
            if (buttonPrefab == null || buttonContainer == null)
            {
                Debug.LogError("[SimController] buttonPrefab or buttonContainer not assigned");
                return;
            }
            foreach (var c in _engine.Commands)
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
            _state = _engine.ExecuteCommand(_state, commandId);
            Refresh();
        }

        private void Refresh()
        {
            if (statusText == null) return;
            var sb = new StringBuilder();
            sb.Append("주차: ").Append(_state.Day);
            foreach (var r in _engine.Resources)
                sb.Append("    ").Append(r.DisplayName).Append(": ").Append(_state.Resources[r.Id]);
            statusText.text = sb.ToString();
        }
    }
}
