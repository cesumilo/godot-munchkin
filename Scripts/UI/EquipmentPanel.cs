using System.Collections.Generic;
using Godot;

/// <summary>
/// Displays a player's equipment in 3D space using Card3D visuals.
/// Handles both worn equipment (in slots) and carried equipment.
/// </summary>
/// <remarks>
/// Per §9: Shows equipped items in appropriate slots (Head, Armor, Feet, Hands).
/// Carried items displayed separately. Per §9.4: Worn items show active bonuses.
/// </remarks>
public partial class EquipmentPanel : Node3D
{
    // References to PlayerState
    private PlayerState _playerState;
    private GameStateManager _gameStateManager;

    // Slot containers for 3D card placement
    /// <summary>
    /// Container node for head slot equipment.
    /// </summary>
    [Export]
    private Node3D _headSlotContainer;

    /// <summary>
    /// Container node for armor slot equipment.
    /// </summary>
    [Export]
    private Node3D _armorSlotContainer;

    /// <summary>
    /// Container node for feet slot equipment.
    /// </summary>
    [Export]
    private Node3D _feetSlotContainer;

    /// <summary>
    /// Container node for first hand slot equipment.
    /// </summary>
    [Export]
    private Node3D _hand1SlotContainer;

    /// <summary>
    /// Container node for second hand slot equipment.
    /// </summary>
    [Export]
    private Node3D _hand2SlotContainer;

    /// <summary>
    /// Container node for carried (unequipped) equipment.
    /// </summary>
    [Export]
    private Node3D _carriedEquipmentContainer;

    // CardVisual scene for instancing (uses existing CardVisual.cs)
    /// <summary>
    /// PackedScene for CardVisual instances.
    /// </summary>
    [Export]
    private PackedScene _cardVisualScene;

    // Track instantiated card visuals
    private Dictionary<string, Node3D> _cardVisuals = new();

    // References to slot markers for visual feedback
    private Dictionary<EquipmentSlot, Node3D> _slotContainers = new();

    // UI labels
    /// <summary>
    /// Label displaying player name.
    /// </summary>
    [Export]
    private Label3D _playerNameLabel;

    /// <summary>
    /// Label displaying player level.
    /// </summary>
    [Export]
    private Label3D _playerLevelLabel;

    /// <summary>
    /// Label displaying total combat bonus.
    /// </summary>
    [Export]
    private Label3D _totalBonusLabel;

    /// <summary>
    /// Label displaying player race(s).
    /// </summary>
    [Export]
    private Label3D _raceLabel;

    /// <summary>
    /// Label displaying player class(es).
    /// </summary>
    [Export]
    private Label3D _classLabel;

    /// <summary>
    /// Initializes slot containers and finds GameStateManager.
    /// </summary>
    public override void _Ready()
    {
        InitializeSlotContainers();
        FindGameStateManager();
    }

    /// <summary>
    /// Maps equipment slots to their container nodes.
    /// </summary>
    private void InitializeSlotContainers()
    {
        _slotContainers[EquipmentSlot.Head] = _headSlotContainer;
        _slotContainers[EquipmentSlot.Armor] = _armorSlotContainer;
        _slotContainers[EquipmentSlot.Foot] = _feetSlotContainer;
        _slotContainers[EquipmentSlot.Hand1] = _hand1SlotContainer;
        _slotContainers[EquipmentSlot.Hand2] = _hand2SlotContainer;
    }

    /// <summary>
    /// Finds and subscribes to GameStateManager for player updates.
    /// </summary>
    private void FindGameStateManager()
    {
        _gameStateManager = GameStateManager.Instance;
        if (_gameStateManager == null)
        {
            GameLogger.Error("GameStateManager not found (check autoloads)", this);
            return;
        }

        _gameStateManager.OnLocalPlayerUpdated += UpdatePlayerState;
        _gameStateManager.OnGameStateUpdated += OnGameStateUpdated;
        UpdatePlayerState(_gameStateManager.LocalPlayer);
    }

    /// <summary>
    /// Handles game state update events.
    /// </summary>
    private void OnGameStateUpdated()
    {
        UpdatePlayerState(_gameStateManager.LocalPlayer);
    }

    /// <summary>
    /// Sets the player state and triggers display update.
    /// </summary>
    /// <param name="playerState">The player state to display.</param>
    public void SetPlayerState(PlayerState playerState)
    {
        bool isSameReference = _playerState == playerState;
        GameLogger.Debug($"SetPlayerState called. Same reference? {isSameReference}", this);

        _playerState = playerState;
        UpdateDisplay();
    }

    /// <summary>
    /// Updates player state and refreshes display.
    /// </summary>
    /// <param name="playerState">The updated player state.</param>
    private void UpdatePlayerState(PlayerState playerState)
    {
        GameLogger.Debug("Called UpdatePlayerState!", this);
        SetPlayerState(playerState);
    }

    /// <summary>
    /// Updates all display elements for current player state.
    /// </summary>
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

    /// <summary>
    /// Updates player information labels (name, level, race, class, bonus).
    /// </summary>
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

    /// <summary>
    /// Updates equipment slot displays for worn and carried items.
    /// </summary>
    private void UpdateEquipmentSlots()
    {
        GameLogger.Debug($"Updating equipment slots for player: {_playerState?.PlayerName}", this);

        ClearEquipmentVisuals();

        var wornEquipment = _playerState.GetWornEquipment();
        GameLogger.Debug($"Player has {wornEquipment.Count} worn items", this);

        foreach (var item in wornEquipment)
        {
            GameLogger.Debug($"Item: {item.Name}, Slot: {item.Slot}, Bonus: {item.Bonus}", this);

            if (item.Slot != EquipmentSlot.None)
            {
                Vector3 slotPos = GetSlotPosition(item.Slot);
                GameLogger.Debug($"Creating visual at slot position: {slotPos}", this);
                CreateCardVisual(item, slotPos);
            }
            else
            {
                Vector3 carriedPos =
                    _carriedEquipmentContainer?.GlobalTransform.Origin ?? Vector3.Zero;
                GameLogger.Debug($"Creating visual in carried area: {carriedPos}", this);
                CreateCardVisual(item, carriedPos);
            }
        }

        var carriedEquipment = _playerState.GetCarriedEquipment();
        foreach (var item in carriedEquipment)
        {
            Vector3 position;
            if (_carriedEquipmentContainer != null)
            {
                int index = _cardVisuals.Count % 6;
                float x = (index % 3) * 1.2f;
                float z = (index / 3) * 1.5f;
                position = _carriedEquipmentContainer.GlobalTransform.Origin + new Vector3(x, 0, z);
            }
            else
            {
                position = Vector3.Zero;
            }

            CreateCardVisual(item, position, true, false);
        }
    }

    /// <summary>
    /// Gets the 3D position for an equipment slot.
    /// </summary>
    /// <param name="slot">The equipment slot.</param>
    /// <returns>The world position for the slot.</returns>
    private Vector3 GetSlotPosition(EquipmentSlot slot)
    {
        if (_slotContainers.TryGetValue(slot, out var container) && container != null)
        {
            return container.GlobalTransform.Origin;
        }

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
    /// Creates a 3D card visual for an item.
    /// </summary>
    /// <param name="itemData">The item card data.</param>
    /// <param name="position">World position for the card.</param>
    /// <param name="faceUp">True if card should be face up.</param>
    /// <param name="isWorn">True if item is currently equipped.</param>
    private void CreateCardVisual(
        ItemCardData itemData,
        Vector3 position,
        bool faceUp = true,
        bool isWorn = true
    )
    {
        Node3D cardVisualInstance;

        if (_cardVisualScene != null)
        {
            cardVisualInstance = _cardVisualScene.Instantiate<Node3D>();
            var cardVisualComponent = cardVisualInstance.GetNodeOrNull<CardVisual>(".");
            if (cardVisualComponent != null)
            {
                cardVisualComponent.CardData = itemData;
            }
            else
            {
                CreateSimpleCardVisual(cardVisualInstance, itemData, isWorn);
            }
        }
        else
        {
            cardVisualInstance = new Node3D();
            CreateSimpleCardVisual(cardVisualInstance, itemData, isWorn);
        }

        AddChild(cardVisualInstance);
        cardVisualInstance.GlobalPosition = position;

        if (!faceUp)
        {
            cardVisualInstance.RotationDegrees = new Vector3(0, 180, 0);
        }

        _cardVisuals[itemData.Id] = cardVisualInstance;

        GameLogger.Debug($"Created card visual for: {itemData.Name} at {position}", this);

        AddDragDropHandler(cardVisualInstance, itemData, isWorn);
    }

    /// <summary>
    /// Creates a simple mesh-based card visual as fallback.
    /// </summary>
    /// <param name="parent">Parent node for the card visual.</param>
    /// <param name="itemData">The item data.</param>
    /// <param name="isWorn">Whether item is worn.</param>
    private void CreateSimpleCardVisual(Node3D parent, ItemCardData itemData, bool isWorn = true)
    {
        var meshInstance = new MeshInstance3D();
        var boxMesh = new BoxMesh { Size = new Vector3(0.7f, 1f, 0.01f) };
        meshInstance.Mesh = boxMesh;

        var material = new StandardMaterial3D();
        Color baseColor = itemData.Bonus switch
        {
            >= 5 => new Color(1f, 0.5f, 0f),
            >= 3 => new Color(0f, 1f, 0f),
            _ => new Color(0.5f, 0.5f, 1f),
        };

        if (isWorn)
        {
            material.AlbedoColor = baseColor;
            material.Emission = baseColor * 0.3f;
            material.EmissionEnabled = true;
        }
        else
        {
            material.AlbedoColor = baseColor * 0.6f;
            material.EmissionEnabled = false;
        }

        meshInstance.MaterialOverride = material;
        parent.AddChild(meshInstance);

        var label = new Label3D
        {
            Text = $"{itemData.Name}\n+{itemData.Bonus}\n{(isWorn ? "EQUIPPED" : "CARRIED")}",
            FontSize = 16,
            Position = new Vector3(0, 0.3f, 0.006f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        parent.AddChild(label);
    }

    /// <summary>
    /// Creates a tooltip for an item.
    /// </summary>
    /// <param name="itemData">The item data.</param>
    /// <returns>Tooltip node.</returns>
    private Node3D CreateTooltipForItem(ItemCardData itemData)
    {
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

    /// <summary>
    /// Clears all equipment card visuals.
    /// </summary>
    private void ClearEquipmentVisuals()
    {
        foreach (var cardVisual in _cardVisuals.Values)
        {
            cardVisual.QueueFree();
        }
        _cardVisuals.Clear();
    }

    /// <summary>
    /// Clears all display elements.
    /// </summary>
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

    /// <summary>
    /// Attempts to equip an item.
    /// </summary>
    /// <param name="itemId">The item ID to equip.</param>
    /// <returns>True if can equip; false otherwise.</returns>
    public bool TryEquipItem(string itemId)
    {
        if (_playerState == null)
            return false;

        if (_playerState.CanEquipItem(itemId))
        {
            GameLogger.Debug($"Requesting to equip: {itemId}", this);
            return true;
        }

        GameLogger.Debug($"Cannot equip item: {itemId}", this);
        return false;
    }

    /// <summary>
    /// Attempts to unequip an item.
    /// </summary>
    /// <param name="itemId">The item ID to unequip.</param>
    /// <returns>True if item was unequipped; false otherwise.</returns>
    public bool TryUnequipItem(string itemId)
    {
        if (_playerState == null)
            return false;

        if (_playerState.WornEquipmentIds.Contains(itemId))
        {
            GameLogger.Debug($"Requesting to unequip: {itemId}", this);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Unsubscribes from events on exit.
    /// </summary>
    public override void _ExitTree()
    {
        if (_gameStateManager != null)
        {
            _gameStateManager.OnLocalPlayerUpdated -= UpdatePlayerState;
            _gameStateManager.OnGameStateUpdated -= OnGameStateUpdated;
        }
    }

    /// <summary>
    /// Adds drag-drop handler to a card visual.
    /// </summary>
    /// <param name="cardVisual">The card visual node.</param>
    /// <param name="itemData">The item data.</param>
    /// <param name="isWorn">Whether item is worn.</param>
    private void AddDragDropHandler(Node3D cardVisual, ItemCardData itemData, bool isWorn)
    {
        var dragHandler = new DragDropHandler();
        cardVisual.AddChild(dragHandler);
        dragHandler.DragStarted += OnDragStarted;
        dragHandler.DragEnded += OnDragEnded;
        dragHandler.DroppedOnSlot += OnDroppedOnSlot;
        dragHandler.SetCardData(itemData.Id, itemData);
        GameLogger.Debug($"Added drag handler to: {itemData.Name}", this);
    }

    /// <summary>
    /// Handles drag started event.
    /// </summary>
    /// <param name="draggable">The dragged node.</param>
    private void OnDragStarted(Node3D draggable)
    {
        GameLogger.Debug($"Drag started: {draggable.Name}", this);
    }

    /// <summary>
    /// Handles drag ended event.
    /// </summary>
    /// <param name="draggable">The dragged node.</param>
    /// <param name="position">Final position.</param>
    private void OnDragEnded(Node3D draggable, Vector3 position)
    {
        GameLogger.Debug($"Drag ended at: {position}", this);
    }

    /// <summary>
    /// Handles drop on slot event.
    /// </summary>
    /// <param name="draggable">The dragged node.</param>
    /// <param name="slotInt">The slot value as int.</param>
    private void OnDroppedOnSlot(Node3D draggable, int slotInt)
    {
        EquipmentSlot slot = (EquipmentSlot)slotInt;
        GameLogger.Debug($"Dropped on slot: {slot}", this);

        var dragHandler = draggable.GetNodeOrNull<DragDropHandler>(".");
        if (dragHandler == null)
        {
            GameLogger.Error("No DragDropHandler found on draggable", this);
            return;
        }

        var cardVisual = draggable.GetParent() as CardVisual;
        if (cardVisual == null || cardVisual.CardData == null)
        {
            GameLogger.Error("Could not get CardVisual or CardData from draggable", this);
            return;
        }

        var cardData = cardVisual.CardData;
        GameLogger.Debug($"Found card data: {cardData.Name} (ID: {cardData.Id})", this);

        if (_playerState == null)
        {
            GameLogger.Error("No PlayerState set on EquipmentPanel", this);
            return;
        }

        bool isEquipped = _playerState.WornEquipmentIds.Contains(cardData.Id);
        bool isCarried = _playerState.CarriedEquipmentIds.Contains(cardData.Id);
        GameLogger.Debug($"Item state: Equipped={isEquipped}, Carried={isCarried}", this);

        if (slot == EquipmentSlot.None)
        {
            if (isEquipped)
            {
                GameLogger.Debug($"Attempting to unequip {cardData.Name}...", this);
                bool success = _playerState.UnequipItem(cardData.Id);
                GameLogger.Debug($"Unequip result: {success}", this);
            }
        }
        else if (isCarried)
        {
            GameLogger.Debug($"Attempting to equip {cardData.Name}...", this);
            bool success = _playerState.EquipItem(cardData.Id);
            GameLogger.Debug($"Equip result: {success}", this);
        }

        UpdatePlayerInfo();
        GameLogger.Debug($"Bonus updated: {_playerState.TotalCombatBonus}", this);
    }
}
