using System.Collections;
using UnityEngine;

namespace VNEngine.Unity
{
    public class VNRunner : MonoBehaviour
    {
        [Header("References")]
        public DialogueViewUnity dialogueView;
        public StageViewUnity stageView;

        [Header("Script")]
        [Tooltip("Subfolder inside a Resources/ folder that holds the .vns TextAssets")]
        public string scriptsResourcesFolder = "scripts";
        public string entryLabel = "start";

        [Tooltip("Seed for random(); fixed for reproducible runs")]
        public int randomSeed = 12345;

        private Interpreter _interp;
        private GameState _state;
        private VnProgram _program;
        private string _programHash;
        private Coroutine _loop;

        private IEnumerator Start()
        {
            if (dialogueView == null || stageView == null)
            {
                Debug.LogError("[VNRunner] dialogueView and stageView must be assigned");
                yield break;
            }

            if (!BuildInterpreter()) yield break;

            string startError = null;
            try { _interp.Start(entryLabel); }
            catch (VnException e) { startError = e.Message; }
            if (startError != null) { Debug.LogError($"[VNRunner] {startError}"); yield break; }

            _loop = StartCoroutine(RunLoop());
        }

        // Loads+compiles the scripts and creates a fresh interpreter/state.
        private bool BuildInterpreter()
        {
            string loadError = null;
            try { _program = VnScriptLoader.LoadAndCompile(scriptsResourcesFolder, out _programHash); }
            catch (VnException e) { loadError = e.Message; }
            if (loadError != null)
            {
                Debug.LogError($"[VNRunner] script load/compile failed: {loadError}");
                dialogueView.ShowLine(null, null, "[script load failed]");
                return false;
            }
            _state = new GameState(new SeededRandom(randomSeed));
            _interp = new Interpreter(_program, _state, dialogueView, stageView);
            return true;
        }

        private IEnumerator RunLoop()
        {
            while (!_interp.IsFinished)
            {
                string tickError = null;
                try { _interp.Tick(); }
                catch (VnException e) { tickError = e.Message; }
                if (tickError != null)
                {
                    Debug.LogError($"[VNRunner] runtime error: {tickError}");
                    yield break;
                }
                yield return null;
            }
        }

        // Save the current run to a slot. Only valid while waiting for input.
        public bool SaveToSlot(int slot)
        {
            if (_interp == null || !_interp.IsWaiting)
            {
                Debug.LogWarning("[VNRunner] cannot save: not currently waiting for input");
                return false;
            }
            SaveSystem.Write(slot, _interp.CaptureSave(_programHash));
            return true;
        }

        // Restore a slot into a fresh interpreter and resume.
        public bool LoadFromSlot(int slot)
        {
            var data = SaveSystem.Read(slot);
            if (data == null) { Debug.LogWarning($"[VNRunner] no save in slot {slot}"); return false; }
            if (!SaveSystem.IsCompatible(data, _programHash))
            {
                Debug.LogWarning($"[VNRunner] save slot {slot} incompatible: script changed or version mismatch");
                return false;
            }

            if (_loop != null) { StopCoroutine(_loop); _loop = null; }
            if (!BuildInterpreter()) return false;
            _interp.RestoreSave(data);
            _loop = StartCoroutine(RunLoop());
            return true;
        }
    }
}
