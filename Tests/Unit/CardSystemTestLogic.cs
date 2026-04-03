using System.Collections.Generic;

namespace Tests.Unit;

/// <summary>
/// Unit tests for CardFactory functionality.
/// Can run headless without scene dependencies.
/// </summary>
public static class CardSystemTestLogic
{
    /// <summary>
    /// Runs all card system tests.
    /// </summary>
    /// <returns>True if all tests passed.</returns>
    public static bool Run()
    {
        bool allPassed = true;

        allPassed &= TestCardFactoryLoaded();
        allPassed &= TestGetAllCards();
        allPassed &= TestGetCardsByType();
        allPassed &= TestGetCardsByDeckType();
        allPassed &= TestGetCardById();

        return allPassed;
    }

    /// <summary>
    /// Tests that CardFactory is loaded and has cards.
    /// </summary>
    private static bool TestCardFactoryLoaded()
    {
        var cardFactory = CardFactory.Instance;
        if (cardFactory == null)
        {
            GameLogger.Error("CardFactory not found in autoloads", nameof(CardSystemTestLogic));
            return false;
        }

        int count = cardFactory.GetTotalCardCount();
        GameLogger.Info($"CardFactory loaded with {count} cards", nameof(CardSystemTestLogic));
        return count > 0;
    }

    /// <summary>
    /// Tests GetAllCards method.
    /// </summary>
    private static bool TestGetAllCards()
    {
        var cards = CardFactory.Instance.GetAllCards();
        GameLogger.Info($"GetAllCards returned {cards.Count} cards", nameof(CardSystemTestLogic));
        return cards.Count > 0;
    }

    /// <summary>
    /// Tests GetCardsByType method.
    /// </summary>
    private static bool TestGetCardsByType()
    {
        var cardFactory = CardFactory.Instance;
        bool anyFound = false;

        foreach (CardData.CardType type in System.Enum.GetValues(typeof(CardData.CardType)))
        {
            var cards = cardFactory.GetCardsByType(type);
            if (cards.Count > 0)
            {
                GameLogger.Debug($"  {type}: {cards.Count} cards", nameof(CardSystemTestLogic));
                anyFound = true;
            }
        }

        return anyFound;
    }

    /// <summary>
    /// Tests GetCardsByDeckType method.
    /// </summary>
    private static bool TestGetCardsByDeckType()
    {
        var cardFactory = CardFactory.Instance;

        foreach (CardData.DeckType deckType in System.Enum.GetValues(typeof(CardData.DeckType)))
        {
            var cards = cardFactory.GetCardsByDeckType(deckType);
            GameLogger.Info($"  {deckType}: {cards.Count} cards", nameof(CardSystemTestLogic));
        }

        return true;
    }

    /// <summary>
    /// Tests GetCardById method.
    /// </summary>
    private static bool TestGetCardById()
    {
        var cardFactory = CardFactory.Instance;
        bool allPassed = true;

        // Test known cards
        var testCards = new Dictionary<string, System.Type>
        {
            { "monster_goblin_001", typeof(MonsterCardData) },
            { "item_broad_sword_001", typeof(ItemCardData) },
            { "race_elf_001", typeof(RaceCardData) },
        };

        foreach (var test in testCards)
        {
            var card = cardFactory.GetCardById(test.Key);
            if (card == null)
            {
                GameLogger.Error($"Card not found: {test.Key}", nameof(CardSystemTestLogic));
                allPassed = false;
                continue;
            }

            if (!test.Value.IsInstanceOfType(card))
            {
                GameLogger.Error(
                    $"Card {test.Key} is wrong type. Expected {test.Value.Name}, got {card.GetType().Name}",
                    nameof(CardSystemTestLogic)
                );
                allPassed = false;
                continue;
            }

            GameLogger.Info($"  Found {test.Key}: {card.Name}", nameof(CardSystemTestLogic));
        }

        return allPassed;
    }
}
