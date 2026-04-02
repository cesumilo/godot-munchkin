using Godot;

/// <summary>
/// Represents an Item card (equipment) with bonuses and slot restrictions.
/// </summary>
/// <remarks>
/// Per §4.2 and §9: Items provide combat bonuses when equipped to specific slots.
/// Items have restrictions based on race, class, and sex.
/// </remarks>
public partial class ItemCardData : CardData
{
    /// <summary>
    /// Gets or sets the combat bonus provided when this item is equipped.
    /// </summary>
    /// <value>Integer added to player's combat force per §8.3.</value>
    [Export]
    public int Bonus { get; set; } = 0;

    /// <summary>
    /// Gets or sets the gold value for selling this item.
    /// </summary>
    /// <value>Sell value in gold, always a multiple of 100 per §10.</value>
    /// <remarks>
    /// Per §10: 1000 gold = 1 level gained. Can only sell during your turn, outside combat.
    /// </remarks>
    [Export]
    public int GoldValue { get; set; } = 100; // Multiple of 100

    /// <summary>
    /// Gets or sets the size category of this item.
    /// </summary>
    /// <value>Normal or Big item per §9.2.</value>
    /// <remarks>
    /// Per §9.2: Players can carry only one Big item unless they are a Dwarf.
    /// </remarks>
    [Export]
    public ItemSize Size { get; set; } = ItemSize.Normal;

    /// <summary>
    /// Gets or sets which equipment slot this item occupies.
    /// </summary>
    /// <value>The slot type where this item can be equipped.</value>
    /// <remarks>
    /// Per §9.1: Slots are Head, Armor, Foot, Hand1, Hand2, TwoHands, or None.
    /// </remarks>
    [Export]
    public EquipmentSlot Slot { get; set; } = EquipmentSlot.None;

    /// <summary>
    /// Gets or sets how many hands this item requires.
    /// </summary>
    /// <value>0, 1, or 2 hands required to use this item.</value>
    /// <remarks>
    /// Per §9.1: Two-handed items occupy both hand slots and cannot be used
    /// with any other hand items.
    /// </remarks>
    [Export]
    public int HandsRequired { get; set; } = 1; // 0, 1, or 2

    /// <summary>
    /// Gets or sets the race restriction for using this item.
    /// </summary>
    /// <value>The race that can use this item, or None for no restriction.</value>
    /// <remarks>
    /// Per §9.3: Some items can only be used by specific races.
    /// </remarks>
    [Export]
    public RaceType RaceRestriction { get; set; } = RaceType.None;

    /// <summary>
    /// Gets or sets the class restriction for using this item.
    /// </summary>
    /// <value>The class that can use this item, or None for no restriction.</value>
    [Export]
    public ClassType ClassRestriction { get; set; } = ClassType.None;

    /// <summary>
    /// Gets or sets the sex restriction for using this item.
    /// </summary>
    /// <value>The sex that can use this item, or None for no restriction.</value>
    /// <remarks>
    /// Per §9.3: Some items are restricted by character sex.
    /// </remarks>
    [Export]
    public SexType SexRestriction { get; set; } = SexType.None;

    /// <summary>
    /// Initializes a new instance with default deck and card type.
    /// </summary>
    public ItemCardData()
    {
        Type = CardType.Item;
        DType = DeckType.Treasure;
    }
}
