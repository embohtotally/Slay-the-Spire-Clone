using UnityEngine;

public readonly struct StatusEffectDisplayData
{
    public string Id { get; }
    public string DisplayName { get; }
    public Sprite Icon { get; }
    public int Stack { get; }
    public int RemainingTurns { get; }
    public bool IsDebuff { get; }

    public StatusEffectDisplayData(string id, string displayName, Sprite icon, int stack, int remainingTurns, bool isDebuff)
    {
        Id = id;
        DisplayName = displayName;
        Icon = icon;
        Stack = stack;
        RemainingTurns = remainingTurns;
        IsDebuff = isDebuff;
    }
}
