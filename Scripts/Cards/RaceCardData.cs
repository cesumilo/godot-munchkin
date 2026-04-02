using Godot;

/// <summary>
/// Represents a Race card that changes the player's race.
/// </summary>
/// <remarks>
/// Per §4.1 and §5.1: Race cards change the player's race from Human
/// to Elf, Dwarf, or Halfling, each with unique abilities.
/// </remarks>
public partial class RaceCardData : CardData
{
    /// <summary>
    /// Gets or sets the race granted by this card.
    /// </summary>
    /// <value>The specific race type (Elf, Dwarf, Halfling).</value>
    /// <remarks>
    /// Per §5.1:
    /// - Elf: Gains +1 level when helping another player kill a monster
    /// - Dwarf: Can carry multiple Big items
    /// - Halfling: Can reroll flee once per combat, +1 to flee rolls
    /// </remarks>
    [Export]
    public RaceType Race { get; set; } = RaceType.Elf;

    /// <summary>
    /// Gets or sets the list of ability descriptions for this race.
    /// </summary>
    /// <value>String array describing the race's special abilities.</value>
    [Export]
    public string[] Abilities { get; set; } = [];

    /// <summary>
    /// Initializes a new instance with default deck and card type.
    /// </summary>
    public RaceCardData()
    {
        Type = CardType.Race;
        DType = DeckType.Dungeon;
    }
}
