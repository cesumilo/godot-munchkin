using System;
using Godot;

/// <summary>
/// Represents the base data for all card types in Munchkin.
/// Serves as the foundation for the card hierarchy per §4.
/// </summary>
/// <remarks>
/// Per §4: All cards have id, name, description, deck type, and card type.
/// This is an abstract base class that should not be instantiated directly.
/// </remarks>
public partial class CardData : Resource
{
    /// <summary>
    /// Gets or sets the unique identifier for this card instance.
    /// </summary>
    /// <value>A GUID string that uniquely identifies this card.</value>
    [Export]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the display name of the card.
    /// </summary>
    /// <value>The card's title as shown to players.</value>
    [Export]
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the detailed description of the card's effects.
    /// </summary>
    /// <value>Multi-line text explaining what the card does.</value>
    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = "";

    /// <summary>
    /// Gets or sets which deck this card belongs to.
    /// </summary>
    /// <value>Dungeon or Treasure deck per §4.</value>
    /// <remarks>
    /// Per §4.1: Dungeon deck contains Monsters, Curses, Races, Classes.
    /// Per §4.2: Treasure deck contains Items and Actions.
    /// </remarks>
    [Export]
    public DeckType DType { get; set; } = DeckType.Dungeon;

    /// <summary>
    /// Gets or sets the specific card type.
    /// </summary>
    /// <value>The card's classification affecting when and how it can be played.</value>
    /// <remarks>
    /// Per §4.4: Different card types have different play timing rules.
    /// </remarks>
    [Export]
    public CardType Type { get; set; } = CardType.Monster;

    /// <summary>
    /// Defines the two card decks in Munchkin.
    /// </summary>
    /// <remarks>
    /// Per §1 (Glossary): Pioche Donjon and Pioche Trésor.
    /// Per §6: Each player draws 4 from each during setup.
    /// </remarks>
    public enum DeckType
    {
        /// <summary>
        /// Dungeon deck - contains Monsters, Curses, Races, Classes.
        /// </summary>
        Dungeon,

        /// <summary>
        /// Treasure deck - contains Items and Actions.
        /// </summary>
        Treasure,
    }

    /// <summary>
    /// Defines all card types in Munchkin.
    /// </summary>
    /// <remarks>
    /// Per §4: Complete taxonomy of card types.
    /// </remarks>
    public enum CardType
    {
        /// <summary>
        /// Monster cards - fought in combat per §8.
        /// </summary>
        Monster,

        /// <summary>
        /// Item cards - equipment that provides bonuses per §9.
        /// </summary>
        Item,

        /// <summary>
        /// Curse cards - negative effects applied to players.
        /// </summary>
        Curse,

        /// <summary>
        /// Race cards - changes player's race per §5.1.
        /// </summary>
        Race,

        /// <summary>
        /// Class cards - changes player's class per §5.2.
        /// </summary>
        Class,

        /// <summary>
        /// Action cards - one-time effects per §4.2.
        /// </summary>
        Action,
    }
}
