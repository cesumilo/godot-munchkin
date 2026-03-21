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

    private string GetHelmSlotInfo()
    {
        var cardFactory = GetNode<CardFactory>("/root/CardFactory");
        if (cardFactory == null)
            return "CardFactory not found";

        var helmData = cardFactory.GetCardById("item_helm_of_courage_001") as ItemCardData;
        if (helmData == null)
            return "Helm data not found";

        return $"Slot={helmData.Slot}, Bonus={helmData.Bonus}";
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

        // Try to equip both items
        if (player.CanEquipItem("item_broad_sword_001"))
        {
            bool equippedSword = player.EquipItem("item_broad_sword_001");
            GD.Print($"Test player equipped Broad Sword: {equippedSword}");
        }
        else
        {
            GD.PrintErr("FAIL: Cannot equip Broad Sword");
        }

        if (player.CanEquipItem("item_helm_of_courage_001"))
        {
            bool equippedHelm = player.EquipItem("item_helm_of_courage_001");
            GD.Print($"Test player equipped Helm of Courage: {equippedHelm}");
        }
        else
        {
            GD.PrintErr($"FAIL: Cannot equip Helm of Courage. Check slot: {GetHelmSlotInfo()}");
        }

        // Debug: Check what's in each list
        GD.Print($"\nDebug - Player equipment state:");
        GD.Print($"  Hand: {player.HandCardIds.Count} items");
        GD.Print($"  Worn: {player.WornEquipmentIds.Count} items");
        GD.Print($"  Carried: {player.CarriedEquipmentIds.Count} items");

        foreach (var id in player.WornEquipmentIds)
            GD.Print($"    Worn: {id}");
        foreach (var id in player.CarriedEquipmentIds)
            GD.Print($"    Carried: {id}");

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

        // Count worn equipment - we equipped both sword and helm
        int wornCount = testPlayer.WornEquipmentIds.Count;
        GD.Print($"Worn equipment count: {wornCount} (should be 2: sword + helm)");

        if (wornCount == 2)
        {
            GD.Print("PASS: EquipmentPanel should show 2 worn items");
        }
        else
        {
            GD.PrintErr($"FAIL: Expected 2 worn items, got {wornCount}");
        }

        // Check total bonus calculation: Level 5 + Sword 3 + Helm 1 = 9
        int totalBonus = testPlayer.TotalCombatBonus;
        GD.Print($"Total combat bonus: {totalBonus} (should be 9: Level 5 + Sword 3 + Helm 1)");

        if (totalBonus == 9)
        {
            GD.Print("PASS: Total bonus calculated correctly");
        }
        else
        {
            GD.PrintErr($"FAIL: Expected total bonus 9, got {totalBonus}");
        }

        // Test equipment validation - helm already equipped, so should return false
        bool canEquipHelm = testPlayer.CanEquipItem("item_helm_of_courage_001");
        GD.Print($"Can equip Helm of Courage? {canEquipHelm} (should be false - already equipped)");

        // Update status label
        if (_statusLabel != null)
        {
            _statusLabel.Text = $"Test Complete\nWorn: {wornCount}/2\nBonus: {totalBonus}/9";
        }

        // Additional test: Try to unequip and re-equip
        TestUnequipAndReequip(testPlayer);
    }

    private void TestUnequipAndReequip(PlayerState testPlayer)
    {
        GD.Print("\n--- Testing Unequip/Re-equip ---");

        // Try to unequip the helm
        bool unequipped = testPlayer.UnequipItem("item_helm_of_courage_001");
        GD.Print($"Unequipped helm: {unequipped}");

        if (unequipped)
        {
            GD.Print(
                $"After unequip - Worn: {testPlayer.WornEquipmentIds.Count}, Carried: {testPlayer.CarriedEquipmentIds.Count}"
            );

            // Now should be able to equip it again
            bool canEquipNow = testPlayer.CanEquipItem("item_helm_of_courage_001");
            GD.Print($"Can equip helm now? {canEquipNow} (should be true)");

            if (canEquipNow)
            {
                bool reequipped = testPlayer.EquipItem("item_helm_of_courage_001");
                GD.Print($"Re-equipped helm: {reequipped}");
            }
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
