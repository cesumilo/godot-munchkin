using System;
using Godot;

public partial class SlotDebugTest : Node
{
    public override void _Ready()
    {
        TestSlotValues();
        TestHelmSlot();
    }

    private void TestSlotValues()
    {
        GD.Print("=== Testing EquipmentSlot enum values ===");
        GD.Print($"Head: {(int)EquipmentSlot.Head}");
        GD.Print($"Armor: {(int)EquipmentSlot.Armor}");
        GD.Print($"Foot: {(int)EquipmentSlot.Foot}");
        GD.Print($"Hand1: {(int)EquipmentSlot.Hand1}");
        GD.Print($"Hand2: {(int)EquipmentSlot.Hand2}");
        GD.Print($"TwoHands: {(int)EquipmentSlot.TwoHands}");
        GD.Print($"None: {(int)EquipmentSlot.None}");
        GD.Print("");
    }

    private void TestHelmSlot()
    {
        GD.Print("=== Testing Helm of Courage slot ===");

        var cardFactory = GetNode<CardFactory>("/root/CardFactory");
        if (cardFactory == null)
        {
            GD.PrintErr("CardFactory not found!");
            return;
        }

        var helmData = cardFactory.GetCardById("item_helm_of_courage_001") as ItemCardData;
        if (helmData == null)
        {
            GD.PrintErr("Helm of Courage not found!");
            return;
        }

        GD.Print($"Helm Name: {helmData.Name}");
        GD.Print($"Helm Slot enum value: {helmData.Slot}");
        GD.Print($"Helm Slot int value: {(int)helmData.Slot}");
        GD.Print($"Is Slot == Head? {helmData.Slot == EquipmentSlot.Head}");
        GD.Print($"Is Slot == None? {helmData.Slot == EquipmentSlot.None}");
        GD.Print($"Slot.ToString(): {helmData.Slot}");

        // Test PlayerState method
        var playerState = new PlayerState();
        playerState.PlayerId = "test_player";
        playerState.PlayerName = "Test Player";

        GD.Print("");
        GD.Print("Testing PlayerState.CanEquipItem for helm:");
        GD.Print($"CanEquipItem: {playerState.CanEquipItem("item_helm_of_courage_001")}");

        // Equip it and check
        playerState.WornEquipmentIds.Add("item_helm_of_courage_001");
        GD.Print($"After equipping helm, TotalCombatBonus: {playerState.TotalCombatBonus}");
    }
}
