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

            // TODO: Add equipment bonuses from CardFactory
            // This will be implemented when we integrate with CardData system

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
        // TODO: Implement equipment slot validation (rules §9.3)
        // Check slots, restrictions, big item limits
        return true; // Placeholder
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
        if (!WornEquipmentIds.Contains(cardId))
        {
            WornEquipmentIds.Add(cardId);
            return true;
        }
        return false;
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
