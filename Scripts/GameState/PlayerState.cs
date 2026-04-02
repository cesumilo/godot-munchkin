using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a player's state according to game rules §5.
/// Client-side representation that mirrors server state for the Munchkin card game.
/// </summary>
/// <remarks>
/// Per §5: Tracks character attributes (level, race, class, sex), equipment (worn and carried),
/// hand cards, and death status. This is a client-side mirror of the authoritative server state.
/// </remarks>
public partial class PlayerState
{
    // Player identification
    /// <summary>
    /// Gets or sets the unique player identifier assigned by the server.
    /// </summary>
    public string PlayerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name chosen by the player.
    /// </summary>
    public string PlayerName { get; set; } = string.Empty;

    // Character attributes (rules §5)
    /// <summary>
    /// Gets or sets the player's current level.
    /// </summary>
    /// <value>Range 1-10. Per §2 and §14: Level 10 can ONLY be reached by killing a monster in combat.</value>
    public int Level { get; set; } = 1;

    // Race system (null = Human)
    /// <summary>
    /// Gets or sets the player's primary race.
    /// </summary>
    /// <value>Per §5.1: RaceType.None represents Human (default). Other values are Elf, Dwarf, Halfling.</value>
    public RaceType PrimaryRace { get; set; } = RaceType.None;

    /// <summary>
    /// Gets or sets the player's secondary race.
    /// </summary>
    /// <value>
    /// Per §5: Only non-null when MixedBlood card is active. Provides additional racial abilities.
    /// </value>
    public RaceType SecondaryRace { get; set; } = RaceType.None;

    /// <summary>
    /// Gets or sets whether the player has the MixedBlood card active.
    /// </summary>
    /// <value>Per §4.1: Allows the player to have two races simultaneously.</value>
    public bool HasMixedBlood { get; set; } = false;

    // Class system (null = no class)
    /// <summary>
    /// Gets or sets the player's primary class.
    /// </summary>
    /// <value>Per §5.2: ClassType.None represents no class. Other values are Warrior, Thief, Mage, Cleric.</value>
    public ClassType PrimaryClass { get; set; } = ClassType.None;

    /// <summary>
    /// Gets or sets the player's secondary class.
    /// </summary>
    /// <value>
    /// Per §5: Only non-null when SuperMunchkin card is active. Provides additional class abilities.
    /// </value>
    public ClassType SecondaryClass { get; set; } = ClassType.None;

    /// <summary>
    /// Gets or sets whether the player has the SuperMunchkin card active.
    /// </summary>
    /// <value>Per §4.1: Allows the player to have two classes simultaneously.</value>
    public bool HasSuperMunchkin { get; set; } = false;

    /// <summary>
    /// Gets or sets the player's sex.
    /// </summary>
    /// <value>Per §5: Used for certain equipment restrictions (§9.3) and curse effects.</value>
    public SexType Sex { get; set; } = SexType.None;

    // Equipment (rules §9)
    /// <summary>
    /// Gets the list of IDs for equipment currently worn by the player.
    /// </summary>
    /// <remarks>
    /// Per §9.1: Worn equipment occupies slots and provides active bonuses.
    /// Per §9.4: Worn equipment is visible to all players and can be stolen (Thief ability §12.3).
    /// </remarks>
    public List<string> WornEquipmentIds { get; set; } = new();

    /// <summary>
    /// Gets the list of IDs for equipment currently carried by the player.
    /// </summary>
    /// <remarks>
    /// Per §9.4: Carried equipment is in play but not equipped, providing no bonus.
    /// Can be sold (§10), given to other players (§12.1), or equipped later.
    /// </remarks>
    public List<string> CarriedEquipmentIds { get; set; } = new();

    // Hand cards (rules §7.1)
    /// <summary>
    /// Gets the list of card IDs in the player's hand.
    /// </summary>
    /// <remarks>
    /// Per §7.1 and §12: Cards in hand are not visible to other players.
    /// Per §12: Maximum 5 cards at end of turn; excess must be given away during Charity phase.
    /// </remarks>
    public List<string> HandCardIds { get; set; } = new();

    // Status
    /// <summary>
    /// Gets or sets whether the player is currently dead.
    /// </summary>
    /// <remarks>
    /// Per §11: Death is a temporary state. Player loses all equipment and hand cards,
    /// but retains level, race, and class. Resurrects at next turn with 4 Donjon + 4 Trésor cards.
    /// </remarks>
    public bool IsDead { get; set; } = false;

    // Calculated properties
    /// <summary>
    /// Gets the total combat bonus for display purposes.
    /// </summary>
    /// <value>
    /// Per §8.3: Returns level + sum of all worn equipment bonuses.
    /// This is client-side calculation for UI only; server is authoritative for actual combat resolution.
    /// </value>
    [JsonIgnore]
    public int TotalCombatBonus
    {
        get
        {
            // This is client-side calculation for display only
            // Server does authoritative calculation
            int total = Level;

            // Add equipment bonuses from CardFactory
            if (CardFactory.Instance != null)
            {
                foreach (var equipmentId in WornEquipmentIds)
                {
                    var item = CardFactory.Instance.GetCardById<ItemCardData>(equipmentId);
                    if (item != null)
                    {
                        total += item.Bonus;
                    }
                }
            }

            return total;
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerState"/> class.
    /// </summary>
    public PlayerState() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayerState"/> class with specified player information.
    /// </summary>
    /// <param name="playerId">The unique player identifier.</param>
    /// <param name="playerName">The display name of the player.</param>
    public PlayerState(string playerId, string playerName)
    {
        PlayerId = playerId;
        PlayerName = playerName;
    }

    /// <summary>
    /// Determines whether the player can equip the specified item.
    /// </summary>
    /// <param name="itemCardId">The unique identifier of the item card to check.</param>
    /// <returns>
    /// <c>true</c> if the player meets all requirements (race, class, sex, slot availability, big item limit);
    /// <c>false</c> otherwise.
    /// </returns>
    /// <remarks>
    /// Per §9.3: Validates race restrictions, class restrictions, sex restrictions, slot availability,
    /// and big item limits (§9.2). Dwarf race allows multiple big items.
    /// </remarks>
    public bool CanEquipItem(string itemCardId)
    {
        var item = GetItemData(itemCardId);
        if (item == null)
            return false;

        // Check race restriction
        if (item.RaceRestriction != RaceType.None)
        {
            if (item.RaceRestriction != PrimaryRace && item.RaceRestriction != SecondaryRace)
                return false;
        }

        // Check class restriction
        if (item.ClassRestriction != ClassType.None)
        {
            if (item.ClassRestriction != PrimaryClass && item.ClassRestriction != SecondaryClass)
                return false;
        }

        // Check sex restriction
        if (item.SexRestriction != SexType.None && item.SexRestriction != Sex)
            return false;

        // Check slot availability
        if (!IsSlotAvailable(item))
            return false;

        // Check big item limit (rules §9.2)
        if (item.Size == ItemSize.Big && !CanCarryAnotherBigItem())
            return false;

        return true;
    }

    /// <summary>
    /// Checks if the specified slot is available for equipping an item.
    /// </summary>
    /// <param name="item">The item card data to check slot availability for.</param>
    /// <returns><c>true</c> if the slot is available; <c>false</c> otherwise.</returns>
    /// <remarks>
    /// Per §9.1: Tracks slot usage including hand slots (max 2), two-handed items (occupies both hands),
    /// and body slots (Head, Armor, Feet - max 1 each).
    /// </remarks>
    private bool IsSlotAvailable(ItemCardData item)
    {
        if (item.Slot == EquipmentSlot.None)
            return true;

        // Count occupied hand slots
        int occupiedHandSlots = 0;
        bool hasTwoHandedItem = false;

        foreach (var wornId in WornEquipmentIds)
        {
            var wornItem = GetItemData(wornId);
            if (wornItem != null)
            {
                if (wornItem.Slot == EquipmentSlot.TwoHands)
                {
                    hasTwoHandedItem = true;
                    occupiedHandSlots += 2; // Two-handed occupies both
                }
                else if (
                    wornItem.Slot == EquipmentSlot.Hand1
                    || wornItem.Slot == EquipmentSlot.Hand2
                )
                {
                    occupiedHandSlots += 1;
                }
            }
        }

        // Check slot availability
        if (item.Slot == EquipmentSlot.TwoHands)
        {
            // Two-handed item needs both hand slots free
            return occupiedHandSlots == 0;
        }
        else if (item.Slot == EquipmentSlot.Hand1 || item.Slot == EquipmentSlot.Hand2)
        {
            // One-handed item needs at least one hand slot free
            // And no two-handed item equipped
            return !hasTwoHandedItem && occupiedHandSlots < 2;
        }
        else
        {
            // Head, Armor, Foot - check if slot already occupied
            foreach (var wornId in WornEquipmentIds)
            {
                var wornItem = GetItemData(wornId);
                if (wornItem != null && wornItem.Slot == item.Slot)
                {
                    return false;
                }
            }
            return true;
        }
    }

    /// <summary>
    /// Determines whether the player can carry another big item.
    /// </summary>
    /// <returns>
    /// <c>true</c> if the player can carry another big item; <c>false</c> otherwise.
    /// </returns>
    /// <remarks>
    /// Per §9.2: Dwarf race can carry multiple big items. Other races are limited to one big item.
    /// </remarks>
    private bool CanCarryAnotherBigItem()
    {
        // Dwarf can carry multiple big items (rules §9.2)
        if (PrimaryRace == RaceType.Dwarf || SecondaryRace == RaceType.Dwarf)
            return true;

        // Count big items currently worn
        int bigItemCount = 0;
        foreach (var wornId in WornEquipmentIds)
        {
            var wornItem = GetItemData(wornId);
            if (wornItem != null && wornItem.Size == ItemSize.Big)
                bigItemCount++;
        }

        return bigItemCount < 1; // Only one big item allowed for non-dwarves
    }

    /// <summary>
    /// Determines whether the player has the specified card in play (worn or carried).
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to check.</param>
    /// <returns><c>true</c> if the card is worn or carried; <c>false</c> otherwise.</returns>
    public bool HasItemInPlay(string cardId)
    {
        return WornEquipmentIds.Contains(cardId) || CarriedEquipmentIds.Contains(cardId);
    }

    /// <summary>
    /// Determines whether the player has the specified card in hand.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to check.</param>
    /// <returns><c>true</c> if the card is in hand; <c>false</c> otherwise.</returns>
    public bool HasCardInHand(string cardId)
    {
        return HandCardIds.Contains(cardId);
    }

    /// <summary>
    /// Adds a card to the player's hand.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to add.</param>
    /// <returns><c>true</c> if the card was added; <c>false</c> if already in hand.</returns>
    public bool AddToHand(string cardId)
    {
        if (!HandCardIds.Contains(cardId))
        {
            HandCardIds.Add(cardId);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a card from the player's hand.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card to remove.</param>
    /// <returns><c>true</c> if the card was removed; <c>false</c> if not found in hand.</returns>
    public bool RemoveFromHand(string cardId)
    {
        return HandCardIds.Remove(cardId);
    }

    /// <summary>
    /// Equips an item from hand or carried equipment.
    /// </summary>
    /// <param name="cardId">The unique identifier of the item to equip.</param>
    /// <returns>
    /// <c>true</c> if the item was successfully equipped; <c>false</c> otherwise.
    /// </returns>
    /// <remarks>
    /// Per §9: Item must pass CanEquipItem validation. Item is removed from hand or carried list
    /// and added to worn equipment. Per §9.4: Items can only be equipped during the player's turn,
    /// outside of combat (enforced by server, client shows preview).
    /// </remarks>
    public bool EquipItem(string cardId)
    {
        // Check if already equipped
        if (WornEquipmentIds.Contains(cardId))
            return false;

        // Check if can be equipped
        if (!CanEquipItem(cardId))
            return false;

        // Item must be in hand or carried to equip it
        bool wasInHand = HandCardIds.Remove(cardId);
        bool wasCarried = CarriedEquipmentIds.Remove(cardId);

        if (!wasInHand && !wasCarried)
            return false; // Item not available to equip

        // Equip the item
        WornEquipmentIds.Add(cardId);
        return true;
    }

    /// <summary>
    /// Unequips an item, moving it to carried equipment.
    /// </summary>
    /// <param name="cardId">The unique identifier of the item to unequip.</param>
    /// <returns><c>true</c> if the item was unequipped; <c>false</c> if not found in worn equipment.</returns>
    /// <remarks>
    /// Per §9.4: Unequipped items become "carried" - still in play, visible, but provide no bonus.
    /// Can be sold, given away, or re-equipped later.
    /// </remarks>
    public bool UnequipItem(string cardId)
    {
        if (WornEquipmentIds.Remove(cardId))
        {
            // Move to carried equipment when unequipped
            if (!CarriedEquipmentIds.Contains(cardId))
            {
                CarriedEquipmentIds.Add(cardId);
            }
            return true;
        }
        return false;
    }

    /// <summary>
    /// Gets the item card data for the specified card ID.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card.</param>
    /// <returns>The <see cref="ItemCardData"/> if found; <c>null</c> otherwise.</returns>
    public ItemCardData GetItemData(string cardId)
    {
        if (CardFactory.Instance == null)
            return null;

        return CardFactory.Instance.GetCardById<ItemCardData>(cardId);
    }

    /// <summary>
    /// Gets the card data for the specified card ID.
    /// </summary>
    /// <param name="cardId">The unique identifier of the card.</param>
    /// <returns>The <see cref="CardData"/> if found; <c>null</c> otherwise.</returns>
    public CardData GetCardData(string cardId)
    {
        if (CardFactory.Instance == null)
            return null;

        return CardFactory.Instance.GetCardById(cardId);
    }

    /// <summary>
    /// Gets the typed card data for the specified card ID.
    /// </summary>
    /// <typeparam name="T">The expected type of card data.</typeparam>
    /// <param name="cardId">The unique identifier of the card.</param>
    /// <returns>The typed <see cref="CardData"/> if found; <c>null</c> otherwise.</returns>
    public T GetCardData<T>(string cardId)
        where T : CardData
    {
        if (CardFactory.Instance == null)
            return null;

        return CardFactory.Instance.GetCardById<T>(cardId);
    }

    /// <summary>
    /// Calculates the total bonus from all worn equipment.
    /// </summary>
    /// <returns>The sum of all equipment bonuses.</returns>
    /// <remarks>
    /// Per §8.3: Equipment bonuses are added to level for combat force calculation.
    /// </remarks>
    public int GetEquipmentBonus()
    {
        int bonus = 0;
        if (CardFactory.Instance != null)
        {
            foreach (var equipmentId in WornEquipmentIds)
            {
                var item = GetItemData(equipmentId);
                if (item != null)
                {
                    bonus += item.Bonus;
                }
            }
        }
        return bonus;
    }

    /// <summary>
    /// Gets a list of all worn equipment items with their data.
    /// </summary>
    /// <returns>A list of <see cref="ItemCardData"/> for all worn equipment.</returns>
    public List<ItemCardData> GetWornEquipment()
    {
        var result = new List<ItemCardData>();
        if (CardFactory.Instance != null)
        {
            foreach (var equipmentId in WornEquipmentIds)
            {
                var item = GetItemData(equipmentId);
                if (item != null)
                {
                    result.Add(item);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Gets a list of all carried equipment items with their data.
    /// </summary>
    /// <returns>A list of <see cref="ItemCardData"/> for all carried equipment.</returns>
    public List<ItemCardData> GetCarriedEquipment()
    {
        var result = new List<ItemCardData>();
        if (CardFactory.Instance != null)
        {
            foreach (var equipmentId in CarriedEquipmentIds)
            {
                var item = GetItemData(equipmentId);
                if (item != null)
                {
                    result.Add(item);
                }
            }
        }
        return result;
    }

    /// <summary>
    /// Creates a deep copy of the player state for network transmission.
    /// </summary>
    /// <returns>A new <see cref="PlayerState"/> instance with copied values.</returns>
    public PlayerState Clone()
    {
        return new PlayerState
        {
            PlayerId = PlayerId,
            PlayerName = PlayerName,
            Level = Level,
            PrimaryRace = PrimaryRace,
            SecondaryRace = SecondaryRace,
            HasMixedBlood = HasMixedBlood,
            PrimaryClass = PrimaryClass,
            SecondaryClass = SecondaryClass,
            HasSuperMunchkin = HasSuperMunchkin,
            Sex = Sex,
            WornEquipmentIds = new List<string>(WornEquipmentIds),
            CarriedEquipmentIds = new List<string>(CarriedEquipmentIds),
            HandCardIds = new List<string>(HandCardIds),
            IsDead = IsDead,
        };
    }

    /// <summary>
    /// Returns a string representation of the player state.
    /// </summary>
    /// <returns>A formatted string with player name, level, race, and class.</returns>
    public override string ToString()
    {
        return $"{PlayerName} (Level {Level}) - {PrimaryRace}/{PrimaryClass}";
    }
}
