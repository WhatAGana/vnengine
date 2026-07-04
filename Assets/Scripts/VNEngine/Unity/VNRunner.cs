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

        private IEnumerator Start()
        {
            if (dialogueView == null || stageView == null)
            {
                Debug.LogError("[VNRunner] dialogueView and stageView must be assigned");
                yield break;
            }

            VnProgram program = null;
            string loadError = null;
            try
            {
                program = VnScriptLoader.LoadAndCompile(scriptsResourcesFolder);
            }
            catch (VnException e) { loadError = e.Message; }

            if (loadError != null)
            {
                Debug.LogError($"[VNRunner] script load/compile failed: {loadError}");
                dialogueView.ShowLine(null, null, "[script load failed]");
                yield break;
            }

            var state = new GameState(new SeededRandom(randomSeed));
            _interp = new Interpreter(program, state, dialogueView, stageView);

            string startError = null;
            try { _interp.Start(entryLabel); }
            catch (VnException e) { startError = e.Message; }
            if (startError != null)
            {
                Debug.LogError($"[VNRunner] {startError}");
                yield break;
            }

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
    }
}
