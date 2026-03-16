using System.Collections.Generic;
using Godot;

public partial class CardSystemTest : Node
{
    public override void _Ready()
    {
        GD.Print("=== CARD SYSTEM TEST START ===");

        // Wait a frame to ensure CardFactory is loaded
        Callable.From(TestCardSystem).CallDeferred();
    }

    private void TestCardSystem()
    {
        // Get CardFactory instance
        var cardFactory = GetNodeOrNull<CardFactory>("/root/CardFactory");
        if (cardFactory == null)
        {
            GD.PrintErr("ERROR: CardFactory not found in autoloads");
            return;
        }

        GD.Print($"CardFactory found. Total cards: {cardFactory.GetTotalCardCount()}");

        // Test 1: Get all cards
        var allCards = cardFactory.GetAllCards();
        GD.Print($"\nTest 1: GetAllCards() - Found {allCards.Count} cards:");

        foreach (var card in allCards)
        {
            GD.Print($"  - {card.Name} ({card.Type}, {card.DType})");
        }

        // Test 2: Get cards by type
        GD.Print($"\nTest 2: GetCardsByType() - Breakdown:");
        foreach (CardData.CardType type in System.Enum.GetValues(typeof(CardData.CardType)))
        {
            var cardsOfType = cardFactory.GetCardsByType(type);
            if (cardsOfType.Count > 0)
            {
                GD.Print($"  {type}: {cardsOfType.Count} cards");
            }
        }

        // Test 3: Get cards by deck type
        GD.Print($"\nTest 3: GetCardsByDeckType() - Breakdown:");
        foreach (CardData.DeckType deckType in System.Enum.GetValues(typeof(CardData.DeckType)))
        {
            var cardsInDeck = cardFactory.GetCardsByDeckType(deckType);
            GD.Print($"  {deckType}: {cardsInDeck.Count} cards");
        }

        // Test 4: Get specific card by ID
        GD.Print($"\nTest 4: GetCardById() - Sample tests:");

        // Test Monster card
        var goblin = cardFactory.GetCardById<MonsterCardData>("monster_goblin_001");
        if (goblin != null)
        {
            GD.Print($"  Found Goblin: Level {goblin.Level}, Treasures: {goblin.Treasures}");
        }

        // Test Item card
        var sword = cardFactory.GetCardById<ItemCardData>("item_broad_sword_001");
        if (sword != null)
        {
            GD.Print($"  Found Broad Sword: +{sword.Bonus} Bonus, Slot: {sword.Slot}");
        }

        // Test Race card
        var elf = cardFactory.GetCardById<RaceCardData>("race_elf_001");
        if (elf != null)
        {
            GD.Print($"  Found Elf Race: {elf.Race}");
        }

        // Test 5: Network serialization
        GD.Print($"\nTest 5: Network serialization:");
        if (goblin != null)
        {
            var networkCard = MessageProtocol.ToNetworkFormat(goblin);
            GD.Print($"  Goblin network format:");
            GD.Print($"    ID: {networkCard.Id}");
            GD.Print($"    Type: {networkCard.Type}, Deck: {networkCard.DeckType}");
            GD.Print($"    Level: {networkCard.Level}, Treasures: {networkCard.Treasures}");
        }

        GD.Print("\n=== CARD SYSTEM TEST COMPLETE ===");
    }
}
