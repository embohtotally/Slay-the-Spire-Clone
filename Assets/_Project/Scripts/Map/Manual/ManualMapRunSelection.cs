using UnityEngine;

using Gameseed26;
public class ManualMapRunSelection : PersistentSingleton<ManualMapRunSelection>
{
    public string SelectedLayoutId { get; private set; }
    public bool HasSelectedLayout => !string.IsNullOrWhiteSpace(SelectedLayoutId);

    public void SelectLayout(string layoutId)
    {
        if (string.IsNullOrWhiteSpace(layoutId))
        {
            Gameseed26.Logger.LogWarning("Tried to select an empty manual map layout id.");
            return;
        }

        SelectedLayoutId = layoutId.Trim();
    }

    public ManualMapLayoutEntry SelectRandomLayout(ManualMapLayoutRegistry registry)
    {
        if (registry == null)
        {
            Gameseed26.Logger.LogWarning("Cannot randomize manual map layout because registry is missing.");
            return null;
        }

        ManualMapLayoutEntry layout = registry.GetRandomLayout();
        if (layout == null)
        {
            Gameseed26.Logger.LogWarning("Cannot randomize manual map layout because registry has no available layouts.");
            return null;
        }

        SelectLayout(layout.SafeId);
        return layout;
    }

    public void ClearSelection()
    {
        SelectedLayoutId = null;
    }
}
