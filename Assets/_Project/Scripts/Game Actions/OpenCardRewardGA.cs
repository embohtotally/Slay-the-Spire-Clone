using PlushieOdyssey.Rewards;
namespace PlushieOdyssey.Combat.Actions
{
    public class OpenCardRewardGA : GameAction
    {
        public CardRewardRequest Request { get; }

        public OpenCardRewardGA(CardRewardRequest request = null)
        {
            Request = request;
        }
    }
}
