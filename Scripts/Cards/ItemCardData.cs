using Godot;

public partial class ItemCardData : CardData
{
    [Export]
    public int Bonus { get; set; } = 0;

    [Export]
    public int GoldValue { get; set; } = 100; // Multiple of 100

    [Export]
    public ItemSize Size { get; set; } = ItemSize.Normal;

    [Export]
    public EquipmentSlot Slot { get; set; } = EquipmentSlot.None;

    [Export]
    public int HandsRequired { get; set; } = 1; // 0, 1, or 2

    [Export]
    public RaceType RaceRestriction { get; set; } = RaceType.None;

    [Export]
    public ClassType ClassRestriction { get; set; } = ClassType.None;

    [Export]
    public SexType SexRestriction { get; set; } = SexType.None;

    public ItemCardData()
    {
        Type = CardType.Item;
        DType = DeckType.Treasure;
    }
}
