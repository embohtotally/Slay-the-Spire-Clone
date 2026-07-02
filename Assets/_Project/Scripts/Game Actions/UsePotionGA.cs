public class UsePotionGA : GameAction
{
    public int PotionIndex { get; }
    public PotionData Potion { get; }
    public CombatantView ManualTarget { get; }
    public bool ConsumeOnUse { get; }
    public bool WasSuccessful { get; private set; }
    public string FailureReason { get; private set; }

    public UsePotionGA(int potionIndex, PotionData potion, CombatantView manualTarget = null, bool consumeOnUse = true)
    {
        PotionIndex = potionIndex;
        Potion = potion;
        ManualTarget = manualTarget;
        ConsumeOnUse = consumeOnUse;
    }

    public void MarkSuccessful()
    {
        WasSuccessful = true;
        FailureReason = string.Empty;
    }

    public void MarkFailed(string reason)
    {
        WasSuccessful = false;
        FailureReason = reason;
    }
}
