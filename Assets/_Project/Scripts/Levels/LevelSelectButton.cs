using NaughtyAttributes;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelSelectButton : MonoBehaviour
{
    [Header("References")]
    [BoxGroup("References")]
    [SerializeField] private Button button;
    [BoxGroup("References")]
    [SerializeField] private TMP_Text titleText;
    [BoxGroup("References")]
    [SerializeField] private TMP_Text statusText;
    [BoxGroup("References")]
    [SerializeField] private Image backgroundImage;

    [Header("Visual States")]
    [BoxGroup("Visual States")]
    [SerializeField] private Color unlockedColor = new(0.95f, 0.82f, 0.25f, 1f);
    [BoxGroup("Visual States")]
    [SerializeField] private Color completedColor = new(0.25f, 0.85f, 0.45f, 1f);
    [BoxGroup("Visual States")]
    [SerializeField] private Color lockedColor = new(0.32f, 0.32f, 0.36f, 1f);

    private LevelSelectController controller;
    private LevelDefinition level;

    private void Reset()
    {
        CacheReferences();
    }

    private void Awake()
    {
        CacheReferences();
    }

    public void Bind(LevelSelectController owner, LevelDefinition definition, bool unlocked, bool completed)
    {
        controller = owner;
        level = definition;
        CacheReferences();

        if (titleText != null)
        {
            titleText.text = definition != null ? definition.DisplayName : "Empty Level";
        }

        if (statusText != null)
        {
            statusText.text = completed ? "Completed" : unlocked ? "Unlocked" : "Locked";
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
            button.onClick.AddListener(HandleClicked);
            button.interactable = unlocked && definition != null;
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = completed ? completedColor : unlocked ? unlockedColor : lockedColor;
        }
    }

    private void HandleClicked()
    {
        controller?.StartLevel(level);
    }

    private void CacheReferences()
    {
        if (button == null) button = GetComponent<Button>();
        if (backgroundImage == null) backgroundImage = GetComponent<Image>();
        if (titleText == null) titleText = GetComponentInChildren<TMP_Text>(true);
    }
}
