using Godot;

namespace Tests.Integration;

/// <summary>
/// Integration test for GameState system.
/// Tests PlayerState, GameStateMachine, and GameStateManager together.
/// Requires scene: Scenes/Tests/Integration/GameStateTest.tscn
/// </summary>
public partial class GameStateTest : Node
{
    private GameStateManager _gameStateManager;
    private Label3D _statusLabel;

    public override void _Ready()
    {
        GameLogger.Info("=== GameState Integration Test Started ===", nameof(GameStateTest));

        _statusLabel = GetNode<Label3D>("StatusLabel");
        UpdateStatus("Loading...");

        // Get GameStateManager
        _gameStateManager = GameStateManager.Instance;
        if (_gameStateManager == null)
        {
            GameLogger.Error("GameStateManager instance not found", nameof(GameStateTest));
            UpdateStatus("FAIL: GameStateManager not found");
            return;
        }

        GameLogger.Info("GameStateManager loaded", nameof(GameStateTest));

        // Run tests
        TestPlayerStateSerialization();
        TestGameStateMachineTransitions();
        TestGameStateManagerIntegration();
        TestPlayerStateCardFactoryIntegration();

        GameLogger.Info("=== GameState Integration Test Complete ===", nameof(GameStateTest));
        UpdateStatus("Test Complete - Check Console");
    }

    private void TestPlayerStateSerialization()
    {
        GameLogger.Info("\n--- Test: PlayerState Serialization ---", nameof(GameStateTest));

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

        GameLogger.Info(
            $"Created player: {player.PlayerName}, Level {player.Level}",
            nameof(GameStateTest)
        );
        GameLogger.Info($"Hand size: {player.HandCardIds.Count}", nameof(GameStateTest));

        // Test clone
        var cloned = player.Clone();
        if (cloned.PlayerId == player.PlayerId && cloned.Level == player.Level)
        {
            GameLogger.Info("PASS: PlayerState clone successful", nameof(GameStateTest));
        }
        else
        {
            GameLogger.Error("FAIL: PlayerState clone failed", nameof(GameStateTest));
        }
    }

    private void TestGameStateMachineTransitions()
    {
        GameLogger.Info("\n--- Test: GameStateMachine Transitions ---", nameof(GameStateTest));

        var stateMachine = new GameStateMachine();

        // Test initial state
        if (stateMachine.CurrentPhase == GameStateMachine.MainGamePhase.Initialization)
        {
            GameLogger.Info("PASS: Initial state is Initialization", nameof(GameStateTest));
        }
        else
        {
            GameLogger.Error(
                $"FAIL: Expected Initialization, got {stateMachine.CurrentPhase}",
                nameof(GameStateTest)
            );
        }

        // Test valid transition
        stateMachine.TransitionToPhase(GameStateMachine.MainGamePhase.DealCards);
        if (stateMachine.CurrentPhase == GameStateMachine.MainGamePhase.DealCards)
        {
            GameLogger.Info("PASS: Transition to DealCards successful", nameof(GameStateTest));
        }
        else
        {
            GameLogger.Error(
                $"FAIL: Expected DealCards, got {stateMachine.CurrentPhase}",
                nameof(GameStateTest)
            );
        }

        // Test invalid transition (should be rejected)
        stateMachine.TransitionToPhase(GameStateMachine.MainGamePhase.Combat);
        if (stateMachine.CurrentPhase != GameStateMachine.MainGamePhase.Combat)
        {
            GameLogger.Info(
                "PASS: Invalid transition to Combat was rejected",
                nameof(GameStateTest)
            );
        }
        else
        {
            GameLogger.Error(
                "FAIL: Invalid transition to Combat was accepted",
                nameof(GameStateTest)
            );
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
            GameLogger.Info("PASS: Active player set correctly", nameof(GameStateTest));
        }
        else
        {
            GameLogger.Error("FAIL: Active player not set correctly", nameof(GameStateTest));
        }
    }

    private void TestGameStateManagerIntegration()
    {
        GameLogger.Info("\n--- Test: GameStateManager Integration ---", nameof(GameStateTest));

        // Test mock data
        _gameStateManager.TestWithMockData();

        var stateMachine = _gameStateManager.StateMachine;

        // Verify players
        if (stateMachine.Players.Count >= 2)
        {
            GameLogger.Info(
                $"PASS: {stateMachine.Players.Count} players parsed from mock data",
                nameof(GameStateTest)
            );

            var player1 = stateMachine.Players[0];
            if (player1.PlayerName == "Test Player 1" && player1.PrimaryClass == ClassType.Warrior)
            {
                GameLogger.Info("PASS: Player 1 data parsed correctly", nameof(GameStateTest));
            }
            else
            {
                GameLogger.Error("FAIL: Player 1 data incorrect", nameof(GameStateTest));
            }
        }
        else
        {
            GameLogger.Error(
                $"FAIL: Expected 2+ players, got {stateMachine.Players.Count}",
                nameof(GameStateTest)
            );
        }

        // Verify phase
        if (stateMachine.CurrentPhase == GameStateMachine.MainGamePhase.OpenDoor)
        {
            GameLogger.Info(
                "PASS: Game phase set to OpenDoor from mock data",
                nameof(GameStateTest)
            );
        }
        else
        {
            GameLogger.Error(
                $"FAIL: Expected OpenDoor phase, got {stateMachine.CurrentPhase}",
                nameof(GameStateTest)
            );
        }
    }

    private void TestPlayerStateCardFactoryIntegration()
    {
        GameLogger.Info(
            "\n--- Test: PlayerState + CardFactory Integration ---",
            nameof(GameStateTest)
        );

        var cardFactory = CardFactory.Instance;
        if (cardFactory == null)
        {
            GameLogger.Error("FAIL: CardFactory not loaded", nameof(GameStateTest));
            return;
        }

        GameLogger.Info(
            $"CardFactory loaded with {cardFactory.GetTotalCardCount()} cards",
            nameof(GameStateTest)
        );

        var player = new PlayerState
        {
            PlayerId = "integration-test-001",
            PlayerName = "Integration Test Player",
            Level = 5,
            PrimaryRace = RaceType.Elf,
            PrimaryClass = ClassType.Warrior,
            Sex = SexType.Male,
        };

        // Test equipping
        string broadSwordId = "item_broad_sword_001";
        bool canEquipBroadSword = player.CanEquipItem(broadSwordId);
        GameLogger.Info($"Can equip Broad Sword? {canEquipBroadSword}", nameof(GameStateTest));

        if (canEquipBroadSword)
        {
            bool equipped = player.EquipItem(broadSwordId);
            if (equipped)
            {
                GameLogger.Info("PASS: Broad Sword equipped successfully", nameof(GameStateTest));

                int totalBonus = player.TotalCombatBonus;
                if (totalBonus == 8) // Level 5 + Bonus 3
                {
                    GameLogger.Info(
                        "PASS: Equipment bonus calculated correctly",
                        nameof(GameStateTest)
                    );
                }
                else
                {
                    GameLogger.Error(
                        $"FAIL: Expected total bonus 8, got {totalBonus}",
                        nameof(GameStateTest)
                    );
                }
            }
            else
            {
                GameLogger.Error("FAIL: Failed to equip Broad Sword", nameof(GameStateTest));
            }
        }
        else
        {
            GameLogger.Error(
                "FAIL: Elf Warrior should be able to equip Broad Sword",
                nameof(GameStateTest)
            );
        }
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.Text = message;
    }
}
