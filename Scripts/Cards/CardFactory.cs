using System.Collections.Generic;
using Godot;

/// <summary>
/// Loads and manages all card definitions from resource files.
/// </summary>
/// <remarks>
/// Per §4: Card definitions are loaded from .tres files in Resources/Cards/Definitions/.
/// This factory maintains a singleton instance accessible throughout the game.
/// Cards are referenced by unique ID in network messages per PROTOCOL.md.
/// </remarks>
public partial class CardFactory : Node
{
    /// <summary>
    /// Singleton instance of CardFactory.
    /// </summary>
    private static CardFactory _instance;

    /// <summary>
    /// Gets the singleton instance of CardFactory.
    /// </summary>
    /// <value>The singleton instance, creating one if none exists.</value>
    public static CardFactory Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = new CardFactory();
            }
            return _instance;
        }
    }

    /// <summary>
    /// Dictionary mapping card IDs to their CardData instances.
    /// </summary>
    private Dictionary<string, CardData> _cardDatabase = new();

    /// <summary>
    /// Initializes the node and loads all card definitions.
    /// </summary>
    public override void _Ready()
    {
        _instance = this;
        LoadAllCards();
    }

    /// <summary>
    /// Loads cards from all subdirectories.
    /// </summary>
    private void LoadAllCards()
    {
        LoadCardsFromDirectory("Monsters");
        LoadCardsFromDirectory("Items");
        LoadCardsFromDirectory("Curses");
        LoadCardsFromDirectory("Races");
        LoadCardsFromDirectory("Classes");
        LoadCardsFromDirectory("Actions");

        GD.Print($"[CardFactory] Loaded {_cardDatabase.Count} cards");
    }

    /// <summary>
    /// Loads all .tres card files from a specific directory.
    /// </summary>
    /// <param name="subdirectory">The subdirectory name within Resources/Cards/Definitions/.</param>
    private void LoadCardsFromDirectory(string subdirectory)
    {
        string directoryPath = $"res://Resources/Cards/Definitions/{subdirectory}/";

        if (!DirAccess.DirExistsAbsolute(directoryPath))
        {
            GD.Print($"[CardFactory] Directory doesn't exist: {directoryPath}");
            return;
        }

        using var dir = DirAccess.Open(directoryPath);
        if (dir == null)
        {
            GD.PrintErr($"[CardFactory] Failed to open directory: {directoryPath}");
            return;
        }

        dir.ListDirBegin();
        string fileName = dir.GetNext();

        while (!string.IsNullOrEmpty(fileName))
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".tres"))
            {
                string fullPath = $"{directoryPath}{fileName}";
                var cardData = ResourceLoader.Load<CardData>(fullPath);

                if (cardData != null)
                {
                    _cardDatabase[cardData.Id] = cardData;
                    GD.Print(
                        $"[CardFactory] Loaded card: {cardData.Name} ({cardData.Type}) from {fileName}"
                    );
                }
                else
                {
                    GD.PrintErr($"[CardFactory] Failed to load card from: {fileName}");
                }
            }

            fileName = dir.GetNext();
        }

        dir.ListDirEnd();
    }

    /// <summary>
    /// Retrieves a card by its unique ID.
    /// </summary>
    /// <param name="id">The unique card identifier.</param>
    /// <returns>The CardData instance, or null if not found.</returns>
    public CardData GetCardById(string id)
    {
        if (_cardDatabase.TryGetValue(id, out var cardData))
        {
            return cardData;
        }

        GD.PrintErr($"[CardFactory] Card not found: {id}");
        return null;
    }

    /// <summary>
    /// Retrieves a card by ID and casts it to a specific type.
    /// </summary>
    /// <typeparam name="T">The expected card type (MonsterCardData, ItemCardData, etc.).</typeparam>
    /// <param name="id">The unique card identifier.</param>
    /// <returns>The typed card data, or null if not found or wrong type.</returns>
    public T GetCardById<T>(string id)
        where T : CardData
    {
        var card = GetCardById(id);
        return card as T;
    }

    /// <summary>
    /// Returns a list of all loaded cards.
    /// </summary>
    /// <returns>List containing all CardData instances.</returns>
    public List<CardData> GetAllCards()
    {
        return new List<CardData>(_cardDatabase.Values);
    }

    /// <summary>
    /// Returns all cards of a specific type.
    /// </summary>
    /// <param name="type">The card type to filter by.</param>
    /// <returns>List of cards matching the type.</returns>
    public List<CardData> GetCardsByType(CardData.CardType type)
    {
        var result = new List<CardData>();
        foreach (var card in _cardDatabase.Values)
        {
            if (card.Type == type)
            {
                result.Add(card);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns all cards from a specific deck.
    /// </summary>
    /// <param name="deckType">The deck type to filter by (Dungeon or Treasure).</param>
    /// <returns>List of cards from that deck.</returns>
    public List<CardData> GetCardsByDeckType(CardData.DeckType deckType)
    {
        var result = new List<CardData>();
        foreach (var card in _cardDatabase.Values)
        {
            if (card.DType == deckType)
            {
                result.Add(card);
            }
        }
        return result;
    }

    /// <summary>
    /// Gets the total count of loaded cards.
    /// </summary>
    /// <returns>Number of cards in the database.</returns>
    public int GetTotalCardCount()
    {
        return _cardDatabase.Count;
    }
}
