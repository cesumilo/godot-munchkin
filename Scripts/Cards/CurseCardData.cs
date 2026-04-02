using Godot;

/// <summary>
/// Represents a Curse card with a specific negative effect.
/// </summary>
/// <remarks>
/// Per §4.1: Curse cards apply negative effects immediately when drawn
/// or when played from hand. Effects include losing equipment, levels, or race.
/// </remarks>
public partial class CurseCardData : CardData
{
    /// <summary>
    /// Gets or sets the specific curse effect applied when this card resolves.
    /// </summary>
    /// <value>The type of negative effect applied to the target.</value>
    /// <remarks>
    /// Per §4.1: Curse effects include LoseHeadgear, LoseLevel, ChangeSex, LoseRace, etc.
    /// </remarks>
    [Export]
    public CurseEffect Effect { get; set; } = CurseEffect.LoseHeadgear;

    /// <summary>
    /// Initializes a new instance with default deck and card type.
    /// </summary>
    public CurseCardData()
    {
        Type = CardType.Curse;
        DType = DeckType.Dungeon;
    }
}
