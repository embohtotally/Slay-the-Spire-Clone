using PlushieOdyssey.Data;
using UnityEngine;

namespace PlushieOdyssey.Combat.Views
{
    public readonly struct CardDisplayData
    {
        public string Title { get; }
        public string Description { get; }
        public int Mana { get; }
        public CardType Type { get; }
        public Sprite Image { get; }
        public Tier Tier { get; }

        public CardDisplayData(string title, string description, int mana,
            CardType type, Sprite image, Tier tier)
        {
            Title = title;
            Description = description;
            Mana = mana;
            Type = type;
            Image = image;
            Tier = tier;
        }

        public CardDisplayData WithCost(int mana) =>
            new CardDisplayData(Title, Description, mana, Type, Image, Tier);
    }
}
