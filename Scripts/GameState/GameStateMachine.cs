using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Main game state machine following rules §7 and §16
/// Client-side representation that mirrors server state
/// </summary>
public partial class GameStateMachine
{
    // Game state enums matching rules
    // Using expanded phases beyond MessageProtocol.TurnPhase for full game lifecycle
    public enum MainGamePhase
    {
        Initialization,
        DealCards,
        EquipInitial,
        TurnStart,
        OpenDoor,
        Combat,
        LookForTrouble,
        LootRoom,
        Charity,
        TurnEnd,
        GameOver,
    }

    public enum CombatPhase
    {
        None,
        InteractionWindow,
        Resolution,
        Victory,
        Flee,
        FleeSuccess,
        FleeFailure,
        Punishment,
        CheckWin,
    }

    // Current state
    public MainGamePhase CurrentPhase { get; private set; } = MainGamePhase.Initialization;
    public CombatPhase CurrentCombatPhase { get; private set; } = CombatPhase.None;

    // Game data
    public List<PlayerState> Players { get; private set; } = new();
    public int ActivePlayerIndex { get; private set; } = 0;
    public bool IsClockwise { get; private set; } = true; // Always clockwise per rules

    // Combat state (if in combat)
    public class CombatState
    {
        public string MainMonsterCardId { get; set; } = string.Empty;
        public List<string> WanderingMonsterCardIds { get; set; } = new();
        public int MonsterModifiers { get; set; } = 0; // Bonuses added by other players
        public string AllyPlayerId { get; set; } = string.Empty; // Empty = no ally
        public int PlayerTemporaryBonus { get; set; } = 0;
        public int AllyTemporaryBonus { get; set; } = 0;
        public Dictionary<string, int> TreasureAgreement { get; set; } = new(); // Player ID -> number of treasures
    }

    public CombatState CurrentCombat { get; private set; } = null;

    // Events
    public event Action<MainGamePhase> OnPhaseChanged;
    public event Action<CombatPhase> OnCombatPhaseChanged;
    public event Action<int> OnActivePlayerChanged;

    // Constructor
    public GameStateMachine() { }

    private void ApplyNewPhase(MainGamePhase newPhase)
    {
        CurrentPhase = newPhase;
        OnPhaseChanged?.Invoke(newPhase);

        // Handle phase-specific initialization
        switch (newPhase)
        {
            case MainGamePhase.TurnStart:
                // Reset combat state at start of turn
                CurrentCombat = null;
                CurrentCombatPhase = CombatPhase.None;
                break;

            case MainGamePhase.Combat:
                // Initialize combat if not already
                if (CurrentCombat == null)
                {
                    CurrentCombat = new CombatState();
                    CurrentCombatPhase = CombatPhase.InteractionWindow;
                    OnCombatPhaseChanged?.Invoke(CombatPhase.InteractionWindow);
                }
                break;
        }
    }

    // State transitions
    public void TransitionToPhase(MainGamePhase newPhase)
    {
        if (CurrentPhase == newPhase)
            return;

        // Validate transition (basic rules)
        bool isValid = IsValidTransition(CurrentPhase, newPhase);

        if (!isValid)
        {
            GD.PrintErr($"Invalid state transition: {CurrentPhase} -> {newPhase}");
            return;
        }

        ApplyNewPhase(newPhase);
    }

    // Set phase directly (for server updates)
    public void SetPhase(MainGamePhase newPhase)
    {
        if (CurrentPhase == newPhase)
            return;

        ApplyNewPhase(newPhase);
    }

    public void TransitionToCombatPhase(CombatPhase newPhase)
    {
        if (CurrentCombatPhase == newPhase)
            return;

        // Must be in combat to change combat phase
        if (CurrentPhase != MainGamePhase.Combat)
        {
            GD.PrintErr($"Cannot change combat phase when not in combat. Current: {CurrentPhase}");
            return;
        }

        CurrentCombatPhase = newPhase;
        OnCombatPhaseChanged?.Invoke(newPhase);
    }

    // Helper methods
    public PlayerState GetActivePlayer()
    {
        if (Players.Count == 0)
            return null;

        return Players[ActivePlayerIndex];
    }

    public PlayerState GetPlayerById(string playerId)
    {
        return Players.Find(p => p.PlayerId == playerId);
    }

    public void SetActivePlayer(int index)
    {
        if (index < 0 || index >= Players.Count)
        {
            GD.PrintErr($"Invalid player index: {index}");
            return;
        }

        ActivePlayerIndex = index;
        OnActivePlayerChanged?.Invoke(index);
    }

    public void NextPlayer()
    {
        int nextIndex = ActivePlayerIndex;

        if (IsClockwise)
        {
            nextIndex = (ActivePlayerIndex + 1) % Players.Count;
        }
        else
        {
            nextIndex = (ActivePlayerIndex - 1 + Players.Count) % Players.Count;
        }

        SetActivePlayer(nextIndex);
    }

    // State validation
    private bool IsValidTransition(MainGamePhase from, MainGamePhase to)
    {
        // Basic validation - will be expanded with full state machine logic
        // Following rules §7 and diagram §16

        switch (from)
        {
            case MainGamePhase.Initialization:
                return to == MainGamePhase.DealCards;

            case MainGamePhase.DealCards:
                return to == MainGamePhase.EquipInitial;

            case MainGamePhase.EquipInitial:
                return to == MainGamePhase.TurnStart;

            case MainGamePhase.TurnStart:
                return to == MainGamePhase.OpenDoor;

            case MainGamePhase.OpenDoor:
                return to == MainGamePhase.Combat
                    || to == MainGamePhase.LookForTrouble
                    || to == MainGamePhase.LootRoom;

            case MainGamePhase.LookForTrouble:
                return to == MainGamePhase.Combat || to == MainGamePhase.LootRoom;

            case MainGamePhase.Combat:
                return to == MainGamePhase.Charity;

            case MainGamePhase.LootRoom:
                return to == MainGamePhase.Charity;

            case MainGamePhase.Charity:
                return to == MainGamePhase.TurnEnd;

            case MainGamePhase.TurnEnd:
                return to == MainGamePhase.TurnStart || to == MainGamePhase.GameOver;

            default:
                return false;
        }
    }

    // Combat calculations (client-side preview only - server is authoritative)
    public int CalculatePlayerCombatForce(string playerId)
    {
        var player = GetPlayerById(playerId);
        if (player == null)
            return 0;

        int force = player.Level;

        // TODO: Add equipment bonuses from CardFactory
        // TODO: Add temporary bonuses from CurrentCombat

        // Class abilities (rules §5.2)
        if (player.PrimaryClass == ClassType.Warrior)
        {
            // Warrior can discard up to 3 cards for +1 each
            // This would be handled when the ability is activated
        }

        return force;
    }

    public int CalculateMonsterCombatForce()
    {
        if (CurrentCombat == null)
            return 0;

        int force = 0;

        // TODO: Get monster level from CardFactory using MainMonsterCardId
        // TODO: Add wandering monster levels
        // TODO: Add monster modifiers

        return force;
    }

    // For debugging
    public override string ToString()
    {
        string combatInfo = CurrentCombat != null ? $" (Combat: {CurrentCombatPhase})" : "";
        return $"Game Phase: {CurrentPhase}{combatInfo}, Active Player: {ActivePlayerIndex}";
    }
}
