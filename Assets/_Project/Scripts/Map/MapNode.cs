using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MapNode
{
    public string Id;
    public int Layer;
    public int Column;
    public MapNodeType Type;
    public Vector2 Position;
    public EncounterData Encounter;
    public List<string> NextNodeIds = new();
    public bool IsVisited;
    public bool IsAvailable;

    public MapNode(string id, int layer, int column, MapNodeType type, Vector2 position)
    {
        Id = id;
        Layer = layer;
        Column = column;
        Type = type;
        Position = position;
    }

    public bool StartsCombat => Type == MapNodeType.Enemy || Type == MapNodeType.Elite || Type == MapNodeType.Boss;
}
