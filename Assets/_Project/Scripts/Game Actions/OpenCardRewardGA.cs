public class OpenCardRewardGA : GameAction
{
    public CardRewardRequest Request { get; }

    public OpenCardRewardGA(CardRewardRequest request = null)
    {
        Request = request;
    }
}
