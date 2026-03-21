using System.Collections.Generic;
using Godot;

/// <summary>
/// EquipmentPanel - Displays player's worn and carried equipment
/// Uses Card3D plugin for 3D card visuals
/// </summary>
public partial class EquipmentPanel : Node3D
{
    // References to PlayerState
    private PlayerState _playerState;
    private GameStateManager _gameStateManager;

    // Slot containers for 3D card placement
    [Export]
    private Node3D _headSlotContainer;

    [Export]
    private Node3D _armorSlotContainer;

    [Export]
    private Node3D _feetSlotContainer;

    [Export]
    private Node3D _hand1SlotContainer;

    [Export]
    private Node3D _hand2SlotContainer;

    [Export]
    private Node3D _carriedEquipmentContainer;

    // CardVisual scene for instancing (uses existing CardVisual.cs)
    [Export]
    private PackedScene _cardVisualScene;

    // Track instantiated card visuals
    private Dictionary<string, Node3D> _cardVisuals = new();

    // References to slot markers for visual feedback
    private Dictionary<EquipmentSlot, Node3D> _slotContainers = new();

    // UI labels
    [Export]
    private Label3D _playerNameLabel;

    [Export]
    private Label3D _playerLevelLabel;

    [Export]
    private Label3D _totalBonusLabel;

    [Export]
    private Label3D _raceLabel;

    [Export]
    private Label3D _classLabel;

    public override void _Ready()
    {
        InitializeSlotContainers();
        FindGameStateManager();
    }

    private void InitializeSlotContainers()
    {
        // Map slot types to their container nodes
        _slotContainers[EquipmentSlot.Head] = _headSlotContainer;
        _slotContainers[EquipmentSlot.Armor] = _armorSlotContainer;
        _slotContainers[EquipmentSlot.Foot] = _feetSlotContainer;
        _slotContainers[EquipmentSlot.Hand1] = _hand1SlotContainer;
        _slotContainers[EquipmentSlot.Hand2] = _hand2SlotContainer;
    }

    private void FindGameStateManager()
    {
        _gameStateManager = GameStateManager.Instance;
        if (_gameStateManager == null)
        {
            GD.PrintErr("[EquipmentPanel] GameStateManager not found (check autoloads)");
            return;
        }

        // Subscribe to player state changes
        _gameStateManager.OnLocalPlayerUpdated += UpdatePlayerState;
        _gameStateManager.OnGameStateUpdated += OnGameStateUpdated;

        // Initial update
        UpdatePlayerState(_gameStateManager.LocalPlayer);
    }

    private void OnGameStateUpdated()
    {
        // Refresh if player state might have changed
        UpdatePlayerState(_gameStateManager.LocalPlayer);
    }

    public void SetPlayerState(PlayerState playerState)
    {
        // Always update the display even if it's the same player object
        // because the player's state (equipment, level, etc.) may have changed
        bool isSameReference = _playerState == playerState;
        GD.Print($"[EquipmentPanel] SetPlayerState called. Same reference? {isSameReference}");

        _playerState = playerState;
        UpdateDisplay();
    }

    private void UpdatePlayerState(PlayerState playerState)
    {
        GD.Print("Called UpdatePlayerState !");
        SetPlayerState(playerState);
    }

    private void UpdateDisplay()
    {
        if (_playerState == null)
        {
            ClearDisplay();
            return;
        }

        UpdatePlayerInfo();
        UpdateEquipmentSlots();
    }

    private void UpdatePlayerInfo()
    {
        if (_playerNameLabel != null)
            _playerNameLabel.Text = _playerState.PlayerName;

        if (_playerLevelLabel != null)
            _playerLevelLabel.Text = $"Level: {_playerState.Level}";

        if (_totalBonusLabel != null)
            _totalBonusLabel.Text = $"Total Bonus: {_playerState.TotalCombatBonus}";

        if (_raceLabel != null)
        {
            string raceText = _playerState.PrimaryRace.ToString();
            if (_playerState.HasMixedBlood && _playerState.SecondaryRace != RaceType.None)
                raceText += $" + {_playerState.SecondaryRace}";
            _raceLabel.Text = $"Race: {raceText}";
        }

        if (_classLabel != null)
        {
            string classText = _playerState.PrimaryClass.ToString();
            if (_playerState.HasSuperMunchkin && _playerState.SecondaryClass != ClassType.None)
                classText += $" + {_playerState.SecondaryClass}";
            _classLabel.Text = $"Class: {classText}";
        }
    }

    private void UpdateEquipmentSlots()
    {
        GD.Print(
            $"[EquipmentPanel] Updating equipment slots for player: {_playerState?.PlayerName}"
        );

        ClearEquipmentVisuals();

        // Display worn equipment in appropriate slots
        var wornEquipment = _playerState.GetWornEquipment();
        GD.Print($"[EquipmentPanel] Player has {wornEquipment.Count} worn items");

        foreach (var item in wornEquipment)
        {
            GD.Print($"[EquipmentPanel] Item: {item.Name}, Slot: {item.Slot}, Bonus: {item.Bonus}");

            if (item.Slot != EquipmentSlot.None)
            {
                Vector3 slotPos = GetSlotPosition(item.Slot);
                GD.Print($"[EquipmentPanel] Creating visual at slot position: {slotPos}");
                CreateCardVisual(item, slotPos);
            }
            else
            {
                // No-slot items (amulets, rings) go to carried container
                Vector3 carriedPos =
                    _carriedEquipmentContainer?.GlobalTransform.Origin ?? Vector3.Zero;
                GD.Print($"[EquipmentPanel] Creating visual in carried area: {carriedPos}");
                CreateCardVisual(item, carriedPos);
            }
        }

        // Display carried equipment
        var carriedEquipment = _playerState.GetCarriedEquipment();
        foreach (var item in carriedEquipment)
        {
            Vector3 position;
            if (_carriedEquipmentContainer != null)
            {
                // Position in grid within carried container
                int index = _cardVisuals.Count % 6; // Max 6 items visible
                float x = (index % 3) * 1.2f;
                float z = (index / 3) * 1.5f;
                position = _carriedEquipmentContainer.GlobalTransform.Origin + new Vector3(x, 0, z);
            }
            else
            {
                position = Vector3.Zero;
            }

            CreateCardVisual(item, position, true); // Carried items are face-up (visible)
        }
    }

    private Vector3 GetSlotPosition(EquipmentSlot slot)
    {
        if (_slotContainers.TryGetValue(slot, out var container) && container != null)
        {
            return container.GlobalTransform.Origin;
        }

        // Fallback positions based on slot type
        return slot switch
        {
            EquipmentSlot.Head => new Vector3(-1.5f, 1.5f, 0),
            EquipmentSlot.Armor => new Vector3(0, 1.5f, 0),
            EquipmentSlot.Foot => new Vector3(1.5f, 1.5f, 0),
            EquipmentSlot.Hand1 => new Vector3(-1.5f, 0, 0),
            EquipmentSlot.Hand2 => new Vector3(1.5f, 0, 0),
            EquipmentSlot.TwoHands => new Vector3(0, 0, 0),
            _ => Vector3.Zero,
        };
    }

    /// <summary>
    /// Create a card visual for an item
    /// </summary>
    /// <param name="itemData">The item card data</param>
    /// <param name="position">3D position to place the card</param>
    /// <param name="faceUp">True = face-up (visible to all players), False = face-down (only for cards in hand)</param>
    private void CreateCardVisual(ItemCardData itemData, Vector3 position, bool faceUp = true)
    {
        Node3D cardVisualInstance;

        if (_cardVisualScene != null)
        {
            // Use CardVisual scene if assigned
            cardVisualInstance = _cardVisualScene.Instantiate<Node3D>();

            // Try to set CardData if it's a CardVisual component
            var cardVisualComponent = cardVisualInstance.GetNodeOrNull<CardVisual>(".");
            if (cardVisualComponent != null)
            {
                cardVisualComponent.CardData = itemData;
            }
            else
            {
                // Fallback: create simple visual
                CreateSimpleCardVisual(cardVisualInstance, itemData);
            }
        }
        else
        {
            // Create simple visual directly
            cardVisualInstance = new Node3D();
            CreateSimpleCardVisual(cardVisualInstance, itemData);
        }

        AddChild(cardVisualInstance);
        cardVisualInstance.GlobalPosition = position;

        // Configure card appearance
        if (!faceUp)
        {
            // Simple rotation for face-down
            cardVisualInstance.RotationDegrees = new Vector3(0, 180, 0);
        }

        // Store reference
        _cardVisuals[itemData.Id] = cardVisualInstance;

        GD.Print($"[EquipmentPanel] Created card visual for: {itemData.Name} at {position}");
    }

    private void CreateSimpleCardVisual(Node3D parent, ItemCardData itemData)
    {
        // Create a simple mesh for the card
        var meshInstance = new MeshInstance3D();
        var boxMesh = new BoxMesh { Size = new Vector3(0.7f, 1f, 0.01f) };
        meshInstance.Mesh = boxMesh;

        // Color code by item type/bonus
        var material = new StandardMaterial3D();
        material.AlbedoColor = itemData.Bonus switch
        {
            >= 5 => new Color(1f, 0.5f, 0f), // Orange for high bonus
            >= 3 => new Color(0f, 1f, 0f), // Green for medium bonus
            _ => new Color(0.5f, 0.5f, 1f), // Blue for low bonus
        };
        meshInstance.MaterialOverride = material;

        parent.AddChild(meshInstance);

        // Add label with item name and bonus
        var label = new Label3D
        {
            Text = $"{itemData.Name}\n+{itemData.Bonus}",
            FontSize = 16,
            Position = new Vector3(0, 0.3f, 0.006f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        parent.AddChild(label);
    }

    private Node3D CreateTooltipForItem(ItemCardData itemData)
    {
        // Create a simple tooltip with item info
        var tooltip = new Node3D();
        var label = new Label3D
        {
            Text = $"{itemData.Name}\nBonus: +{itemData.Bonus}\nSlot: {itemData.Slot}",
            FontSize = 12,
            Position = new Vector3(0, 0.5f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        tooltip.AddChild(label);
        return tooltip;
    }

    private void ClearEquipmentVisuals()
    {
        foreach (var cardVisual in _cardVisuals.Values)
        {
            cardVisual.QueueFree();
        }
        _cardVisuals.Clear();
    }

    private void ClearDisplay()
    {
        ClearEquipmentVisuals();

        if (_playerNameLabel != null)
            _playerNameLabel.Text = "No Player";
        if (_playerLevelLabel != null)
            _playerLevelLabel.Text = "Level: --";
        if (_totalBonusLabel != null)
            _totalBonusLabel.Text = "Total Bonus: --";
        if (_raceLabel != null)
            _raceLabel.Text = "Race: --";
        if (_classLabel != null)
            _classLabel.Text = "Class: --";
    }

    // Public methods for interaction
    public bool TryEquipItem(string itemId)
    {
        if (_playerState == null)
            return false;

        if (_playerState.CanEquipItem(itemId))
        {
            // TODO: Send equip request to server via GameStateManager
            GD.Print($"[EquipmentPanel] Requesting to equip: {itemId}");
            return true;
        }

        GD.Print($"[EquipmentPanel] Cannot equip item: {itemId}");
        return false;
    }

    public bool TryUnequipItem(string itemId)
    {
        if (_playerState == null)
            return false;

        if (_playerState.WornEquipmentIds.Contains(itemId))
        {
            // TODO: Send unequip request to server
            GD.Print($"[EquipmentPanel] Requesting to unequip: {itemId}");
            return true;
        }

        return false;
    }

    // Cleanup
    public override void _ExitTree()
    {
        if (_gameStateManager != null)
        {
            _gameStateManager.OnLocalPlayerUpdated -= UpdatePlayerState;
            _gameStateManager.OnGameStateUpdated -= OnGameStateUpdated;
        }
    }
}
