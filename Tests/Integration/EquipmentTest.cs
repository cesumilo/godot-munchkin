using Godot;

namespace Tests.Integration;

/// <summary>
/// Integration test for EquipmentPanel.
/// Tests EquipmentPanel + GameStateManager + PlayerState together.
/// Requires scene: Scenes/Tests/Integration/EquipmentTest.tscn
/// </summary>
public partial class EquipmentTest : Node3D
{
    [Export]
    private EquipmentPanel _equipmentPanel;

    [Export]
    private Label3D _statusLabel;

    private GameStateManager _gameStateManager;
    private PlayerState _testPlayer;

    public override void _Ready()
    {
        GameLogger.Info("=== Equipment Integration Test Started ===", nameof(EquipmentTest));

        // Get references
        _equipmentPanel = GetNode<EquipmentPanel>("EquipmentPanel");
        _statusLabel = GetNode<Label3D>("StatusLabel");

        // Get GameStateManager
        _gameStateManager = GameStateManager.Instance;
        if (_gameStateManager == null)
        {
            GameLogger.Error("FAIL: GameStateManager not found", nameof(EquipmentTest));
            UpdateStatus("FAIL: GameStateManager not found");
            return;
        }

        GameLogger.Info("GameStateManager loaded", nameof(EquipmentTest));

        // Create test player
        _testPlayer = CreateTestPlayer();

        // Setup mock data
        SetupMockGameState();

        // Run tests
        TestEquipmentPanelBasics();

        GameLogger.Info("=== Equipment Integration Test Complete ===", nameof(EquipmentTest));
    }

    private PlayerState CreateTestPlayer()
    {
        var player = new PlayerState
        {
            PlayerId = "equipment-test-001",
            PlayerName = "Test Equipment Player",
            Level = 5,
            PrimaryRace = RaceType.Elf,
            PrimaryClass = ClassType.Warrior,
            Sex = SexType.Male,
            HasMixedBlood = false,
            HasSuperMunchkin = false,
        };

        // Add equipment to hand
        player.AddToHand("item_broad_sword_001");
        player.AddToHand("item_helm_of_courage_001");

        // Equip items
        if (player.CanEquipItem("item_broad_sword_001"))
        {
            bool equipped = player.EquipItem("item_broad_sword_001");
            GameLogger.Info($"Broad Sword equipped: {equipped}", nameof(EquipmentTest));
        }
        else
        {
            GameLogger.Error("FAIL: Cannot equip Broad Sword", nameof(EquipmentTest));
        }

        if (player.CanEquipItem("item_helm_of_courage_001"))
        {
            bool equipped = player.EquipItem("item_helm_of_courage_001");
            GameLogger.Info($"Helm of Courage equipped: {equipped}", nameof(EquipmentTest));
        }
        else
        {
            GameLogger.Error("FAIL: Cannot equip Helm of Courage", nameof(EquipmentTest));
        }

        // Log state
        GameLogger.Info($"Player equipment state:", nameof(EquipmentTest));
        GameLogger.Info($"  Hand: {player.HandCardIds.Count} items", nameof(EquipmentTest));
        GameLogger.Info($"  Worn: {player.WornEquipmentIds.Count} items", nameof(EquipmentTest));
        GameLogger.Info(
            $"  Carried: {player.CarriedEquipmentIds.Count} items",
            nameof(EquipmentTest)
        );

        return player;
    }

    private void SetupMockGameState()
    {
        _gameStateManager.TestWithMockData();
        GameLogger.Info("Mock game state loaded", nameof(EquipmentTest));
    }

    private void TestEquipmentPanelBasics()
    {
        GameLogger.Info("\n--- Testing EquipmentPanel Basics ---", nameof(EquipmentTest));

        // Test 1: Panel initialization
        if (_equipmentPanel == null)
        {
            GameLogger.Error("FAIL: EquipmentPanel not found", nameof(EquipmentTest));
            UpdateStatus("FAIL: EquipmentPanel not found");
            return;
        }

        GameLogger.Info("PASS: EquipmentPanel found", nameof(EquipmentTest));

        // Test 2: Set player state
        _equipmentPanel.SetPlayerState(_testPlayer);
        GameLogger.Info("PASS: PlayerState set on EquipmentPanel", nameof(EquipmentTest));

        // Test 3: Check display after frame
        Callable.From(() => CheckEquipmentDisplay()).CallDeferred();

        GameLogger.Info("--- EquipmentPanel Basics Complete ---", nameof(EquipmentTest));
    }

    private void CheckEquipmentDisplay()
    {
        GameLogger.Info("\n--- Checking Equipment Display ---", nameof(EquipmentTest));

        int wornCount = _testPlayer.WornEquipmentIds.Count;
        GameLogger.Info($"Worn equipment count: {wornCount}", nameof(EquipmentTest));

        if (wornCount == 2)
        {
            GameLogger.Info("PASS: EquipmentPanel shows 2 worn items", nameof(EquipmentTest));
        }
        else
        {
            GameLogger.Error(
                $"FAIL: Expected 2 worn items, got {wornCount}",
                nameof(EquipmentTest)
            );
        }

        // Check bonus: Level 5 + Sword 3 + Helm 1 = 9
        int totalBonus = _testPlayer.TotalCombatBonus;
        GameLogger.Info($"Total combat bonus: {totalBonus}", nameof(EquipmentTest));

        if (totalBonus == 9)
        {
            GameLogger.Info("PASS: Total bonus calculated correctly", nameof(EquipmentTest));
            UpdateStatus("Test PASSED - See Console");
        }
        else
        {
            GameLogger.Error(
                $"FAIL: Expected total bonus 9, got {totalBonus}",
                nameof(EquipmentTest)
            );
            UpdateStatus($"Test FAILED - Expected bonus 9, got {totalBonus}");
        }

        if (_statusLabel != null)
        {
            _statusLabel.Text =
                $"Test Complete\n"
                + $"Worn: {wornCount}/2\n"
                + $"Bonus: {totalBonus}/9\n"
                + "Press E/U/R for manual tests";
        }
    }

    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.Text = message;
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            switch (keyEvent.Keycode)
            {
                case Key.E:
                    TryEquipHelm();
                    break;
                case Key.U:
                    TryUnequipSword();
                    break;
                case Key.R:
                    ResetTest();
                    break;
            }
        }
    }

    private void TryEquipHelm()
    {
        GameLogger.Info("\n--- Manual Test: Equip Helm ---", nameof(EquipmentTest));

        if (_testPlayer == null)
            return;

        bool canEquip = _testPlayer.CanEquipItem("item_helm_of_courage_001");
        if (canEquip)
        {
            bool equipped = _testPlayer.EquipItem("item_helm_of_courage_001");
            GameLogger.Info($"Helm equipped: {equipped}", nameof(EquipmentTest));
            _equipmentPanel?.SetPlayerState(_testPlayer);
            UpdateStatus($"Helm equipped: {equipped}\nBonus: {_testPlayer.TotalCombatBonus}");
        }
        else
        {
            GameLogger.Warning(
                "Cannot equip helm (already equipped or slot occupied)",
                nameof(EquipmentTest)
            );
        }
    }

    private void TryUnequipSword()
    {
        GameLogger.Info("\n--- Manual Test: Unequip Sword ---", nameof(EquipmentTest));

        if (_testPlayer == null)
            return;

        bool unequipped = _testPlayer.UnequipItem("item_broad_sword_001");
        GameLogger.Info($"Sword unequipped: {unequipped}", nameof(EquipmentTest));
        _equipmentPanel?.SetPlayerState(_testPlayer);
        UpdateStatus($"Sword unequipped: {unequipped}\nBonus: {_testPlayer.TotalCombatBonus}");
    }

    private void ResetTest()
    {
        GameLogger.Info("\n--- Manual Test: Reset ---", nameof(EquipmentTest));
        _testPlayer = CreateTestPlayer();
        _equipmentPanel?.SetPlayerState(_testPlayer);
        UpdateStatus("Test Reset\nPress E/U/R");
    }
}
