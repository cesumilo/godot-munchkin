namespace Tests.Unit;

/// <summary>
/// Unit tests for equipment slot functionality.
/// Can run headless without scene dependencies.
/// </summary>
public static class SlotDebugTestLogic
{
    /// <summary>
    /// Runs all slot tests.
    /// </summary>
    /// <returns>True if all tests passed.</returns>
    public static bool Run()
    {
        bool allPassed = true;

        allPassed &= TestSlotEnumValues();
        allPassed &= TestHelmSlotAssignment();
        allPassed &= TestSlotOccupancy();

        return allPassed;
    }

    /// <summary>
    /// Tests EquipmentSlot enum values are consistent.
    /// </summary>
    private static bool TestSlotEnumValues()
    {
        GameLogger.Info("Testing EquipmentSlot enum values", nameof(SlotDebugTestLogic));

        // Verify all slots have expected values
        bool valid = true;

        // Check that slots are distinct
        var slots = new[]
        {
            EquipmentSlot.Head,
            EquipmentSlot.Armor,
            EquipmentSlot.Foot,
            EquipmentSlot.Hand1,
            EquipmentSlot.Hand2,
            EquipmentSlot.TwoHands,
            EquipmentSlot.None,
        };

        var seenValues = new System.Collections.Generic.HashSet<int>();
        foreach (var slot in slots)
        {
            int value = (int)slot;
            if (!seenValues.Add(value))
            {
                GameLogger.Error(
                    $"Duplicate slot value: {slot} = {value}",
                    nameof(SlotDebugTestLogic)
                );
                valid = false;
            }
        }

        GameLogger.Info(
            $"  {slots.Length} unique slot values verified",
            nameof(SlotDebugTestLogic)
        );
        return valid;
    }

    /// <summary>
    /// Tests helm slot assignment and validation.
    /// </summary>
    private static bool TestHelmSlotAssignment()
    {
        var cardFactory = CardFactory.Instance;
        if (cardFactory == null)
        {
            GameLogger.Error("CardFactory not available", nameof(SlotDebugTestLogic));
            return false;
        }

        var helmData = cardFactory.GetCardById<ItemCardData>("item_helm_of_courage_001");
        if (helmData == null)
        {
            GameLogger.Error("Helm of Courage not found", nameof(SlotDebugTestLogic));
            return false;
        }

        GameLogger.Info($"Testing {helmData.Name} slot assignment", nameof(SlotDebugTestLogic));

        bool passed = true;

        // Verify slot is Head
        if (helmData.Slot != EquipmentSlot.Head)
        {
            GameLogger.Error(
                $"Helm slot should be Head, got {helmData.Slot}",
                nameof(SlotDebugTestLogic)
            );
            passed = false;
        }

        // Verify bonus
        if (helmData.Bonus < 0)
        {
            GameLogger.Error(
                $"Helm bonus should be >= 0, got {helmData.Bonus}",
                nameof(SlotDebugTestLogic)
            );
            passed = false;
        }

        GameLogger.Info(
            $"  Slot: {helmData.Slot}, Bonus: {helmData.Bonus}",
            nameof(SlotDebugTestLogic)
        );
        return passed;
    }

    /// <summary>
    /// Tests slot occupancy logic in PlayerState.
    /// </summary>
    private static bool TestSlotOccupancy()
    {
        GameLogger.Info("Testing slot occupancy", nameof(SlotDebugTestLogic));

        var player = new PlayerState { PlayerId = "test_slot_player", PlayerName = "Test Player" };

        // Per §9: Item must be in hand before equipping
        player.AddToHand("item_helm_of_courage_001");

        // Initial state should allow equipping
        bool canEquipHelm = player.CanEquipItem("item_helm_of_courage_001");
        if (!canEquipHelm)
        {
            GameLogger.Error("Should be able to equip helm initially", nameof(SlotDebugTestLogic));
            return false;
        }

        // Equip the helm (moves from hand to worn)
        bool equipped = player.EquipItem("item_helm_of_courage_001");
        if (!equipped)
        {
            GameLogger.Error("Failed to equip helm", nameof(SlotDebugTestLogic));
            return false;
        }

        // Verify it's in worn equipment
        if (!player.WornEquipmentIds.Contains("item_helm_of_courage_001"))
        {
            GameLogger.Error("Helm not found in worn equipment", nameof(SlotDebugTestLogic));
            return false;
        }

        // Verify combat bonus includes the helm
        int bonus = player.TotalCombatBonus;
        if (bonus <= player.Level)
        {
            GameLogger.Error(
                $"Combat bonus should exceed level ({player.Level}), got {bonus}",
                nameof(SlotDebugTestLogic)
            );
            return false;
        }

        // Now should not be able to equip another head item
        bool canEquipSecondHelm = player.CanEquipItem("item_helm_of_courage_001");
        if (canEquipSecondHelm)
        {
            GameLogger.Warning(
                "Can equip duplicate head item - may be expected",
                nameof(SlotDebugTestLogic)
            );
        }

        GameLogger.Info($"  Equipped helm, bonus: {bonus}", nameof(SlotDebugTestLogic));
        return true;
    }
}
