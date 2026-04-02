using System.Linq;
using Godot;

/// <summary>
/// Handles 3D drag-and-drop interactions for card visuals.
/// Implements screen-space dragging with raycast-based drop zone detection.
/// </summary>
/// <remarks>
/// Per AGENTS.md discoveries: Z is vertical in this coordinate system.
/// Drop zone Area3D nodes have thin CollisionShape3D (Z height 0.1 units).
/// Raycast must start above shapes to detect properly.
/// </remarks>
public partial class DragDropHandler : Node3D
{
    /// <summary>
    /// Emitted when dragging starts.
    /// </summary>
    /// <param name="draggable">The Node3D being dragged.</param>
    [Signal]
    public delegate void DragStartedEventHandler(Node3D draggable);

    /// <summary>
    /// Emitted when dragging ends.
    /// </summary>
    /// <param name="draggable">The Node3D that was dragged.</param>
    /// <param name="position">The final position where dragging ended.</param>
    [Signal]
    public delegate void DragEndedEventHandler(Node3D draggable, Vector3 position);

    /// <summary>
    /// Emitted when card is dropped on a valid equipment slot.
    /// </summary>
    /// <param name="draggable">The Node3D that was dropped.</param>
    /// <param name="slot">The EquipmentSlot value where dropped.</param>
    [Signal]
    public delegate void DroppedOnSlotEventHandler(Node3D draggable, int slot);

    /// <summary>
    /// Gets or sets whether this card can be dragged.
    /// </summary>
    [Export]
    public bool IsDraggable { get; set; } = true;

    /// <summary>
    /// Gets or sets the height above the table while dragging.
    /// </summary>
    /// <value>Default is 0.5 units in Z (vertical).</value>
    [Export]
    public float DragHeight { get; set; } = .5f;

    /// <summary>
    /// Gets or sets the animation speed for returning to original position.
    /// </summary>
    /// <value>Higher values mean faster return animation.</value>
    [Export]
    public float ReturnSpeed { get; set; } = 10.0f;

    private bool _isDragging = false;
    private Vector3 _originalPosition;
    private Vector2 _lastMousePosition;
    private RayCast3D _rayCast;
    private string _cardId = string.Empty;
    private ItemCardData _itemData = null;

    /// <summary>
    /// Initializes the drag drop handler and creates raycast for drop detection.
    /// </summary>
    /// <remarks>
    /// Attempts to find card data from parent CardVisual if available.
    /// Creates RayCast3D configured for drop zone detection (layer 2).
    /// </remarks>
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

    /// <summary>
    /// Handles input events for drag detection.
    /// </summary>
    /// <param name="@event">The input event.</param>
    /// <remarks>
    /// Processes mouse button and motion events to manage drag state.
    /// Only processes input when IsDraggable is true and mouse is over the card.
    /// </remarks>
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

    /// <summary>
    /// Checks if mouse is over this card's collider.
    /// </summary>
    /// <param name="mousePosition">Current mouse position in screen coordinates.</param>
    /// <param name="camera">The active camera.</param>
    /// <returns>True if mouse is over this card; false otherwise.</returns>
    /// <remarks>
    /// Uses physics raycast to detect collision with this card's geometry.
    /// </remarks>
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

    /// <summary>
    /// Starts dragging the card.
    /// </summary>
    /// <param name="mousePosition">Current mouse position.</param>
    /// <param name="camera">The active camera.</param>
    /// <remarks>
    /// Stores original position, raises card to DragHeight, and enables drop detection raycast.
    /// Emits DragStarted signal.
    /// </remarks>
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

    /// <summary>
    /// Updates the dragged card's position during mouse movement.
    /// </summary>
    /// <param name="mousePosition">Current mouse position.</param>
    /// <param name="camera">The active camera.</param>
    /// <remarks>
    /// Converts screen-space mouse delta to world-space movement using camera basis vectors.
    /// Updates raycast position for drop zone detection.
    /// </remarks>
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

    /// <summary>
    /// Ends dragging and determines if a valid drop occurred.
    /// </summary>
    /// <param name="mousePosition">Final mouse position.</param>
    /// <param name="camera">The active camera.</param>
    /// <remarks>
    /// Uses raycast to detect drop zone. If valid, emits DroppedOnSlot signal.
    /// Otherwise, animates card back to original position.
    /// </remarks>
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

    /// <summary>
    /// Validates whether an item can be dropped in the specified slot.
    /// </summary>
    /// <param name="slot">The target equipment slot.</param>
    /// <returns>True if the drop is valid; false otherwise.</returns>
    /// <remarks>
    /// Per §9: Validates slot compatibility including hand requirements and item slot restrictions.
    /// All items can be dropped in carried zone (EquipmentSlot.None).
    /// </remarks>
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

    /// <summary>
    /// Animates the card back to its original position.
    /// </summary>
    /// <remarks>
    /// Called when drop is invalid. Uses Lerp for smooth animation.
    /// </remarks>
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

    /// <summary>
    /// Animates the card to a target position.
    /// </summary>
    /// <param name="targetPosition">The destination position.</param>
    /// <remarks>
    /// Called when drop is valid. Uses Lerp for smooth placement animation.
    /// </remarks>
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

    /// <summary>
    /// Sets the card data for this drag handler.
    /// </summary>
    /// <param name="cardId">The unique card identifier.</param>
    /// <param name="itemData">The item card data.</param>
    public void SetCardData(string cardId, ItemCardData itemData)
    {
        _cardId = cardId;
        _itemData = itemData;
    }

    /// <summary>
    /// Recursively finds all Area3D nodes in the scene.
    /// </summary>
    /// <param name="node">The starting node.</param>
    /// <param name="areas">List to collect found areas.</param>
    /// <remarks>
    /// Used for debugging drop zone detection issues.
    /// </remarks>
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
