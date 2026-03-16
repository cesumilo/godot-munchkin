using Godot;

public partial class RaceCardData : CardData
{
    [Export]
    public RaceType Race { get; set; } = RaceType.Elf;

    [Export]
    public string[] Abilities { get; set; } = [];

    public RaceCardData()
    {
        Type = CardType.Race;
        DType = DeckType.Dungeon;
    }
}
