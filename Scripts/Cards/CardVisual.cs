using Godot;

/// <summary>
/// Visual representation of a card in 3D space.
/// Handles card display, face-up/down states, and hover effects.
/// </summary>
/// <remarks>
/// Per §4: Displays card name, description, and visual styling based on card type.
/// Integrates with Card3D plugin for 3D card visuals.
/// </remarks>
public partial class CardVisual : Node3D
{
    /// <summary>
    /// Gets or sets the card data to display.
    /// </summary>
    [Export]
    private CardData _cardData;

    private Label3D _nameLabel;
    private Label3D _descriptionLabel;
    private MeshInstance3D _cardMesh;

    /// <summary>
    /// Gets or sets the card data and updates visuals.
    /// </summary>
    public CardData CardData
    {
        get => _cardData;
        set
        {
            _cardData = value;
            UpdateVisuals();
        }
    }

    /// <summary>
    /// Initializes the card visual by finding child nodes.
    /// </summary>
    public override void _Ready()
    {
        // Try to find child nodes
        _nameLabel = GetNodeOrNull<Label3D>("CardMesh/NameLabel");
        _descriptionLabel = GetNodeOrNull<Label3D>("CardMesh/DescriptionLabel");
        _cardMesh = GetNodeOrNull<MeshInstance3D>("CardMesh");

        if (_cardData != null)
        {
            UpdateVisuals();
        }
    }

    /// <summary>
    /// Updates card visual elements based on card data.
    /// </summary>
    private void UpdateVisuals()
    {
        if (_cardData == null)
            return;

        // Update name label if exists
        if (_nameLabel != null)
        {
            _nameLabel.Text = _cardData.Name;
        }

        // Update description label if exists
        if (_descriptionLabel != null)
        {
            _descriptionLabel.Text = _cardData.Description;
        }

        // Update card color based on type
        if (_cardMesh != null && _cardMesh.MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = GetCardColor();
        }

        GD.Print($"[CardVisual] Updated visual for: {_cardData.Name} ({_cardData.Type})");
    }

    /// <summary>
    /// Gets color based on card type.
    /// </summary>
    /// <returns>Color appropriate for the card type.</returns>
    private Color GetCardColor()
    {
        return _cardData.Type switch
        {
            CardData.CardType.Monster => new Color(1.0f, 0.5f, 0.5f), // Reddish
            CardData.CardType.Item => new Color(0.5f, 1.0f, 0.5f), // Greenish
            CardData.CardType.Curse => new Color(0.8f, 0.2f, 0.8f), // Purple
            CardData.CardType.Race => new Color(0.5f, 0.8f, 1.0f), // Blueish
            CardData.CardType.Class => new Color(1.0f, 1.0f, 0.5f), // Yellowish
            CardData.CardType.Action => new Color(1.0f, 0.8f, 0.5f), // Orange
            _ => new Color(0.8f, 0.8f, 0.8f), // Gray
        };
    }

    /// <summary>
    /// Sets the card face orientation.
    /// </summary>
    /// <param name="faceDown">True to show card back; false for face up.</param>
    public void SetFaceDown(bool faceDown)
    {
        // Per §1: Cards in hand are not visible to other players (face-down)
        // Per §9.4: Worn equipment is visible to all players (face-up)
        if (faceDown)
        {
            RotationDegrees = new Vector3(0, 180, 0);
        }
        else
        {
            RotationDegrees = Vector3.Zero;
        }
    }

    /// <summary>
    /// Sets hover state for visual feedback.
    /// </summary>
    /// <param name="hovered">True when hovered; false otherwise.</param>
    public void SetHovered(bool hovered)
    {
        if (hovered)
        {
            Position = new Vector3(0, 0.1f, 0);
        }
        else
        {
            Position = Vector3.Zero;
        }
    }
}
