using Godot;

/// <summary>
/// Test scene for the visual feedback system.
/// Demonstrates card focus, selection, and zone highlighting.
/// </summary>
/// <remarks>
/// Part of Phase 2: Visual Feedback System testing.
/// Run this scene to verify visual states work correctly.
/// </remarks>
public partial class VisualFeedbackTest : Node3D
{
    [Export]
    public PackedScene CardScene; // Assign Card3D or CardVisual in inspector

    private Node3D _cardContainer;
    private FocusNavigator _focusNavigator;
    private Node3D[] _handCards = new Node3D[3];
    private CardVisualState[] _testCards = new CardVisualState[3];
    private PlayZoneHighlighter[] _testZones = new PlayZoneHighlighter[2];

    public override void _Ready()
    {
        GD.Print("[VisualFeedbackTest] Starting visual feedback test...");

        // Use autoloaded InputManager
        var inputManager = InputManager.Instance;
        if (inputManager == null)
        {
            GD.PrintErr("[VisualFeedbackTest] InputManager autoload not found!");
            return;
        }

        GD.Print("[VisualFeedbackTest] Using autoloaded InputManager");

        // Check InputRemappingManager
        var remapping = InputRemappingManager.Instance;
        if (remapping == null)
        {
            GD.PrintErr("[VisualFeedbackTest] InputRemappingManager autoload not found!");
            return;
        }
        GD.Print(
            $"[VisualFeedbackTest] InputRemappingManager loaded with {remapping.Bindings.Count} bindings"
        );

        // Initialize FocusNavigator
        _focusNavigator = new FocusNavigator();
        AddChild(_focusNavigator);

        // Connect input events
        inputManager.Navigate += OnNavigate;
        inputManager.SelectPressed += OnSelectPressed;
        inputManager.CancelPressed += OnCancelPressed;

        GD.Print("[VisualFeedbackTest] Input events connected");

        // Create test cards
        CreateTestCards();

        // Create test zones
        CreateTestZones();

        // Focus first card
        _focusNavigator.FocusFirst();

        // Verify initial focus
        if (_focusNavigator.CurrentFocus != null)
        {
            GD.Print($"[VisualFeedbackTest] Initial focus: {_focusNavigator.CurrentFocus.Name}");
        }
        else
        {
            GD.PrintErr("[VisualFeedbackTest] No initial focus set!");
        }

        GD.Print("[VisualFeedbackTest] Test setup complete.");
        GD.Print("Controls: Arrow keys/D-pad to navigate, Enter/Click to select, Escape to cancel");
    }

    /// <summary>
    /// Creates test cards in a row.
    /// </summary>
    private void CreateTestCards()
    {
        _cardContainer = new Node3D { Name = "TestCards" };
        AddChild(_cardContainer);

        for (int i = 0; i < 3; i++)
        {
            // Create a simple card mesh
            var cardNode = new Node3D
            {
                Name = $"TestCard{i}",
                Position = new Vector3((i - 1) * 3.0f, 0, 0),
            };

            // Add visual mesh
            var mesh = new MeshInstance3D
            {
                Name = "CardMesh",
                Mesh = new BoxMesh
                {
                    Size = new Vector3(2.5f, 0.1f, 3.5f), // Card-like dimensions
                },
            };
            mesh.SetSurfaceOverrideMaterial(
                0,
                new StandardMaterial3D { AlbedoColor = new Color(0.8f, 0.8f, 0.9f) }
            );
            cardNode.AddChild(mesh);

            // Add collision shape for mouse raycasting
            var staticBody = new StaticBody3D { Name = "CardCollider" };
            var collisionShape = new CollisionShape3D
            {
                Shape = new BoxShape3D { Size = new Vector3(2.5f, 0.2f, 3.5f) },
            };
            staticBody.AddChild(collisionShape);
            cardNode.AddChild(staticBody);

            // Add CardVisualState
            var visualState = new CardVisualState { Name = "CardVisualState" };
            cardNode.AddChild(visualState);

            _cardContainer.AddChild(cardNode);
            _handCards[i] = cardNode;
            _testCards[i] = visualState;

            // Register with focus navigator
            _focusNavigator.RegisterHandCard(cardNode);
        }

        GD.Print("[VisualFeedbackTest] Created 3 test cards");
    }

    /// <summary>
    /// Creates test zones behind the cards.
    /// </summary>
    private void CreateTestZones()
    {
        var zoneContainer = new Node3D { Name = "TestZones" };
        AddChild(zoneContainer);

        // Door slot
        var doorZone = new PlayZoneHighlighter
        {
            Name = "DoorZone",
            Position = new Vector3(-2.0f, 0, -4.0f),
            Type = PlayZoneHighlighter.ZoneType.DoorSlot,
        };
        zoneContainer.AddChild(doorZone);
        _testZones[0] = doorZone;
        _focusNavigator.RegisterPlayZone(doorZone);

        // Combat zone
        var combatZone = new PlayZoneHighlighter
        {
            Name = "CombatZone",
            Position = new Vector3(2.0f, 0, -4.0f),
            Type = PlayZoneHighlighter.ZoneType.CombatArea,
        };
        zoneContainer.AddChild(combatZone);
        _testZones[1] = combatZone;
        _focusNavigator.RegisterPlayZone(combatZone);

        GD.Print("[VisualFeedbackTest] Created 2 test zones");
    }

    /// <summary>
    /// Handles navigation input.
    /// </summary>
    private void OnNavigate(Vector2 direction)
    {
        GD.Print($"[VisualFeedbackTest] Navigate: {direction}");
        _focusNavigator.Navigate(direction);

        // Log new focus
        if (_focusNavigator.CurrentFocus != null)
        {
            GD.Print($"[VisualFeedbackTest] Now focused: {_focusNavigator.CurrentFocus.Name}");
        }
    }

    /// <summary>
    /// Handles select button press.
    /// </summary>
    private void OnSelectPressed()
    {
        var focusedCard = _focusNavigator.GetFocusedCardVisual();
        if (focusedCard != null)
        {
            // Toggle selection
            if (focusedCard.CurrentState == CardVisualState.State.Selected)
            {
                focusedCard.SetSelected(false);
                _focusNavigator.UnhighlightAllZones();
                GD.Print("[VisualFeedbackTest] Card deselected");
            }
            else
            {
                focusedCard.SetSelected(true);
                _focusNavigator.HighlightValidTargets(zone => true); // All zones valid for test

                // Auto-focus first zone so user sees immediate feedback
                if (_testZones.Length > 0 && _testZones[0] != null)
                {
                    _focusNavigator.SetFocus(_testZones[0]);
                }

                GD.Print(
                    "[VisualFeedbackTest] Card selected - zones highlighted, first zone focused"
                );
            }
        }
        else if (_focusNavigator.GetFocusedZone() != null)
        {
            // Zone selected - show ghost preview
            var zone = _focusNavigator.GetFocusedZone();
            zone.ShowGhostPreview();
            GD.Print("[VisualFeedbackTest] Zone selected - ghost preview shown");
        }
    }

    /// <summary>
    /// Handles cancel button press.
    /// </summary>
    private void OnCancelPressed()
    {
        // Clear all selections and highlights
        foreach (var card in _testCards)
        {
            card?.SetSelected(false);
        }

        foreach (var zone in _testZones)
        {
            zone?.Unhighlight();
            zone?.HideGhostPreview();
        }

        _focusNavigator.ClearFocus();
        _focusNavigator.FocusFirst();

        GD.Print("[VisualFeedbackTest] Cancelled - reset to initial state");
    }

    /// <summary>
    /// Handles mouse input for hover and click detection.
    /// </summary>
    public override void _UnhandledInput(InputEvent @event)
    {
        // Mouse motion - handle hover
        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleMouseHover(mouseMotion.Position);
        }
        // Mouse button - already handled by InputManager, but we can add click detection here if needed
    }

    /// <summary>
    /// Handles mouse hover to focus cards under cursor.
    /// </summary>
    private void HandleMouseHover(Vector2 mousePosition)
    {
        var camera = GetViewport().GetCamera3D();
        if (camera == null)
            return;

        // Raycast from camera through mouse position
        var rayOrigin = camera.ProjectRayOrigin(mousePosition);
        var rayDir = camera.ProjectRayNormal(mousePosition);

        // Check intersection with cards
        var spaceState = GetWorld3D().DirectSpaceState;
        var query = PhysicsRayQueryParameters3D.Create(rayOrigin, rayOrigin + rayDir * 100f);
        query.CollideWithAreas = true;
        query.CollideWithBodies = true;

        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var collider = result["collider"].AsGodotObject();

            // Check if we hit a card
            foreach (var card in _handCards)
            {
                if (IsDescendantOf(collider, card))
                {
                    if (_focusNavigator.CurrentFocus != card)
                    {
                        // Dim zones when switching from zone to card
                        if (_focusNavigator.GetFocusedZone() != null)
                        {
                            _focusNavigator.DimAllZones();
                        }

                        _focusNavigator.SetFocus(card);
                        GD.Print($"[VisualFeedbackTest] Mouse hover focused card: {card.Name}");
                    }
                    return;
                }
            }

            // Check if we hit a zone
            foreach (var zone in _testZones)
            {
                if (zone != null && IsDescendantOf(collider, zone))
                {
                    if (_focusNavigator.CurrentFocus != zone)
                    {
                        _focusNavigator.SetFocus(zone);
                        GD.Print($"[VisualFeedbackTest] Mouse hover focused zone: {zone.Name}");
                    }
                    return;
                }
            }
        }
        else
        {
            // Hovering nothing - dim all zones if we were focusing a zone
            if (_focusNavigator.GetFocusedZone() != null)
            {
                _focusNavigator.DimAllZones();
            }
        }
    }

    /// <summary>
    /// Checks if an object is a descendant of the given node.
    /// </summary>
    private bool IsDescendantOf(GodotObject obj, Node ancestor)
    {
        if (obj is Node node)
        {
            Node current = node;
            while (current != null)
            {
                if (current == ancestor)
                    return true;
                current = current.GetParent();
            }
        }
        return false;
    }

    public override void _ExitTree()
    {
        _focusNavigator?.ClearAll();
    }
}
