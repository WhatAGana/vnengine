using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VNEngine.Unity
{
    public class DialogueViewUnity : MonoBehaviour, IDialogueView
    {
        [Header("UI References")]
        public GameObject dialoguePanel;
        public TMP_Text speakerText;
        public TMP_Text dialogueText;

        [Header("Choices")]
        public Button choiceButtonPrefab;
        public Transform choicesContainer;

        [Header("Typing")]
        [Range(0.005f, 0.2f)] public float typingSpeed = 0.04f;

        private Coroutine _typing;
        private bool _isTyping;
        private string _fullText = "";
        private bool _lineComplete;

        private readonly List<GameObject> _choiceButtons = new List<GameObject>();
        private bool _hasChoice;
        private int _chosen = -1;
        private bool _choicesShowing;

        private void Awake()
        {
            if (choicesContainer != null) choicesContainer.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_choicesShowing) return; // buttons handle input while choosing
            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
                OnAdvanceClick();
            }
        }

        private void OnAdvanceClick()
        {
            if (_isTyping)
            {
                if (_typing != null) StopCoroutine(_typing);
                dialogueText.text = _fullText;
                _isTyping = false;
                return; // first click finishes typing; a second click advances
            }
            _lineComplete = true;
        }

        // ---- IDialogueView ----

        public void ShowLine(string speakerName, string colorHex, string text)
        {
            _lineComplete = false;
            if (dialoguePanel != null) dialoguePanel.SetActive(true);

            if (speakerText != null)
            {
                speakerText.text = speakerName ?? "";
                if (!string.IsNullOrEmpty(colorHex) && ColorUtility.TryParseHtmlString(colorHex, out var col))
                    speakerText.color = col;
                else
                    speakerText.color = Color.white;
            }

            _fullText = text ?? "";
            if (_typing != null) StopCoroutine(_typing);
            _typing = StartCoroutine(TypeLine(_fullText));
        }

        public bool IsLineComplete => _lineComplete;

        private IEnumerator TypeLine(string full)
        {
            _isTyping = true;
            dialogueText.text = "";
            for (int i = 0; i < full.Length; i++)
            {
                dialogueText.text += full[i];
                yield return new WaitForSeconds(typingSpeed);
            }
            _isTyping = false;
        }

        public void ShowChoices(IReadOnlyList<string> labels)
        {
            if (choiceButtonPrefab == null || choicesContainer == null)
            {
                Debug.LogError("[DialogueView] choiceButtonPrefab or choicesContainer not assigned");
                return;
            }
            ClearChoices();
            choicesContainer.gameObject.SetActive(true);
            _choicesShowing = true;

            for (int i = 0; i < labels.Count; i++)
            {
                int index = i; // capture
                Button btn = Instantiate(choiceButtonPrefab, choicesContainer);
                btn.name = $"ChoiceButton_{i}";
                var tmp = btn.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null) tmp.text = labels[i];
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => { _chosen = index; _hasChoice = true; });
                _choiceButtons.Add(btn.gameObject);
            }
        }

        public bool HasChoice => _hasChoice;
        public int ChosenIndex => _chosen;

        public void ClearChoices()
        {
            for (int i = 0; i < _choiceButtons.Count; i++)
                if (_choiceButtons[i] != null) Destroy(_choiceButtons[i]);
            _choiceButtons.Clear();
            if (choicesContainer != null) choicesContainer.gameObject.SetActive(false);
            _hasChoice = false;
            _chosen = -1;
            _choicesShowing = false;
        }
    }
}
