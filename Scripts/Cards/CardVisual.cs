using Godot;

public partial class CardVisual : Node3D
{
    [Export]
    private CardData _cardData;

    private Label3D _nameLabel;
    private Label3D _descriptionLabel;
    private MeshInstance3D _cardMesh;

    public CardData CardData
    {
        get => _cardData;
        set
        {
            _cardData = value;
            UpdateVisuals();
        }
    }

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

        // Update card color based on type (temporary visualization)
        if (_cardMesh != null && _cardMesh.MaterialOverride is StandardMaterial3D material)
        {
            material.AlbedoColor = GetCardColor();
        }

        GD.Print($"[CardVisual] Updated visual for: {_cardData.Name} ({_cardData.Type})");
    }

    private Color GetCardColor()
    {
        // Temporary color coding for card types
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

    public void SetFaceDown(bool faceDown)
    {
        // TODO: Implement card flipping when integrated with Card3D plugin
        if (faceDown)
        {
            RotationDegrees = new Vector3(0, 180, 0);
        }
        else
        {
            RotationDegrees = Vector3.Zero;
        }
    }

    public void SetHovered(bool hovered)
    {
        // TODO: Implement hover effect when integrated with Card3D plugin
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
