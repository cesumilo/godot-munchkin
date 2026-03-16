using System;
using Godot;

public partial class CardData : Resource
{
    [Export]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Export]
    public string Name { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string Description { get; set; } = "";

    [Export]
    public DeckType DType { get; set; } = DeckType.Dungeon;

    [Export]
    public CardType Type { get; set; } = CardType.Monster;

    public enum DeckType
    {
        Dungeon,
        Treasure,
    }

    public enum CardType
    {
        Monster,
        Item,
        Curse,
        Race,
        Class,
        Action,
    }
}
