using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
// using Code.Scripts.Managers;
using DG.Tweening;

[System.Serializable]
public struct NPCDialogueLine
{
    public string speakerName;
    [TextArea(3, 5)]
    public string dialogueText;
    public Sprite speakerImageA;
    public Sprite speakerImageB;
    public bool isSpeakerAActive;
    public bool disableClickToAdvance;
}

public class NPCInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionRadius = 2f;
    [SerializeField] private GameObject interactIndicator;

    [Header("Dialogue Settings")]
    [SerializeField] private List<NPCDialogueLine> dialogueLines;
    [SerializeField] private GameObject dialogueUI;
    [SerializeField] private Image speakerImageA;
    [SerializeField] private Image speakerImageB;
    [SerializeField] private GameObject skipButton;

    [Header("Speaker B Dialogue Box")]
    [SerializeField] private GameObject dialogueUIB;

    // Auto-resolved from children named "Name" and "Dialogue Lines"
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private TextMeshProUGUI dialogueText;
    // [SerializeField] private TextMeshProUGUI speakerTextB;
    // [SerializeField] private TextMeshProUGUI dialogueTextB;

    [Header("Skip Dialogue")]
    [SerializeField] private GameObject skipPanel;
    [SerializeField] private TextMeshProUGUI skipSummaryText;
    [SerializeField][TextArea(2, 5)] private string skipSummary;

    [Header("Typing Settings")]
    [SerializeField] private float typingSpeed = 0.03f;

    [Header("Patrol Settings")]
    [SerializeField] private Transform patrolPoint;
    [SerializeField] private float patrolSpeed = 2f;

    [Header("Auto Dialogue Trigger")]
    [SerializeField] private bool autoStartDialogue = false;
    [SerializeField] private bool oneTimeOnly = false;
    [SerializeField] private float autoStartDelay = 3.0f;

    [SerializeField] private GameObject blurPanel;

    [Header("Animation Settings")]
    [SerializeField] private RectTransform dialogueBoxRect;
    [SerializeField] private float animationDuration = 0.5f;
    [SerializeField] private Vector2 dialogueBoxStartOffset = new Vector2(0, -200f);
    [SerializeField] private Vector2 speakerAStartOffset = new Vector2(-200f, 0);
    [SerializeField] private Vector2 speakerBStartOffset = new Vector2(200f, 0);

    [Header("Events")]
    public UnityEngine.Events.UnityEvent onDialogueFinished;
    [SerializeField] private string nextSceneName;

    [Header("SFX")]
    [SerializeField] private Gameseed26.SfxID dialogueAdvanceSFX;

    // [Header("Reference")]
    // [SerializeField] GameManager gameManager;

    public bool IsDialogueActive => isDialogueActive;

    private bool isPlayerInRange = false;
    private bool isPatrolling = false;
    private bool isTyping = false;
    private bool currentLineAllowsClickToAdvance = true;
    private bool isSkipPanelOpen = false;
    private bool isDialogueActive = false;
    private bool hasTriggered = false;

    private string currentFullLine = "";
    private TextMeshProUGUI _activeDialogueText;
    private Coroutine typingCoroutine;
    private Coroutine autoStartCoroutine;
    private Queue<NPCDialogueLine> dialogueQueue;

    // private float previousTimeScale = 1f;

    // --- FIX START: Variables to store your Inspector Scale ---
    private Vector3 defaultScaleA;
    private Vector3 defaultScaleB;
    // --- FIX END ---
    
    private Vector2 originalDialogueBoxPos;
    private Vector2 originalSpeakerAPos;
    private Vector2 originalSpeakerBPos;
    private bool isSpeakerAVisible = false;
    private bool isSpeakerBVisible = false;

    private TextMeshProUGUI FindTextRecursive(Transform parent, string name)
    {
        var allTexts = parent.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var text in allTexts)
        {
            if (text.gameObject.name == name)
            {
                return text;
            }
        }
        return null;
    }

    private void Awake()
    {
        // gameManager = FindFirstObjectByType<GameManager>();
        dialogueQueue = new Queue<NPCDialogueLine>();
        dialogueUI.SetActive(false);
        //if (dialogueUIB != null) dialogueUIB.SetActive(false);

        // Auto-find text components from named children (now searches recursively)
        speakerText  = FindTextRecursive(dialogueUI.transform, "Name");
        dialogueText = FindTextRecursive(dialogueUI.transform, "Dialogue Lines");
        if (dialogueUIB != null)
        {
            dialogueUIB.SetActive(false); // Hide B if it exists, as we use the same for both now
        }

        if (interactIndicator != null)
            interactIndicator.SetActive(false);

        // --- FIX START: Remember the scale you set in Inspector (e.g., 5) ---
        if (speakerImageA != null) defaultScaleA = speakerImageA.transform.localScale;
        if (speakerImageB != null) defaultScaleB = speakerImageB.transform.localScale;
        // --- FIX END ---

        if (dialogueBoxRect != null) originalDialogueBoxPos = dialogueBoxRect.anchoredPosition;
        if (speakerImageA != null) originalSpeakerAPos = speakerImageA.rectTransform.anchoredPosition;
        if (speakerImageB != null) originalSpeakerBPos = speakerImageB.rectTransform.anchoredPosition;

        if (speakerImageA != null) speakerImageA.enabled = false;
        if (speakerImageB != null) speakerImageB.enabled = false;
    }

    private void Start()
    {
        Debug.Log($"[NPCInteraction] Start() called on {gameObject.name}. autoStartDialogue: {autoStartDialogue}, hasTriggered: {hasTriggered}");
        if (autoStartDialogue && !hasTriggered)
        {
            autoStartCoroutine = StartCoroutine(WaitAndStartDialogue());
        }
    }

    private IEnumerator WaitAndStartDialogue()
    {
        Debug.Log($"[NPCInteraction] WaitAndStartDialogue() started. Waiting for {autoStartDelay} seconds.");
        yield return new WaitForSecondsRealtime(autoStartDelay);

        Debug.Log($"[NPCInteraction] Wait finished. isDialogueActive: {isDialogueActive}, oneTimeOnly: {oneTimeOnly}, hasTriggered: {hasTriggered}");
        if (!isDialogueActive && (!oneTimeOnly || !hasTriggered))
        {
            StartDialogue();
        }

        autoStartCoroutine = null;
    }

    public void OnInteractButtonClicked()
    {
        if (!isDialogueActive && isPlayerInRange)
        {
            StartDialogue();
        }
    }

    private void Update()
    {
        if (isSkipPanelOpen) return;

        // Advance Dialogue
        if (isDialogueActive)
        {
            bool advancePressed = (currentLineAllowsClickToAdvance && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) ||
                                  (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame));
            if (advancePressed)
            {
                Debug.Log("[NPCInteraction] Advance pressed in Update!");
                if (isTyping)
                {
                    Debug.Log("[NPCInteraction] Skipping typing...");
                    if (typingCoroutine != null) StopCoroutine(typingCoroutine);
                    if (_activeDialogueText != null) _activeDialogueText.text = currentFullLine;
                    isTyping = false;
                    typingCoroutine = null;
                }
                else
                {
                    Debug.Log("[NPCInteraction] Advancing to next sentence...");
                    Gameseed26.Tune.SFX(dialogueAdvanceSFX);
                    DisplayNextSentence();
                }
            }
            return;
        }

        // Manual Start
        if (isPlayerInRange && Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame)
        {
            StartDialogue();
        }

        // Patrol
        if (isPatrolling)
        {
            float step = patrolSpeed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, patrolPoint.position, step);

            if (Vector3.Distance(transform.position, patrolPoint.position) < 0.01f)
            {
                isPatrolling = false;
            }
        }
    }

    public void StartDialogue()
    {
        Debug.Log($"[NPCInteraction] StartDialogue() called on {gameObject.name}.");
        if (autoStartCoroutine != null)
        {
            StopCoroutine(autoStartCoroutine);
            autoStartCoroutine = null;
        }

        if (hasTriggered && oneTimeOnly)
        {
            Debug.LogWarning($"[NPCInteraction] Aborted: hasTriggered is true and oneTimeOnly is true.");
            return;
        }

        if (dialogueLines == null || dialogueLines.Count == 0)
        {
            Debug.LogWarning($"[NPCInteraction] Aborted: dialogueLines list is empty. Please add lines in the Inspector.");
            return;
        }
        
        if (dialogueUI == null)
        {
            Debug.LogError($"[NPCInteraction] Aborted: dialogueUI is not assigned in the Inspector!");
            return;
        }

        Debug.Log($"[NPCInteraction] Successfully starting dialogue with {dialogueLines.Count} lines.");

        // previousTimeScale = Time.timeScale;
        // Time.timeScale = 0f;

        isDialogueActive = true;
        dialogueQueue.Clear();

        foreach (var line in dialogueLines)
            dialogueQueue.Enqueue(line);

        dialogueUI.SetActive(true);
        if (interactIndicator != null) interactIndicator.SetActive(false);
        if (blurPanel != null) blurPanel.SetActive(true);
        if (skipButton != null) skipButton.SetActive(true);

        isSpeakerAVisible = false;
        isSpeakerBVisible = false;

        if (dialogueBoxRect != null)
        {
            dialogueBoxRect.anchoredPosition = originalDialogueBoxPos + dialogueBoxStartOffset;
            dialogueBoxRect.DOAnchorPos(originalDialogueBoxPos, animationDuration).SetUpdate(true).SetEase(Ease.OutBack);
        }

        DisplayNextSentence();
    }

    private void DisplayNextSentence()
    {
        Debug.Log($"[NPCInteraction] DisplayNextSentence called. Queue count: {dialogueQueue.Count}");
        if (dialogueQueue.Count == 0)
        {
            Debug.Log("[NPCInteraction] Queue is empty, calling EndDialogue.");
            EndDialogue();
            return;
        }

        var currentLine = dialogueQueue.Dequeue();
        currentLineAllowsClickToAdvance = !currentLine.disableClickToAdvance;

        bool speakerAActive = currentLine.isSpeakerAActive;

        // Show the dialogue box (same for both speakers)
        dialogueUI.SetActive(true);
        if (dialogueUIB != null) dialogueUIB.SetActive(false);

        // Route name + body text to the active box
        TextMeshProUGUI activeSpeakerLabel = speakerText;
        _activeDialogueText = dialogueText;

        if (activeSpeakerLabel != null) activeSpeakerLabel.text = currentLine.speakerName;
        currentFullLine = currentLine.dialogueText;

        if (typingCoroutine != null) StopCoroutine(typingCoroutine);
        if (_activeDialogueText != null) typingCoroutine = StartCoroutine(TypeSentence(currentFullLine));

        // --- FIX START: Use the stored defaultScale instead of Vector3.one ---

        Color bright = Color.white;
        Color dim = new Color(0.8f, 0.8f, 0.8f, 1f);

        if (currentLine.speakerImageA != null)
        {
            speakerImageA.enabled = true;
            speakerImageA.sprite = currentLine.speakerImageA;

            if (!isSpeakerAVisible)
            {
                speakerImageA.rectTransform.anchoredPosition = originalSpeakerAPos + speakerAStartOffset;
                speakerImageA.rectTransform.DOAnchorPos(originalSpeakerAPos, animationDuration).SetUpdate(true).SetEase(Ease.OutBack);
                isSpeakerAVisible = true;
            }

            // Calculate dimmed scale based on YOUR default scale (e.g. 5 becomes 4.5)
            Vector3 dimmedScaleA = defaultScaleA * 0.9f;

            speakerImageA.transform.localScale = currentLine.isSpeakerAActive ? defaultScaleA : dimmedScaleA;
            speakerImageA.color = currentLine.isSpeakerAActive ? bright : dim;
        }
        else
        {
            speakerImageA.enabled = false;
            isSpeakerAVisible = false;
        }

        if (currentLine.speakerImageB != null)
        {
            speakerImageB.enabled = true;
            speakerImageB.sprite = currentLine.speakerImageB;

            if (!isSpeakerBVisible)
            {
                speakerImageB.rectTransform.anchoredPosition = originalSpeakerBPos + speakerBStartOffset;
                speakerImageB.rectTransform.DOAnchorPos(originalSpeakerBPos, animationDuration).SetUpdate(true).SetEase(Ease.OutBack);
                isSpeakerBVisible = true;
            }

            // Calculate dimmed scale based on YOUR default scale
            Vector3 dimmedScaleB = defaultScaleB * 0.9f;

            speakerImageB.transform.localScale = currentLine.isSpeakerAActive ? dimmedScaleB : defaultScaleB;
            speakerImageB.color = currentLine.isSpeakerAActive ? dim : bright;
        }
        else
        {
            speakerImageB.enabled = false;
            isSpeakerBVisible = false;
        }
        // --- FIX END ---
    }

    private void EndDialogue()
    {
        Debug.Log("[NPCInteraction] EndDialogue called!");
        // Time.timeScale = previousTimeScale;

        isDialogueActive = false;
        dialogueUI.SetActive(false);
        if (dialogueUIB != null) dialogueUIB.SetActive(false);
        if (skipButton != null) skipButton.SetActive(false);
        if (blurPanel != null) blurPanel.SetActive(false);

        if (speakerText != null) speakerText.text = "";
        if (dialogueText != null) dialogueText.text = "";

        speakerImageA.enabled = false;
        speakerImageB.enabled = false;

        hasTriggered = oneTimeOnly || hasTriggered;

        if (interactIndicator != null && isPlayerInRange)
        {
            if (oneTimeOnly && hasTriggered && patrolPoint != null)
            {
                interactIndicator.SetActive(false);
            }
            else if (!(oneTimeOnly && hasTriggered))
            {
                interactIndicator.SetActive(true);
            }
        }

        if (oneTimeOnly && patrolPoint != null)
        {
            isPatrolling = true;
        }

        onDialogueFinished?.Invoke();

        if (!string.IsNullOrEmpty(nextSceneName))
            Gameseed26.SceneLoader.LoadScene(nextSceneName);

        // gameManager.GameStart();
    }

    public void OnSkipButtonPressed()
    {
        if (!isDialogueActive) return;

        if (skipPanel != null)
        {
            skipPanel.SetActive(true);
            skipSummaryText.text = skipSummary;
            isSkipPanelOpen = true;

            if (skipButton != null) skipButton.SetActive(false);
        }
    }

    public void ForceEndDialogue()
    {
        if (!isDialogueActive) return;

        if (typingCoroutine != null)
        {
            StopCoroutine(typingCoroutine);
            typingCoroutine = null;
        }

        dialogueQueue.Clear();
        EndDialogue();
    }

    public void ConfirmSkip()
    {
        isSkipPanelOpen = false;

        while (dialogueQueue.Count > 0)
            dialogueQueue.Dequeue();

        EndDialogue();

        if (skipPanel != null) skipPanel.SetActive(false);
    }

    public void CancelSkip()
    {
        isSkipPanelOpen = false;

        if (skipPanel != null) skipPanel.SetActive(false);
        if (skipButton != null) skipButton.SetActive(true);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        isPlayerInRange = true;

        if (interactIndicator != null)
            interactIndicator.SetActive(true);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            isPlayerInRange = false;

            if (interactIndicator != null)
                interactIndicator.SetActive(false);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }

    private IEnumerator TypeSentence(string sentence)
    {
        isTyping = true;
        _activeDialogueText.text = "";

        if (_activeDialogueText != null)
        {
            foreach (char letter in sentence.ToCharArray())
            {
                _activeDialogueText.text += letter;
                yield return new WaitForSecondsRealtime(typingSpeed);
            }
        }

        isTyping = false;
        typingCoroutine = null;
    }
}