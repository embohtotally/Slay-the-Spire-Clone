using UnityEngine;

public class RunManager : PersistentSingleton<RunManager>
{
    public MapGraph CurrentMap { get; private set; }
    public string CurrentMapNodeId { get; private set; }
    public EncounterData SelectedEncounter { get; private set; }
    public bool HasActiveRun => CurrentMap != null;
    public int? CurrentHeroHealth { get; set; }

    public void StartNewRun(MapGraph mapGraph)
    {
        CurrentMap = mapGraph;
        CurrentMapNodeId = null;
        SelectedEncounter = null;
        CurrentHeroHealth = null;
    }

    public void SelectMapNode(MapNode selectedNode)
    {
        if (selectedNode == null) return;

        CurrentMapNodeId = selectedNode.Id;
        SelectedEncounter = selectedNode.Encounter;
    }

    public void ClearSelectedEncounter()
    {
        SelectedEncounter = null;
    }

    public void CompleteCurrentEncounter()
    {
        ClearSelectedEncounter();
        if (HeroSystem.Instance != null && HeroSystem.Instance.HeroView != null)
        {
            CurrentHeroHealth = HeroSystem.Instance.HeroView.CurrentHealth;
        }
    }

    public void AbandonRun()
    {
        CurrentMap = null;
        CurrentMapNodeId = null;
        SelectedEncounter = null;
    }
}
