using Godot;

public partial class ActionCardData : CardData
{
    [Export]
    public PlayableWhen PlayableWhen { get; set; } = PlayableWhen.DuringYourTurn;

    [Export(PropertyHint.MultilineText)]
    public string Effect { get; set; } = "";

    public ActionCardData()
    {
        Type = CardType.Action;
    }
}
