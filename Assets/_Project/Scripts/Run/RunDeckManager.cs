using System;
using System.Collections.Generic;
using UnityEngine;

using Gameseed26;
public class RunDeckManager : PersistentSingleton<RunDeckManager>
{
    private readonly List<CardData> currentDeck = new();

    public IReadOnlyList<CardData> CurrentDeck => currentDeck;
    public bool HasDeck => currentDeck.Count > 0;
    public event Action<IReadOnlyList<CardData>> DeckChanged;

    public void InitializeIfEmpty(IEnumerable<CardData> startingDeck)
    {
        if (HasDeck) return;
        ResetDeck(startingDeck);
    }

    public void ResetDeck(IEnumerable<CardData> startingDeck)
    {
        currentDeck.Clear();
        if (startingDeck != null)
        {
            foreach (CardData cardData in startingDeck)
            {
                if (cardData != null)
                {
                    currentDeck.Add(cardData);
                }
            }
        }

        NotifyDeckChanged();
    }

    public List<CardData> GetDeckCopy()
    {
        return new List<CardData>(currentDeck);
    }

    public void ClearDeck()
    {
        if (currentDeck.Count == 0) return;

        currentDeck.Clear();
        NotifyDeckChanged();
    }

    public void AddCard(CardData cardData)
    {
        if (cardData == null)
        {
            Gameseed26.Logger.LogWarning("Tried to add a null card to the run deck.");
            return;
        }

        currentDeck.Add(cardData);
        NotifyDeckChanged();
    }

    public bool Contains(CardData cardData)
    {
        return cardData != null && currentDeck.Contains(cardData);
    }

    public bool CanRemove(CardData cardData)
    {
        return Contains(cardData);
    }

    public bool HasIndex(int index)
    {
        return index >= 0 && index < currentDeck.Count;
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

    public bool RemoveFirst(CardData cardData)
    {
        if (cardData == null) return false;

        int index = currentDeck.IndexOf(cardData);
        if (index < 0) return false;

        currentDeck.RemoveAt(index);
        NotifyDeckChanged();
        return true;
    }

    public int RemoveAll(CardData cardData)
    {
        if (cardData == null) return 0;

        int removedCount = currentDeck.RemoveAll(card => card == cardData);
        if (removedCount > 0)
        {
            NotifyDeckChanged();
        }

        return removedCount;
    }

    public bool RemoveAt(int index)
    {
        if (!HasIndex(index)) return false;

        currentDeck.RemoveAt(index);
        NotifyDeckChanged();
        return true;
    }

    public bool ReplaceFirst(CardData oldCard, CardData newCard)
    {
        if (oldCard == null || newCard == null) return false;

        int index = currentDeck.IndexOf(oldCard);
        if (index < 0) return false;

        currentDeck[index] = newCard;
        NotifyDeckChanged();
        return true;
    }

    public void NotifyDeckChanged()
    {
        DeckChanged?.Invoke(CurrentDeck);
    }
}
