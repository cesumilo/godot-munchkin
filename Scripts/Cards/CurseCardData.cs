using Godot;

public partial class CurseCardData : CardData
{
    [Export]
    public CurseEffect Effect { get; set; } = CurseEffect.LoseHeadgear;

    public CurseCardData()
    {
        Type = CardType.Curse;
        DType = DeckType.Dungeon;
    }
}
