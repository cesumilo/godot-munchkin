using Godot;

namespace Tests.Smoke;

/// <summary>
/// Smoke test for drag-and-drop mechanics with visual feedback.
/// Requires scene: Scenes/Tests/Smoke/DragDropTest.tscn
/// Manual verification: Drag card to colored drop zones.
/// </summary>
public partial class DragDropTest : Node3D
{
    [Export]
    private PackedScene _cardVisualScene;

    [Export]
    private ItemCardData _cardData;

    private DragDropHandler _draggedCard;
    private Label3D _statusLabel;
    private int _successfulDrops = 0;
    private int _failedDrops = 0;

    // Zone colors for visual feedback
    private static readonly Color ColorValid = new Color(0.2f, 0.8f, 0.2f, 0.5f); // Green
    private static readonly Color ColorInvalid = new Color(0.9f, 0.2f, 0.2f, 0.5f); // Red
    private static readonly Color ColorNeutral = new Color(0.5f, 0.5f, 0.5f, 0.3f); // Gray

    public override void _Ready()
    {
        GameLogger.Info("=== DragDropTest Started ===", nameof(DragDropTest));
        GameLogger.Info(
            "Per AGENTS.md: X=left/right, Y=up/down, Z=depth (forward/backward)",
            nameof(DragDropTest)
        );

        // Create status label - position in front of camera
        _statusLabel = new Label3D
        {
            Text = "Drag card to colored zones\nPress SPACE for status",
            FontSize = 20,
            Position = new Vector3(0, 3f, 4f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        AddChild(_statusLabel);

        // Log camera info
        var camera = GetViewport().GetCamera3D();
        if (camera != null)
        {
            GameLogger.Info($"Camera position: {camera.GlobalPosition}", nameof(DragDropTest));
        }

        // Create test environment
        CreateTestCard();
        CreateDropZones();

        // Log what was created
        GameLogger.Info($"Scene has {GetChildCount()} children", nameof(DragDropTest));

        GameLogger.Info(
            "DragDropTest ready - drag the card to colored zones",
            nameof(DragDropTest)
        );
    }

    /// <summary>
    /// Creates a test card with drag handler.
    /// </summary>
    /// <remarks>
    /// Per AGENTS.md: Camera at Z=5 faces -Z (toward Z=0).
    /// Card placed at Z=2 (in front of camera, toward player).
    /// </remarks>
    private void CreateTestCard()
    {
        if (_cardVisualScene != null)
        {
            var testCard = _cardVisualScene.Instantiate<Node3D>();
            var cardVisualComponent = testCard.GetNodeOrNull<CardVisual>(".");

            if (cardVisualComponent != null)
            {
                cardVisualComponent.CardData = _cardData;

                AddChild(testCard);
                // Card at Z=2: closer to camera (Z=5) than drop zones (Z=-1)
                testCard.GlobalPosition = new Vector3(0, 1f, 1f);

                // Add drag handler
                var dragHandler = new DragDropHandler();
                testCard.AddChild(dragHandler);
                dragHandler.DragStarted += OnDragStarted;
                dragHandler.DragEnded += OnDragEnded;
                dragHandler.DroppedOnSlot += OnDroppedOnSlot;

                GameLogger.Info("Test card created with drag handler", nameof(DragDropTest));
            }
            else
            {
                // Create a simple test card
                var simpleCard = CreateSimpleCard();
                AddChild(simpleCard);
                // Card at Z=1: in front of camera
                simpleCard.GlobalPosition = new Vector3(0, 1f, 1f);
            }
        }
        else
        {
            // Create a simple test card
            var simpleCard = CreateSimpleCard();
            AddChild(simpleCard);
            // Card at Z=1: in front of camera
            simpleCard.GlobalPosition = new Vector3(0, 1f, 1f);
        }
    }

    /// <summary>
    /// Creates a simple card mesh with collision for testing.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: CollisionShape3D is required for raycast-based drag detection.
    /// Without it, the DragDropHandler cannot detect mouse clicks on the card.
    /// Per AGENTS.md: Drop zone Area3D nodes have thin CollisionShape3D (Z height 0.1 units).
    /// </remarks>
    private Node3D CreateSimpleCard()
    {
        var card = new Node3D { Name = "TestCard" };

        // Create card mesh
        var mesh = new MeshInstance3D();
        var box = new BoxMesh { Size = new Vector3(0.7f, 1f, 0.01f) };
        mesh.Mesh = box;
        mesh.Name = "CardMesh";

        var material = new StandardMaterial3D { AlbedoColor = new Color(0.9f, 0.9f, 0.9f) };
        mesh.MaterialOverride = material;

        card.AddChild(mesh);

        // CRITICAL: Add collision shape so raycasts can detect the card
        // Without this, DragDropHandler.IsMouseOverObject() will never return true
        var collision = new CollisionShape3D();
        var shape = new BoxShape3D { Size = new Vector3(0.7f, 1f, 0.01f) };
        collision.Shape = shape;
        collision.Name = "CardCollider";

        // StaticBody3D required for collision detection
        var body = new StaticBody3D();
        body.AddChild(collision);
        card.AddChild(body);

        // Add label
        var label = new Label3D
        {
            Text = "Drag Me",
            FontSize = 32,
            Position = new Vector3(0, 0.3f, 0.006f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        mesh.AddChild(label);

        // Add drag handler
        var dragHandler = new DragDropHandler();
        card.AddChild(dragHandler);
        dragHandler.DragStarted += OnDragStarted;
        dragHandler.DragEnded += OnDragEnded;
        dragHandler.DroppedOnSlot += OnDroppedOnSlot;

        GameLogger.Info("Created draggable card with collision shape", nameof(DragDropTest));
        return card;
    }

    /// <summary>
    /// Creates colored drop zones for visual feedback.
    /// </summary>
    /// <remarks>
    /// Per AGENTS.md: X=left/right, Y=up/down, Z=depth (-Z=forward, +Z=backward).
    /// Camera at Z=5 faces -Z. Card at Z=2 (closer to camera).
    /// Drop zones placed BEHIND card at Z=-1 (further into screen, away from camera).
    /// </remarks>
    private void CreateDropZones()
    {
        // Camera at Z=5, card at Z=2
        // Drop zones at Z=-1: behind card (further into screen = more negative Z)

        // Head slot zone (green - valid) - at X=-2 (left side), Y=2 (above card)
        CreateDropZone(new Vector3(-2f, 2f, 0f), EquipmentSlot.Head, ColorValid, "Head Slot");

        // Hand slot zone (green - valid) - at X=2 (right side), Y=0 (same height as card)
        CreateDropZone(new Vector3(2f, 0f, 0f), EquipmentSlot.Hand1, ColorValid, "Hand Slot");

        // Invalid zone (red - should reject) - at X=-1 (left), Y=-2 (below card)
        CreateDropZone(new Vector3(-1f, -2f, 0f), EquipmentSlot.None, ColorInvalid, "Invalid Zone");

        // Neutral zone (gray - no action) - at X=1 (right), Y=-2 (below card)
        CreateDropZone(new Vector3(1f, -2f, 0f), EquipmentSlot.None, ColorNeutral, "Neutral Zone");
    }

    /// <summary>
    /// Creates a single drop zone with visual feedback.
    /// </summary>
    private void CreateDropZone(Vector3 position, EquipmentSlot slot, Color color, string label)
    {
        var zone = new Area3D { Name = $"DropZone_{slot}", Position = position };
        zone.SetMeta("slot", (int)slot);
        zone.CollisionLayer = 2;

        // Collision shape - also rotated to match visual
        var collision = new CollisionShape3D();
        var shape = new CylinderShape3D { Radius = 0.6f, Height = 0.05f };
        collision.Shape = shape;
        // Rotate to lie flat in XY plane
        collision.RotationDegrees = new Vector3(90f, 0f, 0f);
        zone.AddChild(collision);

        // Visual indicator - cylinder lying flat in XY plane (Z is depth)
        var visual = new MeshInstance3D();
        var cylinder = new CylinderMesh
        {
            TopRadius = 0.6f,
            BottomRadius = 0.6f,
            Height = 0.05f,
        };
        visual.Mesh = cylinder;
        // Rotate to lie flat in XY plane (Z is depth)
        visual.RotationDegrees = new Vector3(90f, 0f, 0f);

        var material = new StandardMaterial3D
        {
            AlbedoColor = color,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };
        visual.MaterialOverride = material;

        zone.AddChild(visual);

        // Label - positioned above the zone in Y (up)
        var zoneLabel = new Label3D
        {
            Text = label,
            FontSize = 20,
            Position = new Vector3(0f, 0.8f, 0f),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
        };
        zone.AddChild(zoneLabel);

        AddChild(zone);

        GameLogger.Debug($"Created drop zone: {label} at {position}", nameof(DragDropTest));
    }

    private void OnDragStarted(Node3D draggable)
    {
        GameLogger.Info($"Drag started: {draggable.Name}", nameof(DragDropTest));
        _draggedCard = draggable.GetNodeOrNull<DragDropHandler>(".");

        // Visual feedback - highlight card
        var mesh = draggable.GetNodeOrNull<MeshInstance3D>("CardMesh");
        if (mesh?.MaterialOverride is StandardMaterial3D material)
        {
            material.Emission = new Color(0.5f, 0.5f, 0.5f);
            material.EmissionEnabled = true;
        }
    }

    private void OnDragEnded(Node3D draggable, Vector3 position)
    {
        GameLogger.Info($"Drag ended at: {position}", nameof(DragDropTest));
        _draggedCard = null;

        // Remove highlight
        var mesh = draggable.GetNodeOrNull<MeshInstance3D>("CardMesh");
        if (mesh?.MaterialOverride is StandardMaterial3D material)
        {
            material.Emission = new Color(0, 0, 0);
            material.EmissionEnabled = false;
        }

        UpdateStatusDisplay();
    }

    private void OnDroppedOnSlot(Node3D draggable, int slotInt)
    {
        var slot = (EquipmentSlot)slotInt;
        GameLogger.Info($"Dropped on slot: {slot}", nameof(DragDropTest));

        if (slot == EquipmentSlot.None)
        {
            _failedDrops++;
            GameLogger.Warning("Dropped on invalid slot", nameof(DragDropTest));
        }
        else
        {
            _successfulDrops++;
            GameLogger.Info($"Successful drop on {slot}", nameof(DragDropTest));
        }

        UpdateStatusDisplay();
    }

    private void UpdateStatusDisplay()
    {
        if (_statusLabel != null)
        {
            _statusLabel.Text =
                $"Successful: {_successfulDrops}\n"
                + $"Failed: {_failedDrops}\n"
                + "Press SPACE for details";
        }
    }

    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("ui_accept"))
        {
            GameLogger.Info("--- DragDropTest Status ---", nameof(DragDropTest));
            GameLogger.Info($"Dragging: {_draggedCard != null}", nameof(DragDropTest));
            GameLogger.Info($"Successful drops: {_successfulDrops}", nameof(DragDropTest));
            GameLogger.Info($"Failed drops: {_failedDrops}", nameof(DragDropTest));
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent && keyEvent.Pressed)
        {
            if (keyEvent.Keycode == Key.R)
            {
                // Reset stats
                _successfulDrops = 0;
                _failedDrops = 0;
                UpdateStatusDisplay();
                GameLogger.Info("Stats reset", nameof(DragDropTest));
            }
        }
    }
}
