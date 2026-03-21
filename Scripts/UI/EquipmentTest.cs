using Godot;

/// <summary>
/// Test scene for EquipmentPanel functionality
/// </summary>
public partial class EquipmentTest : Node3D
{
    private EquipmentPanel _equipmentPanel;
    private GameStateManager _gameStateManager;
    private Label3D _statusLabel;

    public override void _Ready()
    {
        GD.Print("=== Starting EquipmentPanel Test ===");

        // Get references
        _equipmentPanel = GetNode<EquipmentPanel>("EquipmentPanel");
        _statusLabel = GetNode<Label3D>("StatusLabel");

        // Get GameStateManager
        _gameStateManager = GameStateManager.Instance;
        if (_gameStateManager == null)
        {
            GD.PrintErr("FAIL: GameStateManager not found (check autoloads)");
            return;
        }

        GD.Print("PASS: GameStateManager loaded");

        // Create test player
        _testPlayer = CreateTestPlayer();

        // Set up mock GameStateManager data
        SetupMockGameState(_testPlayer);

        // Test EquipmentPanel
        TestEquipmentPanelBasics(_testPlayer);

        GD.Print("=== EquipmentPanel Test Complete ===");
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

        // Add some equipment
        player.AddToHand("item_broad_sword_001");
        player.AddToHand("item_helm_of_courage_001");

        // Equip the sword
        if (player.CanEquipItem("item_broad_sword_001"))
        {
            player.EquipItem("item_broad_sword_001");
            GD.Print("Test player equipped Broad Sword");
        }

        return player;
    }

    private void SetupMockGameState(PlayerState testPlayer)
    {
        // Use the existing TestWithMockData to trigger events
        // This will test if EquipmentPanel responds to GameStateManager signals
        _gameStateManager.TestWithMockData();

        GD.Print("PASS: Mock game state sent via GameStateManager.TestWithMockData()");

        // Store test player for manual testing
        _testPlayer = testPlayer;
    }

    private void TestEquipmentPanelBasics(PlayerState testPlayer)
    {
        GD.Print("\n--- Testing EquipmentPanel Basics ---");

        // Test 1: Panel initialization
        if (_equipmentPanel == null)
        {
            GD.PrintErr("FAIL: EquipmentPanel not found in scene");
            return;
        }

        GD.Print("PASS: EquipmentPanel found");

        // Test 2: Player state integration
        _equipmentPanel.SetPlayerState(testPlayer);
        GD.Print("PASS: PlayerState set on EquipmentPanel");

        // Test 3: Check equipment display
        // Wait a frame for visuals to update
        Callable.From(() => CheckEquipmentDisplay(testPlayer)).CallDeferred();

        GD.Print("--- EquipmentPanel Basics Tests Complete ---");
    }

    private void CheckEquipmentDisplay(PlayerState testPlayer)
    {
        GD.Print("\n--- Checking Equipment Display ---");

        // Count worn equipment
        int wornCount = testPlayer.WornEquipmentIds.Count;
        GD.Print($"Worn equipment count: {wornCount} (should be 1)");

        if (wornCount == 1)
        {
            GD.Print("PASS: EquipmentPanel should show 1 worn item");
        }
        else
        {
            GD.PrintErr($"FAIL: Expected 1 worn item, got {wornCount}");
        }

        // Check total bonus calculation
        int totalBonus = testPlayer.TotalCombatBonus;
        GD.Print($"Total combat bonus: {totalBonus} (should be 8: Level 5 + Bonus 3)");

        if (totalBonus == 8)
        {
            GD.Print("PASS: Total bonus calculated correctly");
        }
        else
        {
            GD.PrintErr($"FAIL: Expected total bonus 8, got {totalBonus}");
        }

        // Test equipment validation
        bool canEquipHelm = testPlayer.CanEquipItem("item_helm_of_courage_001");
        GD.Print($"Can equip Helm of Courage? {canEquipHelm}");

        // Update status label
        if (_statusLabel != null)
        {
            _statusLabel.Text =
                $"Equipment Test Complete\nWorn: {wornCount} items\nBonus: +{totalBonus - testPlayer.Level}";
        }
    }

    // Input handling for manual testing
    private PlayerState _testPlayer;

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.E)
            {
                // Simulate equipping an item
                GD.Print("\n--- Manual Test: Try to equip Helm of Courage ---");

                if (_testPlayer != null)
                {
                    bool canEquip = _testPlayer.CanEquipItem("item_helm_of_courage_001");
                    GD.Print($"Can equip helm? {canEquip}");

                    if (canEquip)
                    {
                        bool equipped = _testPlayer.EquipItem("item_helm_of_courage_001");
                        GD.Print($"Equip successful? {equipped}");
                        GD.Print(
                            $"Player now has {_testPlayer.WornEquipmentIds.Count} worn items:"
                        );
                        foreach (var id in _testPlayer.WornEquipmentIds)
                        {
                            var item = _testPlayer.GetItemData(id);
                            GD.Print($"  - {id}: {item?.Name} (Slot: {item?.Slot})");
                        }

                        // Update display
                        GD.Print("Calling EquipmentPanel.SetPlayerState()...");
                        _equipmentPanel?.SetPlayerState(_testPlayer);
                        GD.Print("EquipmentPanel.SetPlayerState() called");

                        // Update status
                        if (_statusLabel != null)
                        {
                            _statusLabel.Text =
                                $"Helm equipped: {equipped}\nTotal Bonus: {_testPlayer.TotalCombatBonus}";
                        }
                    }
                }
            }
            else if (keyEvent.Keycode == Key.U)
            {
                // Simulate unequipping an item
                GD.Print("\n--- Manual Test: Try to unequip Broad Sword ---");

                if (_testPlayer != null)
                {
                    bool unequipped = _testPlayer.UnequipItem("item_broad_sword_001");
                    GD.Print($"Unequip successful? {unequipped}");
                    GD.Print($"Player now has {_testPlayer.WornEquipmentIds.Count} worn items:");
                    foreach (var id in _testPlayer.WornEquipmentIds)
                    {
                        var item = _testPlayer.GetItemData(id);
                        GD.Print($"  - {id}: {item?.Name} (Slot: {item?.Slot})");
                    }

                    // Update display
                    GD.Print("Calling EquipmentPanel.SetPlayerState()...");
                    _equipmentPanel?.SetPlayerState(_testPlayer);
                    GD.Print("EquipmentPanel.SetPlayerState() called");

                    // Update status
                    if (_statusLabel != null)
                    {
                        _statusLabel.Text =
                            $"Sword unequipped: {unequipped}\nTotal Bonus: {_testPlayer.TotalCombatBonus}";
                    }
                }
            }
            else if (keyEvent.Keycode == Key.R)
            {
                // Reset test
                GD.Print("\n--- Manual Test: Reset ---");
                _testPlayer = CreateTestPlayer();
                _equipmentPanel?.SetPlayerState(_testPlayer);

                if (_statusLabel != null)
                {
                    _statusLabel.Text = "Test Reset\nPress E/U/R";
                }
            }
        }
    }
}
