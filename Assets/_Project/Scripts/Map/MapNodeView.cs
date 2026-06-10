using UnityEngine;
using UnityEngine.UI;

public class MapNodeView : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image iconImage;
    [SerializeField] private Text labelText;
    [SerializeField] private Color availableColor = new(1f, 0.9f, 0.25f);
    [SerializeField] private Color lockedColor = new(0.35f, 0.35f, 0.4f);
    [SerializeField] private Color visitedColor = new(0.25f, 0.95f, 0.45f);
    [SerializeField] private Color currentColor = new(0.25f, 0.65f, 1f);

    private MapNode node;
    private MapSystem mapSystem;

    private void Awake()
    {
        if (button == null) button = GetComponent<Button>();
        if (iconImage == null) iconImage = GetComponent<Image>();
        if (labelText == null) labelText = GetComponentInChildren<Text>();
    }

    public void Setup(MapNode node, MapSystem mapSystem)
    {
        this.node = node;
        this.mapSystem = mapSystem;

        if (labelText != null)
        {
            labelText.text = GetLabel(node.Type);
        }

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnClick);
        }

        Refresh();
    }

    public void Refresh()
    {
        if (node == null) return;

        bool isCurrent = RunManager.Instance != null && RunManager.Instance.CurrentMapNodeId == node.Id;

        if (button != null)
        {
            button.interactable = node.IsAvailable && !node.IsVisited;
        }

        if (iconImage == null) return;

        if (isCurrent)
        {
            iconImage.color = currentColor;
        }
        else if (node.IsVisited)
        {
            iconImage.color = visitedColor;
        }
        else if (node.IsAvailable)
        {
            iconImage.color = availableColor;
        }
        else
        {
            iconImage.color = lockedColor;
        }
    }

    private void OnClick()
    {
        mapSystem?.SelectNode(node);
    }

    private static string GetLabel(MapNodeType type)
    {
        return type switch
        {
            MapNodeType.Start => "START",
            MapNodeType.Enemy => "ENEMY",
            MapNodeType.Elite => "ELITE",
            MapNodeType.Event => "EVENT",
            MapNodeType.Shop => "SHOP",
            MapNodeType.Rest => "REST",
            MapNodeType.Treasure => "CHEST",
            MapNodeType.Boss => "BOSS",
            _ => type.ToString().ToUpperInvariant()
        };
    }
}
