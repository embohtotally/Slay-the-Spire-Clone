using System.Collections.Generic;
using UnityEngine;

public class RunDeckManager : PersistentSingleton<RunDeckManager>
{
    private readonly List<CardData> currentDeck = new();

    public IReadOnlyList<CardData> CurrentDeck => currentDeck;
    public bool HasDeck => currentDeck.Count > 0;

    public void InitializeIfEmpty(IEnumerable<CardData> startingDeck)
    {
        if (HasDeck) return;
        ResetDeck(startingDeck);
    }

    public void ResetDeck(IEnumerable<CardData> startingDeck)
    {
        currentDeck.Clear();
        if (startingDeck == null) return;

        foreach (CardData cardData in startingDeck)
        {
            if (cardData != null)
            {
                currentDeck.Add(cardData);
            }
        }
    }

    public List<CardData> GetDeckCopy()
    {
        return new List<CardData>(currentDeck);
    }

    public void AddCard(CardData cardData)
    {
        if (cardData == null)
        {
            Debug.LogWarning("Tried to add a null card to the run deck.");
            return;
        }

        currentDeck.Add(cardData);
    }

    public bool Contains(CardData cardData)
    {
        return cardData != null && currentDeck.Contains(cardData);
    }

    public int Count(CardData cardData)
    {
        if (cardData == null) return 0;

        int count = 0;
        foreach (CardData card in currentDeck)
        {
            if (card == cardData)
            {
                count++;
            }
        }

        return count;
    }

    public bool ReplaceFirst(CardData oldCard, CardData newCard)
    {
        if (oldCard == null || newCard == null) return false;

        int index = currentDeck.IndexOf(oldCard);
        if (index < 0) return false;

        currentDeck[index] = newCard;
        return true;
    }
}
