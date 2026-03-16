using System.Collections.Generic;
using Godot;

public partial class CardTest : Node3D
{
    private CardFactory _cardFactory;
    private Label3D _statusLabel;
    private int _currentCardIndex = 0;
    private List<CardData> _loadedCards = new();
    private CardVisual _currentCardVisual;

    public override void _Ready()
    {
        _statusLabel = GetNode<Label3D>("StatusLabel");
        _currentCardVisual = GetNode<CardVisual>("CardVisual");

        // Get CardFactory instance
        _cardFactory = GetNode<CardFactory>("/root/CardFactory");
        if (_cardFactory == null)
        {
            GD.PrintErr("[CardTest] CardFactory not found in autoloads");
            _statusLabel.Text = "ERROR: CardFactory not loaded";
            return;
        }

        // Get all loaded cards
        _loadedCards = _cardFactory.GetAllCards();

        UpdateStatus();

        if (_loadedCards.Count > 0)
        {
            DisplayCurrentCard();
        }
    }

    private void UpdateStatus()
    {
        _statusLabel.Text =
            $"Cards loaded: {_loadedCards.Count}\n"
            + $"Current: {_currentCardIndex + 1}/{_loadedCards.Count}";
    }

    private void DisplayCurrentCard()
    {
        if (_currentCardIndex >= 0 && _currentCardIndex < _loadedCards.Count)
        {
            var card = _loadedCards[_currentCardIndex];
            _currentCardVisual.CardData = card;

            GD.Print($"[CardTest] Displaying card: {card.Name} ({card.Type})");

            // Show card details in console
            GD.Print($"  ID: {card.Id}");
            GD.Print($"  Description: {card.Description}");
            GD.Print($"  Deck: {card.DType}, Type: {card.Type}");

            // Show type-specific details
            switch (card)
            {
                case MonsterCardData monster:
                    GD.Print($"  Monster - Level: {monster.Level}, Treasures: {monster.Treasures}");
                    break;
                case ItemCardData item:
                    GD.Print($"  Item - Bonus: +{item.Bonus}, Value: {item.GoldValue} gold");
                    break;
                case RaceCardData race:
                    GD.Print($"  Race: {race.Race}");
                    break;
                case ClassCardData @class:
                    GD.Print($"  Class: {@class.Class}");
                    break;
                case ActionCardData action:
                    GD.Print($"  Action - Playable: {action.PlayableWhen}");
                    break;
                case CurseCardData curse:
                    GD.Print($"  Curse - Effect: {curse.Effect}");
                    break;
            }
        }
    }

    public void OnNextButtonPressed()
    {
        if (_loadedCards.Count == 0)
            return;

        _currentCardIndex = (_currentCardIndex + 1) % _loadedCards.Count;
        UpdateStatus();
        DisplayCurrentCard();
    }

    public void OnPrevButtonPressed()
    {
        if (_loadedCards.Count == 0)
            return;

        _currentCardIndex = (_currentCardIndex - 1 + _loadedCards.Count) % _loadedCards.Count;
        UpdateStatus();
        DisplayCurrentCard();
    }

    public void OnTestNetworkButtonPressed()
    {
        if (_loadedCards.Count == 0)
            return;

        var currentCard = _loadedCards[_currentCardIndex];
        var networkCard = MessageProtocol.ToNetworkFormat(currentCard);

        GD.Print($"[CardTest] Network format for {currentCard.Name}:");
        GD.Print($"  Id: {networkCard.Id}");
        GD.Print($"  Type: {networkCard.Type}, Deck: {networkCard.DeckType}");

        if (networkCard.Level.HasValue)
            GD.Print($"  Level: {networkCard.Level}");
        if (networkCard.Bonus.HasValue)
            GD.Print($"  Bonus: {networkCard.Bonus}");

        GD.Print($"  Additional properties: {networkCard.AdditionalProperties.Count}");
        foreach (var key in networkCard.AdditionalProperties.Keys)
        {
            GD.Print($"    {key}: {networkCard.AdditionalProperties[key]}");
        }
    }
}
