using System.Text.Json;
using Godot;

/// <summary>
/// Test scene for GameState system
/// Tests PlayerState, GameStateMachine, and GameStateManager integration
/// </summary>
public partial class GameStateTest : Node
{
    private GameStateManager _gameStateManager;

    public override void _Ready()
    {
        GD.Print("=== Starting GameState Tests ===");

        // GameStateManager is an autoload, get it from the singleton
        _gameStateManager = GameStateManager.Instance;

        if (_gameStateManager == null)
        {
            GD.PrintErr("FAIL: GameStateManager instance not found (check autoloads)");
            return;
        }

        GD.Print("PASS: GameStateManager loaded");

        // Run tests
        TestPlayerStateSerialization();
        TestGameStateMachineTransitions();
        TestGameStateManagerIntegration();

        GD.Print("=== GameState Tests Complete ===");
    }

    private void TestPlayerStateSerialization()
    {
        GD.Print("\n--- Test 1: PlayerState Serialization ---");

        // Create test player
        var player = new PlayerState
        {
            PlayerId = "test-player-001",
            PlayerName = "Test Player",
            Level = 3,
            PrimaryRace = RaceType.Elf,
            PrimaryClass = ClassType.Warrior,
            Sex = SexType.Male,
        };

        player.AddToHand("card_monster_goblin");
        player.AddToHand("card_item_broadsword");
        player.EquipItem("card_item_helmet");

        // Test JSON serialization
        try
        {
            var json = JsonSerializer.Serialize(
                player,
                new JsonSerializerOptions { WriteIndented = true }
            );
            GD.Print($"PASS: Serialized PlayerState to JSON ({json.Length} chars)");

            // Test deserialization
            var deserialized = JsonSerializer.Deserialize<PlayerState>(json);

            if (
                deserialized != null
                && deserialized.PlayerId == player.PlayerId
                && deserialized.Level == player.Level
                && deserialized.HandCardIds.Count == player.HandCardIds.Count
            )
            {
                GD.Print("PASS: PlayerState deserialization successful");
            }
            else
            {
                GD.PrintErr("FAIL: PlayerState deserialization failed");
            }
        }
        catch (System.Exception ex)
        {
            GD.PrintErr($"FAIL: Serialization error: {ex.Message}");
        }

        // Test clone
        var cloned = player.Clone();
        if (cloned.PlayerId == player.PlayerId && cloned.Level == player.Level)
        {
            GD.Print("PASS: PlayerState clone successful");
        }
        else
        {
            GD.PrintErr("FAIL: PlayerState clone failed");
        }
    }

    private void TestGameStateMachineTransitions()
    {
        GD.Print("\n--- Test 2: GameStateMachine Transitions ---");

        var stateMachine = new GameStateMachine();

        // Test initial state
        if (stateMachine.CurrentPhase == GameStateMachine.MainGamePhase.Initialization)
        {
            GD.Print("PASS: Initial state is Initialization");
        }
        else
        {
            GD.PrintErr($"FAIL: Expected Initialization, got {stateMachine.CurrentPhase}");
        }

        // Test valid transitions
        stateMachine.TransitionToPhase(GameStateMachine.MainGamePhase.DealCards);
        if (stateMachine.CurrentPhase == GameStateMachine.MainGamePhase.DealCards)
        {
            GD.Print("PASS: Transition to DealCards successful");
        }
        else
        {
            GD.PrintErr($"FAIL: Expected DealCards, got {stateMachine.CurrentPhase}");
        }

        // Test invalid transition (should be rejected)
        stateMachine.TransitionToPhase(GameStateMachine.MainGamePhase.Combat);
        if (stateMachine.CurrentPhase != GameStateMachine.MainGamePhase.Combat)
        {
            GD.Print("PASS: Invalid transition to Combat was rejected");
        }
        else
        {
            GD.PrintErr("FAIL: Invalid transition to Combat was accepted");
        }

        // Test player management
        var player1 = new PlayerState("player1", "Player 1");
        var player2 = new PlayerState("player2", "Player 2");

        stateMachine.Players.Add(player1);
        stateMachine.Players.Add(player2);
        stateMachine.SetActivePlayer(0);

        var activePlayer = stateMachine.GetActivePlayer();
        if (activePlayer != null && activePlayer.PlayerId == "player1")
        {
            GD.Print("PASS: Active player set correctly");
        }
        else
        {
            GD.PrintErr("FAIL: Active player not set correctly");
        }

        stateMachine.NextPlayer();
        var nextPlayer = stateMachine.GetActivePlayer();
        if (nextPlayer != null && nextPlayer.PlayerId == "player2")
        {
            GD.Print("PASS: Next player transition successful");
        }
        else
        {
            GD.PrintErr("FAIL: Next player transition failed");
        }
    }

    private void TestGameStateManagerIntegration()
    {
        GD.Print("\n--- Test 3: GameStateManager Integration ---");

        // Test mock data parsing
        _gameStateManager.TestWithMockData();

        var stateMachine = _gameStateManager.StateMachine;

        // Verify players were parsed
        if (stateMachine.Players.Count >= 2)
        {
            GD.Print($"PASS: {stateMachine.Players.Count} players parsed from mock data");

            // Check player details
            var player1 = stateMachine.Players[0];
            if (player1.PlayerName == "Test Player 1" && player1.PrimaryClass == ClassType.Warrior)
            {
                GD.Print("PASS: Player 1 data parsed correctly");
            }
            else
            {
                GD.PrintErr("FAIL: Player 1 data incorrect");
            }
        }
        else
        {
            GD.PrintErr($"FAIL: Expected 2+ players, got {stateMachine.Players.Count}");
        }

        // Verify game phase - mock data sets OPEN_DOOR phase
        if (stateMachine.CurrentPhase == GameStateMachine.MainGamePhase.OpenDoor)
        {
            GD.Print("PASS: Game phase set to OpenDoor from mock data");
        }
        else
        {
            GD.PrintErr($"FAIL: Expected OpenDoor phase, got {stateMachine.CurrentPhase}");
        }

        // Verify active player
        var activePlayer = stateMachine.GetActivePlayer();
        if (activePlayer != null && activePlayer.PlayerId == "player1")
        {
            GD.Print("PASS: Active player set correctly from mock data");
        }
        else
        {
            GD.PrintErr("FAIL: Active player not set correctly");
        }

        // Test local player reference
        var localPlayer = _gameStateManager.LocalPlayer;
        if (localPlayer != null && localPlayer.PlayerId == "player1")
        {
            GD.Print("PASS: Local player reference set");
        }
        else
        {
            GD.Print("NOTE: Local player not auto-detected (expected in mock test)");
        }
    }
}
