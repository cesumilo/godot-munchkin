using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Represents a player's state according to game rules §5
/// Client-side representation that mirrors server state
/// </summary>
public partial class PlayerState
{
    // Player identification
    public string PlayerId { get; set; } = string.Empty;
    public string PlayerName { get; set; } = string.Empty;

    // Character attributes (rules §5)
    public int Level { get; set; } = 1; // Range: 1-10

    // Race system (null = Human)
    public RaceType PrimaryRace { get; set; } = RaceType.None; // None = Human
    public RaceType SecondaryRace { get; set; } = RaceType.None; // Only non-null if MixedBlood active
    public bool HasMixedBlood { get; set; } = false;

    // Class system (null = no class)
    public ClassType PrimaryClass { get; set; } = ClassType.None;
    public ClassType SecondaryClass { get; set; } = ClassType.None; // Only non-null if SuperMunchkin active
    public bool HasSuperMunchkin { get; set; } = false;

    public SexType Sex { get; set; } = SexType.None;

    // Equipment (rules §9)
    // Worn equipment: bonus active, occupies slot
    public List<string> WornEquipmentIds { get; set; } = new();

    // Carried equipment: in play but not equipped, no bonus
    public List<string> CarriedEquipmentIds { get; set; } = new();

    // Hand cards (rules §7.1)
    public List<string> HandCardIds { get; set; } = new();

    // Status
    public bool IsDead { get; set; } = false;

    // Calculated properties
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

    // Constructor
    public PlayerState() { }

    public PlayerState(string playerId, string playerName)
    {
        PlayerId = playerId;
        PlayerName = playerName;
    }

    // Helper methods
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

    public bool HasItemInPlay(string cardId)
    {
        return WornEquipmentIds.Contains(cardId) || CarriedEquipmentIds.Contains(cardId);
    }

    public bool HasCardInHand(string cardId)
    {
        return HandCardIds.Contains(cardId);
    }

    // Add/remove methods
    public bool AddToHand(string cardId)
    {
        if (!HandCardIds.Contains(cardId))
        {
            HandCardIds.Add(cardId);
            return true;
        }
        return false;
    }

    public bool RemoveFromHand(string cardId)
    {
        return HandCardIds.Remove(cardId);
    }

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

    // CardData helper methods
    public ItemCardData GetItemData(string cardId)
    {
        if (CardFactory.Instance == null)
            return null;

        return CardFactory.Instance.GetCardById<ItemCardData>(cardId);
    }

    public CardData GetCardData(string cardId)
    {
        if (CardFactory.Instance == null)
            return null;

        return CardFactory.Instance.GetCardById(cardId);
    }

    public T GetCardData<T>(string cardId)
        where T : CardData
    {
        if (CardFactory.Instance == null)
            return null;

        return CardFactory.Instance.GetCardById<T>(cardId);
    }

    // Equipment management helpers
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

    // Clone for network transmission (deep copy)
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

    // For debugging
    public override string ToString()
    {
        return $"{PlayerName} (Level {Level}) - {PrimaryRace}/{PrimaryClass}";
    }
}
