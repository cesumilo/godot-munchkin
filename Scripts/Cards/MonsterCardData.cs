using Godot;

/// <summary>
/// Represents a Monster card with combat stats and rewards.
/// </summary>
/// <remarks>
/// Per §4.1 and §8: Monster cards define combat encounters with level,
/// bonuses against specific races/classes, flee penalties, and rewards.
/// </remarks>
public partial class MonsterCardData : CardData
{
    /// <summary>
    /// Gets or sets the monster's combat level.
    /// </summary>
    /// <value>Integer representing the monster's base combat strength.</value>
    /// <remarks>
    /// Per §8.3: Monster force = level + modifiers + conditional bonuses.
    /// </remarks>
    [Export]
    public int Level { get; set; } = 1;

    /// <summary>
    /// Gets or sets the race this monster gets a bonus against.
    /// </summary>
    /// <value>The race type that triggers the bonus, or None if no bonus.</value>
    /// <remarks>
    /// Per §8.3: Some monsters have bonus against specific races (e.g., +3 against Elves).
    /// </remarks>
    [Export]
    public RaceType BonusAgainstRace { get; set; } = RaceType.None;

    /// <summary>
    /// Gets or sets the class this monster gets a bonus against.
    /// </summary>
    /// <value>The class type that triggers the bonus, or None if no bonus.</value>
    [Export]
    public ClassType BonusAgainstClass { get; set; } = ClassType.None;

    /// <summary>
    /// Gets or sets the bonus value when fighting the specified race/class.
    /// </summary>
    /// <value>Additional levels added when fighting the vulnerable target.</value>
    [Export]
    public int BonusValue { get; set; } = 0;

    /// <summary>
    /// Gets or sets the penalty applied when fleeing fails.
    /// </summary>
    /// <value>The type of punishment for failed flee attempt.</value>
    /// <remarks>
    /// Per §8.7: Punishments include losing levels, items, or death.
    /// </remarks>
    [Export]
    public FleePenaltyType FleePenalty { get; set; } = FleePenaltyType.LoseLevel;

    /// <summary>
    /// Gets or sets the modifier to flee rolls against this monster.
    /// </summary>
    /// <value>Added to (or subtracted from) the d6 flee roll.</value>
    /// <remarks>
    /// Per §8.6: Some monsters make fleeing harder (e.g., -1 to flee roll).
    /// </remarks>
    [Export]
    public int FleeModifier { get; set; } = 0;

    /// <summary>
    /// Gets or sets the number of treasure cards rewarded for defeating this monster.
    /// </summary>
    /// <value>Number of cards to draw from Treasure deck on victory.</value>
    /// <remarks>
    /// Per §8.5: Winner draws treasure cards based on monster's treasure count.
    /// </remarks>
    [Export]
    public int Treasures { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number of levels gained for defeating this monster.
    /// </summary>
    /// <value>Typically 1, but some monsters grant 2+ levels.</value>
    /// <remarks>
    /// Per §8.5: Levels gained on victory. Per §2: Level 10 can ONLY be reached
    /// by killing a monster - not by selling items or other means.
    /// </remarks>
    [Export]
    public int LevelsGained { get; set; } = 1;

    /// <summary>
    /// Initializes a new instance with default deck and card type.
    /// </summary>
    public MonsterCardData()
    {
        Type = CardType.Monster;
        DType = DeckType.Dungeon;
    }
}
