using Godot;

/// <summary>
/// Represents an Action card (Treasure or Dungeon) with a one-time effect.
/// </summary>
/// <remarks>
/// Per §4.1 and §4.2: Action cards provide one-time effects when played.
/// Timing restrictions determine when each action can be played.
/// </remarks>
public partial class ActionCardData : CardData
{
    /// <summary>
    /// Gets or sets when this action card can be played.
    /// </summary>
    /// <value>The timing window for playing this card.</value>
    /// <remarks>
    /// Per §4.3 (MomentJeu):
    /// - DuringYourTurn: Only by active player, outside combat
    /// - DuringCombat: By any player during combat interaction window
    /// - Anytime: By any player at any moment
    /// - InResponse: As reaction to another card or event
    /// </remarks>
    [Export]
    public PlayableWhen PlayableWhen { get; set; } = PlayableWhen.DuringYourTurn;

    /// <summary>
    /// Gets or sets the detailed effect description of this action.
    /// </summary>
    /// <value>Multi-line text explaining what happens when played.</value>
    [Export(PropertyHint.MultilineText)]
    public string Effect { get; set; } = "";

    /// <summary>
    /// Initializes a new instance with default card type.
    /// </summary>
    /// <remarks>
    /// Note: Action cards can be in either deck (Dungeon or Treasure).
    /// The specific deck is set per instance in the .tres resource files.
    /// </remarks>
    public ActionCardData()
    {
        Type = CardType.Action;
    }
}
