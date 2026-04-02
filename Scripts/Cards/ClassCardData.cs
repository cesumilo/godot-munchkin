using Godot;

/// <summary>
/// Represents a Class card that changes the player's class.
/// </summary>
/// <remarks>
/// Per §4.1 and §5.2: Class cards grant special class abilities.
/// Players start with no class and can acquire one during play.
/// </remarks>
public partial class ClassCardData : CardData
{
    /// <summary>
    /// Gets or sets the class granted by this card.
    /// </summary>
    /// <value>The specific class type (Warrior, Thief, Mage, Cleric).</value>
    /// <remarks>
    /// Per §5.2:
    /// - Warrior: Can discard up to 3 cards for +1 each in combat
    /// - Thief: Can attempt to steal an item once per turn (d6 ≥ 4)
    /// - Mage: Can discard cards for charm effects
    /// - Cleric: Can discard entire hand to resurrect dead players
    /// </remarks>
    [Export]
    public ClassType Class { get; set; } = ClassType.Warrior;

    /// <summary>
    /// Gets or sets the list of ability descriptions for this class.
    /// </summary>
    /// <value>String array describing the class's special abilities.</value>
    [Export]
    public string[] Abilities { get; set; } = [];

    /// <summary>
    /// Initializes a new instance with default deck and card type.
    /// </summary>
    public ClassCardData()
    {
        Type = CardType.Class;
        DType = DeckType.Dungeon;
    }
}
