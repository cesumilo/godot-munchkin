using System.Linq;
using Godot;

/// <summary>
/// Handles 3D drag-and-drop for card visuals
/// Simple screen-space dragging implementation
/// </summary>
public partial class DragDropHandler : Node3D
{
    [Signal]
    public delegate void DragStartedEventHandler(Node3D draggable);

    [Signal]
    public delegate void DragEndedEventHandler(Node3D draggable, Vector3 position);

    [Signal]
    public delegate void DroppedOnSlotEventHandler(Node3D draggable, int slot);

    [Export]
    public bool IsDraggable { get; set; } = true;

    [Export]
    public float DragHeight { get; set; } = .5f;

    [Export]
    public float ReturnSpeed { get; set; } = 10.0f;

    private bool _isDragging = false;
    private Vector3 _originalPosition;
    private Vector2 _lastMousePosition;
    private RayCast3D _rayCast;
    private string _cardId = string.Empty;
    private ItemCardData _itemData = null;

    public override void _Ready()
    {
        // Try to find card data if parent is CardVisual
        var cardVisual = GetParent() as CardVisual;
        if (cardVisual != null && cardVisual.CardData != null)
        {
            _cardId = cardVisual.CardData.Id;
            _itemData = cardVisual.CardData as ItemCardData;
        }

        // Create raycast for detecting drop targets
        _rayCast = new RayCast3D
        {
            Enabled = false,
            CollisionMask = 2, // Use layer 2 for drop zones
            CollideWithAreas = true,
            CollideWithBodies = false,
        };
        AddChild(_rayCast);
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsDraggable)
            return;

        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return;

        if (@event is InputEventMouseButton mouseEvent)
        {
            if (mouseEvent.ButtonIndex == MouseButton.Left)
            {
                if (mouseEvent.Pressed && !_isDragging)
                {
                    if (IsMouseOverObject(mouseEvent.Position, camera))
                    {
                        StartDrag(mouseEvent.Position, camera);
                        GetViewport().SetInputAsHandled();
                    }
                }
                else if (!mouseEvent.Pressed && _isDragging)
                {
                    EndDrag(mouseEvent.Position, camera);
                    GetViewport().SetInputAsHandled();
                }
            }
        }
        else if (@event is InputEventMouseMotion motionEvent && _isDragging)
        {
            UpdateDragPosition(motionEvent.Position, camera);
            GetViewport().SetInputAsHandled();
        }
    }

    private bool IsMouseOverObject(Vector2 mousePosition, Camera3D camera)
    {
        var spaceState = GetWorld3D().DirectSpaceState;
        var from = camera.ProjectRayOrigin(mousePosition);
        var to = from + camera.ProjectRayNormal(mousePosition) * 1000;

        var query = PhysicsRayQueryParameters3D.Create(from, to);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;
        query.CollisionMask = 1; // Default layer

        var result = spaceState.IntersectRay(query);
        if (result.Count > 0)
        {
            var collider = result["collider"].As<Node3D>();

            // Check if collider is in our hierarchy
            var ourParent = GetParent();
            var theirParent = collider?.GetParent();

            if (ourParent != null && theirParent != null)
            {
                // Same parent (both children of CardVisual)
                if (ourParent == theirParent)
                    return true;

                // Or collider is child/grandchild of our parent
                if (ourParent.IsAncestorOf(collider))
                    return true;
            }
        }

        return false;
    }

    private void StartDrag(Vector2 mousePosition, Camera3D camera)
    {
        var parentNode = GetParent() as Node3D;
        if (parentNode == null)
            return;

        _isDragging = true;
        _originalPosition = parentNode.GlobalPosition;
        _lastMousePosition = mousePosition;

        // Raise card when dragging
        var raisedPosition = parentNode.GlobalPosition with
        {
            Z = DragHeight,
        };
        parentNode.GlobalPosition = raisedPosition;

        // Set raycast to shoot DOWN in world Z
        // Start ABOVE the card to avoid starting inside collision shapes
        var rayStartPos = parentNode.GlobalPosition with
        {
            Z = parentNode.GlobalPosition.Z + 1.0f,
        };

        // Ensure raycast has no rotation (shoots in world -Z)
        _rayCast.GlobalTransform = new Transform3D(Basis.Identity, rayStartPos);
        _rayCast.TargetPosition = new Vector3(0, 0, -20.0f); // 20 units down in local space

        // Enable raycast for drop detection
        _rayCast.Enabled = true;

        // Debug raycast setup
        GD.Print($"[DragDropHandler] Raycast setup:");
        GD.Print($"  Card position: {parentNode.GlobalPosition}");
        GD.Print($"  Ray start: {_rayCast.GlobalPosition}");
        GD.Print($"  Ray direction: {_rayCast.TargetPosition}");
        GD.Print($"  Ray end: {_rayCast.GlobalPosition + _rayCast.TargetPosition}");
        GD.Print($"  CollisionMask: {_rayCast.CollisionMask} (should be 2 for drop zones)");
        GD.Print($"  CollideWithAreas: {_rayCast.CollideWithAreas}");
        GD.Print($"  CollideWithBodies: {_rayCast.CollideWithBodies}");
        GD.Print($"  Ray rotation: {_rayCast.GlobalTransform.Basis.GetEuler()}");

        EmitSignal(SignalName.DragStarted, this);
    }

    private void UpdateDragPosition(Vector2 mousePosition, Camera3D camera)
    {
        if (!_isDragging)
            return;

        var parentNode = GetParent() as Node3D;
        if (parentNode == null)
            return;

        // Calculate mouse delta
        var mouseDelta = mousePosition - _lastMousePosition;
        _lastMousePosition = mousePosition;

        // Convert screen delta to world movement using camera basis
        var cameraRight = camera.GlobalTransform.Basis.X;
        var cameraUp = camera.GlobalTransform.Basis.Y;

        // Adjust sensitivity
        float moveSpeed = 0.01f;
        var worldDelta = (cameraRight * mouseDelta.X + cameraUp * -mouseDelta.Y) * moveSpeed;

        var targetPosition = parentNode.GlobalPosition + worldDelta;
        targetPosition.Z = DragHeight; // Keep at constant height

        parentNode.GlobalPosition = targetPosition;

        // Update raycast - shoot DOWN in world Z
        // Start ABOVE the card to avoid starting inside collision shapes
        var rayStartPos = parentNode.GlobalPosition with
        {
            Z = parentNode.GlobalPosition.Z + 1.0f,
        };
        _rayCast.GlobalTransform = new Transform3D(Basis.Identity, rayStartPos);
        _rayCast.TargetPosition = new Vector3(0, 0, -20.0f); // 20 units down in local space

        // Force raycast update
        _rayCast.ForceRaycastUpdate();
    }

    private void EndDrag(Vector2 mousePosition, Camera3D camera)
    {
        if (!_isDragging)
            return;

        _isDragging = false;

        EquipmentSlot dropSlot = EquipmentSlot.None;
        bool validDrop = false;

        // Update raycast position one last time before checking
        var parentNode = GetParent() as Node3D;
        if (parentNode != null)
        {
            // Start ABOVE the card to avoid starting inside collision shapes
            var rayStartPos = parentNode.GlobalPosition with
            {
                Z = parentNode.GlobalPosition.Z + 1.0f,
            };
            _rayCast.GlobalTransform = new Transform3D(Basis.Identity, rayStartPos);
            _rayCast.ForceRaycastUpdate();
        }

        // Check if we hit a drop zone
        if (_rayCast.IsColliding())
        {
            var collider = _rayCast.GetCollider() as Node3D;
            if (collider != null)
            {
                // Check if collider is a drop zone (Area3D with slot metadata)
                if (collider is Area3D area)
                {
                    GD.Print(
                        $"[DragDropHandler] HIT Area3D: '{area.Name}' at {area.GlobalPosition}"
                    );

                    // Get slot from metadata
                    var slotVariant = area.GetMeta("slot", (int)EquipmentSlot.None);
                    dropSlot = (EquipmentSlot)(int)slotVariant;
                    GD.Print($"[DragDropHandler] Dropped on slot: {dropSlot}");

                    // Validate if item can be equipped in this slot
                    if (_itemData != null && ValidateDrop(dropSlot))
                    {
                        validDrop = true;
                        EmitSignal(SignalName.DroppedOnSlot, this, (int)dropSlot);
                    }
                }
            }
        }

        if (validDrop)
        {
            GD.Print($"[DragDropHandler] Valid drop on {dropSlot}");
            // Success! Animate card down to slot position (Z=0)
            if (parentNode != null)
            {
                var slotPosition = parentNode.GlobalPosition with { Z = 0 };
                MoveToPosition(slotPosition);
            }
        }
        else
        {
            if (_rayCast.IsColliding())
            {
                var collider = _rayCast.GetCollider() as Node3D;
                GD.Print(
                    $"[DragDropHandler] Raycast HIT: {collider?.GetType().Name} '{collider?.Name}'"
                );

                // Check all Area3D properties
                if (collider is Area3D area)
                {
                    GD.Print($"[DragDropHandler] Area3D details:");
                    GD.Print($"  Global Position: {area.GlobalPosition}");
                    GD.Print($"  Collision Layer: {area.CollisionLayer}");

                    // Check metadata
                    if (area.HasMeta("slot"))
                    {
                        var slotValue = (int)area.GetMeta("slot", -1);
                        GD.Print(
                            $"[DragDropHandler] Area metadata 'slot' = {slotValue} ({(EquipmentSlot)slotValue})"
                        );
                        GD.Print($"[DragDropHandler] Item slot: {_itemData?.Slot}");
                    }
                    else
                    {
                        GD.PrintErr($"[DragDropHandler] Area3D has NO 'slot' metadata!");
                        GD.Print($"  All metadata keys: {string.Join(", ", area.GetMetaList())}");
                    }

                    // Check if area has CollisionShape
                    var collisionShape = area.GetChild<CollisionShape3D>(0);
                    if (collisionShape != null)
                    {
                        GD.Print(
                            $"  Has CollisionShape: Yes, Shape: {collisionShape.Shape?.GetType().Name}"
                        );
                    }
                    else
                    {
                        GD.PrintErr($"  Has CollisionShape: NO!");
                    }
                }
            }
            else
            {
                GD.Print($"[DragDropHandler] Raycast MISSED all drop zones");
                GD.Print($"[DragDropHandler] Raycast debug:");
                GD.Print($"  From: {_rayCast.GlobalPosition}");
                GD.Print($"  To: {_rayCast.GlobalPosition + _rayCast.TargetPosition}");
                GD.Print($"  Length: {_rayCast.TargetPosition.Length()}");

                // Manually check for Area3D nodes
                var root = GetTree().Root;
                var allAreas = new System.Collections.Generic.List<Area3D>();
                FindAllAreas(root, allAreas);

                if (allAreas.Count > 0)
                {
                    GD.Print($"[DragDropHandler] Found {allAreas.Count} Area3D nodes in scene:");
                    foreach (var area in allAreas)
                    {
                        GD.Print($"  - '{area.Name}' at {area.GlobalPosition}");
                        GD.Print(
                            $"    Layer: {area.CollisionLayer} (has layer 2: {(area.CollisionLayer & 2) == 2})"
                        );
                        GD.Print($"    Enabled: {area.ProcessMode != ProcessModeEnum.Disabled}");

                        var shape = area.GetChild<CollisionShape3D>(0);
                        GD.Print($"    Has CollisionShape: {shape != null}");
                        if (shape != null)
                        {
                            GD.Print($"    Shape Type: {shape.Shape?.GetType().Name}");
                        }
                    }
                }
                else
                {
                    GD.PrintErr($"[DragDropHandler] NO Area3D nodes found in scene!");
                }
            }

            GD.Print($"[DragDropHandler] Invalid drop, returning to original position");
            ReturnToOriginalPosition();
        }

        var parent = GetParent() as Node3D;
        EmitSignal(SignalName.DragEnded, this, parent?.GlobalPosition ?? GlobalPosition);
    }

    private bool ValidateDrop(EquipmentSlot slot)
    {
        if (_itemData == null)
        {
            GD.Print($"[DragDropHandler] ValidateDrop FAIL: _itemData is null");
            return false;
        }

        GD.Print($"[DragDropHandler] ValidateDrop checking:");
        GD.Print(
            $"  Item: {_itemData.Name}, Slot: {_itemData.Slot}, HandsRequired: {_itemData.HandsRequired}"
        );
        GD.Print($"  Drop slot: {slot}");

        // ALL items can be dropped in carried zone (EquipmentSlot.None)
        if (slot == EquipmentSlot.None)
        {
            GD.Print($"[DragDropHandler] Dropping in carried zone → always valid");
            return true;
        }

        // Check if item is for hands (based on Slot or HandsRequired)
        bool isHandItem =
            _itemData.Slot == EquipmentSlot.Hand1
            || _itemData.Slot == EquipmentSlot.Hand2
            || _itemData.Slot == EquipmentSlot.TwoHands
            || _itemData.HandsRequired == 1
            || _itemData.HandsRequired == 2;

        if (isHandItem)
        {
            // Hand item logic
            if (_itemData.HandsRequired == 2 || _itemData.Slot == EquipmentSlot.TwoHands)
            {
                // Two-handed item
                bool valid = slot == EquipmentSlot.TwoHands;
                GD.Print($"[DragDropHandler] Two-handed item → valid for TwoHands? {valid}");
                return valid;
            }
            else
            {
                // One-handed item (Hand1, Hand2, or HandsRequired=1)
                bool valid = slot == EquipmentSlot.Hand1 || slot == EquipmentSlot.Hand2;
                GD.Print($"[DragDropHandler] One-handed item → valid for Hand1/Hand2? {valid}");
                return valid;
            }
        }
        else
        {
            // Non-hand items: exact slot match
            bool valid = _itemData.Slot == slot;
            GD.Print(
                $"[DragDropHandler] Non-hand item → exact match? {valid} (item.Slot={_itemData.Slot}, drop={slot})"
            );
            return valid;
        }
    }

    private async void ReturnToOriginalPosition()
    {
        float t = 0;
        var parent = GetParent() as Node3D;
        if (parent == null)
            return;

        var startPos = parent.GlobalPosition;

        while (t < 1.0f)
        {
            t += (float)GetProcessDeltaTime() * ReturnSpeed;
            parent.GlobalPosition = startPos.Lerp(_originalPosition, Mathf.Clamp(t, 0, 1));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        parent.GlobalPosition = _originalPosition;
    }

    private async void MoveToPosition(Vector3 targetPosition)
    {
        float t = 0;
        var parent = GetParent() as Node3D;
        if (parent == null)
            return;

        var startPos = parent.GlobalPosition;

        while (t < 1.0f)
        {
            t += (float)GetProcessDeltaTime() * ReturnSpeed;
            parent.GlobalPosition = startPos.Lerp(targetPosition, Mathf.Clamp(t, 0, 1));
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        parent.GlobalPosition = targetPosition;
    }

    public void SetCardData(string cardId, ItemCardData itemData)
    {
        _cardId = cardId;
        _itemData = itemData;
    }

    private void FindAllAreas(Node node, System.Collections.Generic.List<Area3D> areas)
    {
        if (node is Area3D area)
        {
            areas.Add(area);
        }

        foreach (var child in node.GetChildren())
        {
            FindAllAreas(child, areas);
        }
    }
}
