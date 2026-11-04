using System.Collections.Generic;
using UnityEngine;

public static class MultiplayerCardCatalog
{
    private static bool initialized;
    private static readonly Dictionary<string, CardData> byId = new Dictionary<string, CardData>();

    public static void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;

        CardData[] cards = Resources.LoadAll<CardData>("Card");
        foreach (CardData card in cards)
        {
            if (card == null) continue;
            string id = GetCardId(card);
            if (string.IsNullOrEmpty(id)) continue;

            byId[id] = card;
        }
    }

    public static string GetCardId(CardData card)
    {
        if (card == null) return string.Empty;
        return card.name;
    }

    public static CardData Find(string id)
    {
        EnsureInitialized();
        if (string.IsNullOrEmpty(id)) return null;

        byId.TryGetValue(id, out CardData card);
        return card;
    }
}
