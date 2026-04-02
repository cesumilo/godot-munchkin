using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// Manages the game state machine following Munchkin rules §7 and §16.
/// Client-side representation that mirrors the authoritative server state.
/// </summary>
/// <remarks>
/// Per §7 and §16: Implements the turn-based game flow with phases including initialization,
/// card dealing, turn phases (Open Door, Combat, Look For Trouble, Loot Room, Charity),
/// and game over. Also manages nested combat state machine per §8.
/// </remarks>
public partial class GameStateMachine
{
    // Game state enums matching rules
    // Using expanded phases beyond MessageProtocol.TurnPhase for full game lifecycle

    /// <summary>
    /// Defines the main phases of a Munchkin game session.
    /// </summary>
    /// <remarks>
    /// Per §7 and §16: Follows the complete game lifecycle from initialization through game over.
    /// </remarks>
    public enum MainGamePhase
    {
        /// <summary>
        /// Initial game setup phase.
        /// </summary>
        Initialization,

        /// <summary>
        /// Per §6: Deals 4 Donjon + 4 Trésor cards to each player.
        /// </summary>
        DealCards,

        /// <summary>
        /// Per §6: Players may equip initial Race, Class, and Item cards.
        /// </summary>
        EquipInitial,

        /// <summary>
        /// Per §7: Beginning of a player's turn, resets combat state.
        /// </summary>
        TurnStart,

        /// <summary>
        /// Per §7.1: Player opens the top card of the Donjon deck face-up.
        /// </summary>
        OpenDoor,

        /// <summary>
        /// Per §8: Combat against a monster.
        /// </summary>
        Combat,

        /// <summary>
        /// Per §7.2: Player may play a monster from hand if no combat occurred.
        /// </summary>
        LookForTrouble,

        /// <summary>
        /// Per §7.3: Player draws a face-down Donjon card if no combat occurred.
        /// </summary>
        LootRoom,

        /// <summary>
        /// Per §7.4: Player must reduce hand to 5 cards maximum.
        /// </summary>
        Charity,

        /// <summary>
        /// End of current player's turn, transitions to next player.
        /// </summary>
        TurnEnd,

        /// <summary>
        /// Game has ended with a winner.
        /// </summary>
        GameOver,
    }

    /// <summary>
    /// Defines the nested state machine for combat resolution per §8.
    /// </summary>
    /// <remarks>
    /// Per §8.2-§8.8: Manages combat phases including interaction window, resolution,
    /// victory/defeat, flee attempts, and punishments.
    /// </remarks>
    public enum CombatPhase
    {
        /// <summary>
        /// Not currently in combat.
        /// </summary>
        None,

        /// <summary>
        /// Per §8.2: Window where players can play cards, use abilities, and negotiate alliances.
        /// </summary>
        InteractionWindow,

        /// <summary>
        /// Per §8.4: Calculating final forces and determining victory or defeat.
        /// </summary>
        Resolution,

        /// <summary>
        /// Per §8.5: Player defeated the monster(s).
        /// </summary>
        Victory,

        /// <summary>
        /// Per §8.6: Player attempting to flee from combat.
        /// </summary>
        Flee,

        /// <summary>
        /// Per §8.6: Flee attempt succeeded (rolled ≥ 5, modified by bonuses/maluses).
        /// </summary>
        FleeSuccess,

        /// <summary>
        /// Per §8.6: Flee attempt failed (rolled < 5, modified).
        /// </summary>
        FleeFailure,

        /// <summary>
        /// Per §8.7: Applying punishment from failed flee attempt.
        /// </summary>
        Punishment,

        /// <summary>
        /// Per §8.5: Checking if player reached level 10 and won the game.
        /// </summary>
        CheckWin,
    }

    // Current state
    /// <summary>
    /// Gets the current main game phase.
    /// </summary>
    public MainGamePhase CurrentPhase { get; private set; } = MainGamePhase.Initialization;

    /// <summary>
    /// Gets the current combat phase.
    /// </summary>
    /// <value>CombatPhase.None when not in combat.</value>
    public CombatPhase CurrentCombatPhase { get; private set; } = CombatPhase.None;

    // Game data
    /// <summary>
    /// Gets the list of all players in the game.
    /// </summary>
    /// <remarks>Per §3: Supports 3-6 players.</remarks>
    public List<PlayerState> Players { get; private set; } = new();

    /// <summary>
    /// Gets the index of the currently active player in the Players list.
    /// </summary>
    public int ActivePlayerIndex { get; private set; } = 0;

    /// <summary>
    /// Gets whether the game proceeds in clockwise direction.
    /// </summary>
    /// <value>Always true per §6: Game is played clockwise.</value>
    public bool IsClockwise { get; private set; } = true;

    // Combat state (if in combat)
    /// <summary>
    /// Tracks the state of an active combat encounter.
    /// </summary>
    /// <remarks>
    /// Per §8: Contains all data needed for combat resolution including monster(s),
    /// modifiers, ally information, and temporary bonuses.
    /// </remarks>
    public class CombatState
    {
        /// <summary>
        /// Gets or sets the card ID of the main monster in combat.
        /// </summary>
        public string MainMonsterCardId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of wandering monster card IDs added to combat.
        /// </summary>
        /// <remarks>Per §8.8: Multiple monsters can be in the same combat.</remarks>
        public List<string> WanderingMonsterCardIds { get; set; } = new();

        /// <summary>
        /// Gets or sets the total modifier applied to monster force by other players.
        /// </summary>
        public int MonsterModifiers { get; set; } = 0;

        /// <summary>
        /// Gets or sets the player ID of the ally helping in combat.
        /// </summary>
        /// <value>Per §8.2: Empty string means no ally. Maximum one ally per combat.</value>
        public string AllyPlayerId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets temporary bonuses applied to the active player during this combat.
        /// </summary>
        public int PlayerTemporaryBonus { get; set; } = 0;

        /// <summary>
        /// Gets or sets temporary bonuses applied to the ally during this combat.
        /// </summary>
        public int AllyTemporaryBonus { get; set; } = 0;

        /// <summary>
        /// Gets or sets the treasure distribution agreement between active player and ally.
        /// </summary>
        /// <remarks>Per §8.5: Maps player ID to number of treasures they receive.</remarks>
        public Dictionary<string, int> TreasureAgreement { get; set; } = new();
    }

    /// <summary>
    /// Gets the current combat state, or null if not in combat.
    /// </summary>
    public CombatState CurrentCombat { get; private set; } = null;

    // Events
    /// <summary>
    /// Emitted when the main game phase changes.
    /// </summary>
    /// <param name="newPhase">The new game phase.</param>
    public event Action<MainGamePhase> OnPhaseChanged;

    /// <summary>
    /// Emitted when the combat phase changes.
    /// </summary>
    /// <param name="newPhase">The new combat phase.</param>
    public event Action<CombatPhase> OnCombatPhaseChanged;

    /// <summary>
    /// Emitted when the active player changes.
    /// </summary>
    /// <param name="playerIndex">The index of the new active player.</param>
    public event Action<int> OnActivePlayerChanged;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameStateMachine"/> class.
    /// </summary>
    public GameStateMachine() { }

    /// <summary>
    /// Applies a new phase and triggers initialization logic.
    /// </summary>
    /// <param name="newPhase">The phase to transition to.</param>
    /// <remarks>
    /// Called internally to handle phase-specific setup. Emits OnPhaseChanged event.
    /// </remarks>
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

    /// <summary>
    /// Transitions to a new main game phase with validation.
    /// </summary>
    /// <param name="newPhase">The target phase to transition to.</param>
    /// <remarks>
    /// Per §7: Validates transitions follow the state machine rules. Prints error for invalid transitions.
    /// </remarks>
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

    /// <summary>
    /// Sets the phase directly without validation, used for server state updates.
    /// </summary>
    /// <param name="newPhase">The phase to set.</param>
    public void SetPhase(MainGamePhase newPhase)
    {
        if (CurrentPhase == newPhase)
            return;

        ApplyNewPhase(newPhase);
    }

    /// <summary>
    /// Transitions to a new combat phase.
    /// </summary>
    /// <param name="newPhase">The target combat phase.</param>
    /// <remarks>
    /// Per §8: Must be in Combat main phase to change combat phases.
    /// </remarks>
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

    /// <summary>
    /// Gets the currently active player.
    /// </summary>
    /// <returns>The <see cref="PlayerState"/> of the active player, or null if no players.</returns>
    public PlayerState GetActivePlayer()
    {
        if (Players.Count == 0)
            return null;

        return Players[ActivePlayerIndex];
    }

    /// <summary>
    /// Finds a player by their unique ID.
    /// </summary>
    /// <param name="playerId">The unique player identifier.</param>
    /// <returns>The <see cref="PlayerState"/> if found; null otherwise.</returns>
    public PlayerState GetPlayerById(string playerId)
    {
        return Players.Find(p => p.PlayerId == playerId);
    }

    /// <summary>
    /// Sets the active player by index.
    /// </summary>
    /// <param name="index">The index in the Players list.</param>
    /// <exception cref="ArgumentOutOfRangeException">If index is invalid.</exception>
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

    /// <summary>
    /// Advances to the next player in turn order.
    /// </summary>
    /// <remarks>
    /// Per §6: Always proceeds clockwise (index + 1, wrapping around).
    /// </remarks>
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

    /// <summary>
    /// Validates whether a state transition is allowed.
    /// </summary>
    /// <param name="from">The current phase.</param>
    /// <param name="to">The target phase.</param>
    /// <returns>True if the transition is valid; false otherwise.</returns>
    /// <remarks>
    /// Per §7 and §16 diagram: Enforces the valid state machine transitions.
    /// </remarks>
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

    /// <summary>
    /// Calculates the combat force for a player (client-side preview).
    /// </summary>
    /// <param name="playerId">The player ID to calculate force for.</param>
    /// <returns>The calculated combat force.</returns>
    /// <remarks>
    /// Per §8.3: Force = level + equipment bonuses + temporary bonuses + class abilities.
    /// This is client-side preview only; server is authoritative for actual combat resolution.
    /// </remarks>
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

    /// <summary>
    /// Calculates the total monster combat force (client-side preview).
    /// </summary>
    /// <returns>The calculated monster force.</returns>
    /// <remarks>
    /// Per §8.3: Monster force = base level + modifiers + racial bonuses.
    /// This is client-side preview only; server is authoritative.
    /// </remarks>
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

    /// <summary>
    /// Returns a string representation of the game state.
    /// </summary>
    /// <returns>A formatted string with current phase and active player.</returns>
    public override string ToString()
    {
        string combatInfo = CurrentCombat != null ? $" (Combat: {CurrentCombatPhase})" : "";
        return $"Game Phase: {CurrentPhase}{combatInfo}, Active Player: {ActivePlayerIndex}";
    }
}
