using System.Collections.Generic;
using Godot;

namespace Tests.Smoke;

/// <summary>
/// Smoke test for visual card display.
/// Requires scene: Scenes/Tests/Smoke/CardTest.tscn
/// Manual verification: Check that cards display correctly.
/// </summary>
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
        _cardFactory = CardFactory.Instance;
        if (_cardFactory == null)
        {
            GameLogger.Error("CardFactory not found in autoloads", nameof(CardTest));
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

        GameLogger.Info("CardTest initialized - use Next/Prev buttons or keys", nameof(CardTest));
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

            GameLogger.Info($"Displaying card: {card.Name} ({card.Type})", nameof(CardTest));

            // Log card details
            LogCardDetails(card);
        }
    }

    private void LogCardDetails(CardData card)
    {
        GameLogger.Debug($"  ID: {card.Id}", nameof(CardTest));
        GameLogger.Debug($"  Description: {card.Description}", nameof(CardTest));
        GameLogger.Debug($"  Deck: {card.DType}, Type: {card.Type}", nameof(CardTest));

        // Type-specific details
        switch (card)
        {
            case MonsterCardData monster:
                GameLogger.Debug(
                    $"  Monster - Level: {monster.Level}, Treasures: {monster.Treasures}",
                    nameof(CardTest)
                );
                break;
            case ItemCardData item:
                GameLogger.Debug(
                    $"  Item - Bonus: +{item.Bonus}, Value: {item.GoldValue} gold",
                    nameof(CardTest)
                );
                break;
            case RaceCardData race:
                GameLogger.Debug($"  Race: {race.Race}", nameof(CardTest));
                break;
            case ClassCardData @class:
                GameLogger.Debug($"  Class: {@class.Class}", nameof(CardTest));
                break;
            case ActionCardData action:
                GameLogger.Debug($"  Action - Playable: {action.PlayableWhen}", nameof(CardTest));
                break;
            case CurseCardData curse:
                GameLogger.Debug($"  Curse - Effect: {curse.Effect}", nameof(CardTest));
                break;
        }
    }

    /// <summary>
    /// Advances to next card.
    /// </summary>
    public void OnNextButtonPressed()
    {
        if (_loadedCards.Count == 0)
            return;

        _currentCardIndex = (_currentCardIndex + 1) % _loadedCards.Count;
        UpdateStatus();
        DisplayCurrentCard();
    }

    /// <summary>
    /// Goes to previous card.
    /// </summary>
    public void OnPrevButtonPressed()
    {
        if (_loadedCards.Count == 0)
            return;

        _currentCardIndex = (_currentCardIndex - 1 + _loadedCards.Count) % _loadedCards.Count;
        UpdateStatus();
        DisplayCurrentCard();
    }

    /// <summary>
    /// Tests network serialization of current card.
    /// </summary>
    public void OnTestNetworkButtonPressed()
    {
        if (_loadedCards.Count == 0)
            return;

        var currentCard = _loadedCards[_currentCardIndex];

        GameLogger.Info($"Network format for {currentCard.Name}:", nameof(CardTest));
        GameLogger.Info($"  ID: {currentCard.Id}", nameof(CardTest));
        GameLogger.Info($"  Type: {currentCard.Type}, Deck: {currentCard.DType}", nameof(CardTest));
    }
}
