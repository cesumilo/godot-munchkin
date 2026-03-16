using System;

/// <summary>
/// Shared enumerations for Munchkin card system
/// Based on game rules §4 and §5
/// </summary>
public enum RaceType
{
    None,
    Human,
    Elf,
    Dwarf,
    Halfling,
}

public enum ClassType
{
    None,
    Warrior,
    Thief,
    Mage,
    Cleric,
}

public enum SexType
{
    None,
    Male,
    Female,
}

public enum ItemSize
{
    Normal,
    Big,
}

public enum EquipmentSlot
{
    Head,
    Armor,
    Foot,
    Hand1,
    Hand2,
    TwoHands,
    None,
}

public enum FleePenaltyType
{
    LoseLevel,
    LoseItem,
    Death,
    Curse,
}

public enum CurseEffect
{
    LoseHeadgear,
    LoseLevel,
    ChangeSex,
    LoseRace,
    LoseClass,
    LoseItem,
}

public enum PlayableWhen
{
    DuringYourTurn,
    DuringCombat,
    Anytime,
    InResponse,
}
