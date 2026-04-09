using Godot;

/// <summary>
/// Manages visual states for a card: Idle, Focused, Selected.
/// Handles animations for lift, tilt, and glow effects.
/// </summary>
/// <remarks>
/// Part of Phase 2: Visual Feedback System.
/// Attach to any Card3D or card visual node.
/// </remarks>
public partial class CardVisualState : Node3D
{
    /// <summary>
    /// The card's visual state.
    /// </summary>
    public enum State
    {
        /// <summary>Normal resting position.</summary>
        Idle,

        /// <summary>Hovered/focused - slight lift and glow.</summary>
        Focused,

        /// <summary>Selected - higher lift, tilt, stronger glow.</summary>
        Selected,
    }

    /// <summary>
    /// Current visual state of the card.
    /// </summary>
    [Export]
    public State CurrentState { get; private set; } = State.Idle;

    // Visual configuration
    [Export]
    public float FocusedLift = 0.2f;

    [Export]
    public float SelectedLift = 0.5f;

    [Export]
    public float SelectedTiltDegrees = 15.0f;

    [Export]
    public float AnimationDuration = 0.2f;

    // References
    private Node3D _targetNode; // The node to animate (parent or mesh)
    private StandardMaterial3D _glowMaterial;

    // Animation state
    private Tween _activeTween;
    private Vector3 _basePosition;
    private Vector3 _baseRotation;

    public override void _Ready()
    {
        // Find the node to animate and setup material
        SetupTargetNode();

        // Use target node's base position
        if (_targetNode != null)
        {
            _basePosition = _targetNode.Position;
            _baseRotation = _targetNode.RotationDegrees;
        }
        else
        {
            _basePosition = Position;
            _baseRotation = RotationDegrees;
        }
    }

    /// <summary>
    /// Finds the target node to animate and sets up glow material.
    /// </summary>
    private void SetupTargetNode()
    {
        // Try to find mesh instance as child first
        MeshInstance3D meshInstance = null;

        // Check children
        foreach (var child in GetChildren())
        {
            if (child is MeshInstance3D mesh)
            {
                meshInstance = mesh;
                break;
            }
        }

        // If not found, check siblings in parent
        if (meshInstance == null && GetParent() != null)
        {
            foreach (var sibling in GetParent().GetChildren())
            {
                if (sibling is MeshInstance3D mesh && sibling != this)
                {
                    meshInstance = mesh;
                    break;
                }
            }
        }

        if (meshInstance != null)
        {
            // Animate the mesh itself
            _targetNode = meshInstance;
            SetupGlowMaterial(meshInstance);
        }
        else if (GetParent() is Node3D parent)
        {
            // Animate the parent node
            _targetNode = parent;
        }
        else
        {
            // Fallback to self
            _targetNode = this;
        }
    }

    /// <summary>
    /// Sets up the glow material on the given mesh.
    /// </summary>
    private void SetupGlowMaterial(MeshInstance3D meshInstance)
    {
        _glowMaterial = new StandardMaterial3D
        {
            EmissionEnabled = true,
            Emission = Colors.Transparent,
            EmissionEnergyMultiplier = 0.0f,
        };

        // Store original material
        var originalMaterial = meshInstance.GetActiveMaterial(0);
        if (originalMaterial != null)
        {
            _glowMaterial.AlbedoColor = originalMaterial is StandardMaterial3D stdMat
                ? stdMat.AlbedoColor
                : Colors.White;
        }

        meshInstance.SetSurfaceOverrideMaterial(0, _glowMaterial);
    }

    /// <summary>
    /// Sets the visual state with animation.
    /// </summary>
    /// <param name="newState">The target state.</param>
    public void SetState(State newState)
    {
        if (CurrentState == newState)
            return;

        CurrentState = newState;
        ApplyStateVisuals();
    }

    /// <summary>
    /// Applies visual changes based on current state.
    /// </summary>
    private void ApplyStateVisuals()
    {
        // Kill any active tween
        _activeTween?.Kill();
        _activeTween = CreateTween();
        _activeTween.SetTrans(Tween.TransitionType.Cubic);
        _activeTween.SetEase(Tween.EaseType.Out);

        Vector3 targetPosition = _basePosition;
        Vector3 targetRotation = _baseRotation;
        Color glowColor = Colors.Transparent;
        float glowEnergy = 0.0f;

        switch (CurrentState)
        {
            case State.Idle:
                // Return to base
                targetPosition = _basePosition;
                targetRotation = _baseRotation;
                glowColor = Colors.Transparent;
                glowEnergy = 0.0f;
                break;

            case State.Focused:
                // Slight lift
                targetPosition = _basePosition with
                {
                    Y = _basePosition.Y + FocusedLift,
                };
                targetRotation = _baseRotation;
                glowColor = Colors.White;
                glowEnergy = 0.3f;
                break;

            case State.Selected:
                // Higher lift + tilt toward camera
                targetPosition = _basePosition with
                {
                    Y = _basePosition.Y + SelectedLift,
                };
                targetRotation = new Vector3(
                    _baseRotation.X + SelectedTiltDegrees,
                    _baseRotation.Y,
                    _baseRotation.Z
                );
                glowColor = Colors.Gold;
                glowEnergy = 0.8f;
                break;
        }

        // Animate target node position and rotation
        if (_targetNode != null)
        {
            _activeTween
                .Parallel()
                .TweenProperty(_targetNode, "position", targetPosition, AnimationDuration);
            _activeTween
                .Parallel()
                .TweenProperty(_targetNode, "rotation_degrees", targetRotation, AnimationDuration);
        }

        // Animate glow
        if (_glowMaterial != null)
        {
            _activeTween
                .Parallel()
                .TweenProperty(_glowMaterial, "emission", glowColor, AnimationDuration);
            _activeTween
                .Parallel()
                .TweenProperty(
                    _glowMaterial,
                    "emission_energy_multiplier",
                    glowEnergy,
                    AnimationDuration
                );
        }
    }

    /// <summary>
    /// Sets the focus state (convenience method).
    /// </summary>
    public void SetFocus(bool focused)
    {
        if (focused && CurrentState == State.Idle)
        {
            SetState(State.Focused);
        }
        else if (!focused && CurrentState == State.Focused)
        {
            SetState(State.Idle);
        }
    }

    /// <summary>
    /// Sets the selected state (convenience method).
    /// </summary>
    public void SetSelected(bool selected)
    {
        if (selected)
        {
            SetState(State.Selected);
        }
        else
        {
            SetState(State.Idle);
        }
    }

    /// <summary>
    /// Gets whether the card is currently focused or selected.
    /// </summary>
    public bool IsFocusedOrSelected => CurrentState != State.Idle;

    /// <summary>
    /// Updates the base position (e.g., when card moves to new slot).
    /// </summary>
    public void UpdateBasePosition(Vector3 newBasePosition)
    {
        _basePosition = newBasePosition;

        // Re-apply current state to new position
        if (CurrentState != State.Idle)
        {
            ApplyStateVisuals();
        }
    }

    /// <summary>
    /// Resets the card to idle state immediately (no animation).
    /// </summary>
    public void ResetImmediately()
    {
        _activeTween?.Kill();
        CurrentState = State.Idle;

        if (_targetNode != null)
        {
            _targetNode.Position = _basePosition;
            _targetNode.RotationDegrees = _baseRotation;
        }

        if (_glowMaterial != null)
        {
            _glowMaterial.Emission = Colors.Transparent;
            _glowMaterial.EmissionEnergyMultiplier = 0.0f;
        }
    }
}
