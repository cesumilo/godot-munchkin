using System;
using Godot;

public partial class ClassCardData : CardData
{
    [Export]
    public ClassType Class { get; set; } = ClassType.Warrior;

    [Export]
    public string[] Abilities { get; set; } = [];

    public ClassCardData()
    {
        Type = CardType.Class;
        DType = DeckType.Dungeon;
    }
}
