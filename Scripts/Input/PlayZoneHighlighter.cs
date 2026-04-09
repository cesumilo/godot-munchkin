using Godot;

/// <summary>
/// Highlights a play zone (door slot, combat area, equipment slot) when it becomes
/// a valid target for card play. Shows ghost preview of card.
/// </summary>
/// <remarks>
/// Part of Phase 2: Visual Feedback System.
/// Attach to any drop zone or play area marker.
/// </remarks>
public partial class PlayZoneHighlighter : Node3D
{
    /// <summary>
    /// Type of play zone.
    /// </summary>
    public enum ZoneType
    {
        DoorSlot,
        CombatArea,
        EquipmentSlot,
        DiscardPile,
    }

    [Export]
    public ZoneType Type { get; set; } = ZoneType.DoorSlot;

    [Export]
    public string ZoneId { get; set; } = "";

    // Visual configuration
    [Export]
    public Color ValidHighlightColor = new Color(0.0f, 1.0f, 0.0f, 0.3f); // Green transparent

    [Export]
    public Color InvalidHighlightColor = new Color(1.0f, 0.0f, 0.0f, 0.3f); // Red transparent

    [Export]
    public float HighlightAnimationDuration = 0.15f;

    // References
    private MeshInstance3D _highlightMesh;
    private MeshInstance3D _ghostCardMesh;
    private Tween _activeTween;

    // State
    private bool _isHighlighted = false;
    private bool _isValidTarget = true;

    public override void _Ready()
    {
        SetupHighlightMesh();
        SetupGhostCardMesh();
        SetupCollision();
    }

    /// <summary>
    /// Creates collision shape for mouse raycast detection.
    /// </summary>
    private void SetupCollision()
    {
        var staticBody = new StaticBody3D { Name = "ZoneCollider" };
        var collisionShape = new CollisionShape3D
        {
            Shape = new BoxShape3D
            {
                Size = new Vector3(3.0f, 0.2f, 4.0f), // Card-sized area
            },
        };
        staticBody.AddChild(collisionShape);
        AddChild(staticBody);
    }

    /// <summary>
    /// Creates the highlight mesh (simple plane under the zone).
    /// </summary>
    private void SetupHighlightMesh()
    {
        _highlightMesh = new MeshInstance3D
        {
            Name = "HighlightMesh",
            Mesh = new PlaneMesh
            {
                Size = new Vector2(3.0f, 4.0f), // Card-sized
            },
            Visible = false,
        };

        var material = new StandardMaterial3D
        {
            AlbedoColor = ValidHighlightColor,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = StandardMaterial3D.ShadingModeEnum.Unshaded,
        };

        _highlightMesh.SetSurfaceOverrideMaterial(0, material);
        _highlightMesh.Position = new Vector3(0, -0.05f, 0); // Slightly below card

        AddChild(_highlightMesh);
    }

    /// <summary>
    /// Creates the ghost card mesh (semi-transparent preview).
    /// </summary>
    private void SetupGhostCardMesh()
    {
        _ghostCardMesh = new MeshInstance3D
        {
            Name = "GhostCardMesh",
            Mesh = new PlaneMesh
            {
                Size = new Vector2(2.5f, 3.5f), // Card size
            },
            Visible = false,
        };

        var material = new StandardMaterial3D
        {
            AlbedoColor = new Color(1.0f, 1.0f, 1.0f, 0.4f),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        };

        _ghostCardMesh.SetSurfaceOverrideMaterial(0, material);
        _ghostCardMesh.RotationDegrees = new Vector3(-90, 0, 0); // Face up

        AddChild(_ghostCardMesh);
    }

    /// <summary>
    /// Highlights the zone as a valid target.
    /// </summary>
    /// <param name="valid">True if valid target, false if invalid.</param>
    public void Highlight(bool valid = true)
    {
        if (_isHighlighted && _isValidTarget == valid)
            return;

        _isHighlighted = true;
        _isValidTarget = valid;

        var material = _highlightMesh.GetActiveMaterial(0) as StandardMaterial3D;
        if (material != null)
        {
            material.AlbedoColor = valid ? ValidHighlightColor : InvalidHighlightColor;
        }

        _highlightMesh.Visible = true;

        // Animate in
        _activeTween?.Kill();
        _activeTween = CreateTween();
        _activeTween.SetTrans(Tween.TransitionType.Cubic);
        _activeTween.SetEase(Tween.EaseType.Out);

        _highlightMesh.Scale = Vector3.Zero;
        _activeTween.TweenProperty(
            _highlightMesh,
            "scale",
            Vector3.One,
            HighlightAnimationDuration
        );
    }

    /// <summary>
    /// Dims the zone highlight (stays visible but at reduced opacity).
    /// Used when another zone is focused.
    /// </summary>
    public void Dim()
    {
        if (!_isHighlighted)
            return;

        _isHighlighted = false; // No longer the "active" highlight

        // Animate to dimmed state
        _activeTween?.Kill();
        _activeTween = CreateTween();
        _activeTween.SetTrans(Tween.TransitionType.Cubic);
        _activeTween.SetEase(Tween.EaseType.Out);

        var material = _highlightMesh.GetActiveMaterial(0) as StandardMaterial3D;
        if (material != null)
        {
            // Dim to 30% opacity
            var dimmedColor = material.AlbedoColor with
            {
                A = 0.1f,
            };
            _activeTween.TweenProperty(
                material,
                "albedo_color",
                dimmedColor,
                HighlightAnimationDuration
            );
        }

        _activeTween.TweenProperty(
            _highlightMesh,
            "scale",
            Vector3.One * 0.8f,
            HighlightAnimationDuration
        );
    }

    /// <summary>
    /// Removes highlight from the zone.
    /// </summary>
    public void Unhighlight()
    {
        if (!_isHighlighted && !_highlightMesh.Visible)
            return;

        _isHighlighted = false;

        // Animate out
        _activeTween?.Kill();
        _activeTween = CreateTween();
        _activeTween.SetTrans(Tween.TransitionType.Cubic);
        _activeTween.SetEase(Tween.EaseType.Out);

        _activeTween.TweenProperty(
            _highlightMesh,
            "scale",
            Vector3.Zero,
            HighlightAnimationDuration
        );
        _activeTween.TweenCallback(Callable.From(() => _highlightMesh.Visible = false));
    }

    /// <summary>
    /// Shows a ghost preview of a card at this zone.
    /// </summary>
    /// <param name="cardTexture">Optional texture to show on the ghost card.</param>
    public void ShowGhostPreview(Texture2D cardTexture = null)
    {
        if (cardTexture != null)
        {
            var material = _ghostCardMesh.GetActiveMaterial(0) as StandardMaterial3D;
            if (material != null)
            {
                material.AlbedoTexture = cardTexture;
            }
        }

        _ghostCardMesh.Visible = true;

        // Animate in
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.Out);

        _ghostCardMesh.Scale = Vector3.Zero;
        tween.TweenProperty(_ghostCardMesh, "scale", Vector3.One, 0.1f);
    }

    /// <summary>
    /// Hides the ghost card preview.
    /// </summary>
    public void HideGhostPreview()
    {
        if (!_ghostCardMesh.Visible)
            return;

        // Animate out
        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.Out);

        tween.TweenProperty(_ghostCardMesh, "scale", Vector3.Zero, 0.1f);
        tween.TweenCallback(
            Callable.From(() =>
            {
                _ghostCardMesh.Visible = false;

                // Clear texture
                var material = _ghostCardMesh.GetActiveMaterial(0) as StandardMaterial3D;
                if (material != null)
                {
                    material.AlbedoTexture = null;
                }
            })
        );
    }

    /// <summary>
    /// Gets whether this zone is currently highlighted.
    /// </summary>
    public bool IsHighlighted => _isHighlighted;

    /// <summary>
    /// Gets whether this zone is a valid target.
    /// </summary>
    public bool IsValidTarget => _isValidTarget;

    /// <summary>
    /// Pulse animation for emphasis.
    /// </summary>
    public void Pulse()
    {
        if (!_isHighlighted)
            return;

        var tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Sine);
        tween.SetEase(Tween.EaseType.InOut);

        // Scale up and down
        tween.TweenProperty(_highlightMesh, "scale", Vector3.One * 1.1f, 0.2f);
        tween.TweenProperty(_highlightMesh, "scale", Vector3.One, 0.2f);
    }
}
