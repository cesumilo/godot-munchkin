using System;

/// <summary>
/// Defines all races available in Munchkin.
/// </summary>
/// <remarks>
/// Per §5.1: Races and their special abilities:
/// - Human (default): No special abilities
/// - Elf: +1 level when helping kill a monster
/// - Dwarf: Can carry multiple Big items
/// - Halfling: Reroll flee once per combat, +1 to flee rolls
/// </remarks>
public enum RaceType
{
    /// <summary>
    /// No race specified (used for restrictions only).
    /// </summary>
    None,

    /// <summary>
    /// Default race with no special abilities.
    /// </summary>
    Human,

    /// <summary>
    /// Gains +1 level when helping another player kill a monster.
    /// </summary>
    Elf,

    /// <summary>
    /// Can carry multiple Big items (ignores normal limit of 1).
    /// </summary>
    Dwarf,

    /// <summary>
    /// Can reroll flee once per combat, +1 to flee threshold (≥4 instead of ≥5).
    /// </summary>
    Halfling,
}

/// <summary>
/// Defines all classes available in Munchkin.
/// </summary>
/// <remarks>
/// Per §5.2: Classes and their special abilities:
/// - Warrior: Discard up to 3 cards in combat for +1 each
/// - Thief: Steal items (d6 ≥ 4), lose 1 level on failure, once per turn
/// - Mage: Discard cards for charm effects
/// - Cleric: Discard entire hand to resurrect dead players
/// </remarks>
public enum ClassType
{
    /// <summary>
    /// No class (default state).
    /// </summary>
    None,

    /// <summary>
    /// Can discard up to 3 cards during combat for +1 bonus each.
    /// </summary>
    Warrior,

    /// <summary>
    /// Can attempt to steal equipped items (d6 ≥ 4), once per turn.
    /// </summary>
    Thief,

    /// <summary>
    /// Can discard cards from hand for various charm effects.
    /// </summary>
    Mage,

    /// <summary>
    /// Can discard entire hand to resurrect dead players before looting.
    /// </summary>
    Cleric,
}

/// <summary>
/// Defines character sex, affecting some item restrictions.
/// </summary>
/// <remarks>
/// Per §5: Character sex is chosen at game start and can change via curses.
/// Some items have sex restrictions per §9.3.
/// </remarks>
public enum SexType
{
    /// <summary>
    /// No sex specified (used for restrictions only).
    /// </summary>
    None,

    /// <summary>
    /// Male character.
    /// </summary>
    Male,

    /// <summary>
    /// Female character.
    /// </summary>
    Female,
}

/// <summary>
/// Defines item size categories.
/// </summary>
/// <remarks>
/// Per §9.2: Size affects carrying capacity.
/// Normal items: unlimited count.
/// Big items: max 1 carried unless player is a Dwarf.
/// </remarks>
public enum ItemSize
{
    /// <summary>
    /// Regular item with no carrying restrictions.
    /// </summary>
    Normal,

    /// <summary>
    /// Large item; players can carry max 1 unless Dwarf.
    /// </summary>
    Big,
}

/// <summary>
/// Defines equipment slots where items can be equipped.
/// </summary>
/// <remarks>
/// Per §9.1: Slot system for equipment.
/// Players have: Head (1), Armor (1), Feet (1), Hands (2 total).
/// Two-handed items occupy both hand slots.
/// </remarks>
public enum EquipmentSlot
{
    /// <summary>
    /// Head slot - helmets, crowns, etc.
    /// </summary>
    Head,

    /// <summary>
    /// Body armor slot.
    /// </summary>
    Armor,

    /// <summary>
    /// Footwear slot - boots, sandals, etc.
    /// </summary>
    Foot,

    /// <summary>
    /// First hand slot.
    /// </summary>
    Hand1,

    /// <summary>
    /// Second hand slot.
    /// </summary>
    Hand2,

    /// <summary>
    /// Both hands required - cannot use with any other hand items.
    /// </summary>
    TwoHands,

    /// <summary>
    /// No specific slot (amulets, rings, etc. carried but not equipped).
    /// </summary>
    None,
}

/// <summary>
/// Defines possible penalties for failing to flee from a monster.
/// </summary>
/// <remarks>
/// Per §8.7: Different monsters apply different punishments.
/// </remarks>
public enum FleePenaltyType
{
    /// <summary>
    /// Lose one or more levels.
    /// </summary>
    LoseLevel,

    /// <summary>
    /// Lose the most valuable equipped item.
    /// </summary>
    LoseItem,

    /// <summary>
    /// Character dies - lose all equipment but keep level.
    /// </summary>
    Death,

    /// <summary>
    /// Apply a curse effect.
    /// </summary>
    Curse,
}

/// <summary>
/// Defines possible curse effects that can be applied to players.
/// </summary>
/// <remarks>
/// Per §4.1: Curse cards apply immediate negative effects.
/// </remarks>
public enum CurseEffect
{
    /// <summary>
    /// Force player to lose equipped headgear.
    /// </summary>
    LoseHeadgear,

    /// <summary>
    /// Reduce player's level by 1 (minimum 1).
    /// </summary>
    LoseLevel,

    /// <summary>
    /// Change player's sex (affects some item bonuses).
    /// </summary>
    ChangeSex,

    /// <summary>
    /// Force player to lose their current race card.
    /// </summary>
    LoseRace,

    /// <summary>
    /// Force player to lose their current class card.
    /// </summary>
    LoseClass,

    /// <summary>
    /// Force player to lose any equipped item.
    /// </summary>
    LoseItem,
}

/// <summary>
/// Defines when an action card can be played.
/// </summary>
/// <remarks>
/// Per §4.3 (MomentJeu): Timing restrictions for card play.
/// Per §4.4: Specific rules for each card type.
/// </remarks>
public enum PlayableWhen
{
    /// <summary>
    /// Only by active player during their turn, outside combat.
    /// </summary>
    DuringYourTurn,

    /// <summary>
    /// By any player during combat interaction window.
    /// </summary>
    DuringCombat,

    /// <summary>
    /// At any time by any player.
    /// </summary>
    Anytime,

    /// <summary>
    /// In response to another card or event (interrupts stack).
    /// </summary>
    InResponse,
}
