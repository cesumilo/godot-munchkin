using System.Collections.Generic;
using Godot;

public partial class CardFactory : Node
{
    private static CardFactory _instance;

    private Dictionary<string, CardData> _cardDatabase = new();

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

    public override void _Ready()
    {
        _instance = this;
        LoadAllCards();
    }

    private void LoadAllCards()
    {
        // Load cards from all subdirectories
        LoadCardsFromDirectory("Monsters");
        LoadCardsFromDirectory("Items");
        LoadCardsFromDirectory("Curses");
        LoadCardsFromDirectory("Races");
        LoadCardsFromDirectory("Classes");
        LoadCardsFromDirectory("Actions");

        GD.Print($"[CardFactory] Loaded {_cardDatabase.Count} cards");
    }

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

    public CardData GetCardById(string id)
    {
        if (_cardDatabase.TryGetValue(id, out var cardData))
        {
            return cardData;
        }

        GD.PrintErr($"[CardFactory] Card not found: {id}");
        return null;
    }

    public T GetCardById<T>(string id)
        where T : CardData
    {
        var card = GetCardById(id);
        return card as T;
    }

    public List<CardData> GetAllCards()
    {
        return new List<CardData>(_cardDatabase.Values);
    }

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

    public int GetTotalCardCount()
    {
        return _cardDatabase.Count;
    }
}
