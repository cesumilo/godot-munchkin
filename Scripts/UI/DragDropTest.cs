using Godot;

/// <summary>
/// Simple test for drag-and-drop functionality
/// </summary>
public partial class DragDropTest : Node3D
{
    [Export]
    private PackedScene _testCardScene;

    [Export]
    private Node3D[] _slotMarkers;

    private DragDropHandler _draggedCard;

    public override void _Ready()
    {
        GD.Print("=== DragDropTest Started ===");

        // Create a test card
        if (_testCardScene != null)
        {
            var testCard = _testCardScene.Instantiate<Node3D>();
            AddChild(testCard);
            testCard.GlobalPosition = new Vector3(0, 1, 0);

            // Add drag handler
            var dragHandler = new DragDropHandler();
            testCard.AddChild(dragHandler);
            dragHandler.DragStarted += OnDragStarted;
            dragHandler.DragEnded += OnDragEnded;
            dragHandler.DroppedOnSlot += OnDroppedOnSlot;

            GD.Print("Test card created with drag handler");
        }

        // Note: Slot markers should be Area3D nodes with "slot" metadata
        // Added in Godot editor, not via code
        GD.Print("DragDropTest ready. Add Area3D nodes with 'slot' metadata to test drop zones.");
    }

    private void OnDragStarted(Node3D draggable)
    {
        GD.Print($"Drag started: {draggable.Name}");
        _draggedCard = draggable.GetNodeOrNull<DragDropHandler>(".");
    }

    private void OnDragEnded(Node3D draggable, Vector3 position)
    {
        GD.Print($"Drag ended at: {position}");
        _draggedCard = null;
    }

    private void OnDroppedOnSlot(Node3D draggable, int slotInt)
    {
        EquipmentSlot slot = (EquipmentSlot)slotInt;
        GD.Print($"Dropped on slot: {slot}");
    }

    public override void _Process(double delta)
    {
        // Simple test: press Space to log current state
        if (Input.IsActionJustPressed("ui_accept"))
        {
            GD.Print("--- DragDropTest Status ---");
            GD.Print($"Dragging: {_draggedCard != null}");
        }
    }
}
