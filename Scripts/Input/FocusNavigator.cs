using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// Manages focus navigation between cards, zones, and UI elements.
/// Supports directional navigation (D-pad, arrow keys, analog stick).
/// </summary>
/// <remarks>
/// Part of Phase 2: Visual Feedback System.
/// Attach to GameBoard or a dedicated interaction manager.
/// </remarks>
public partial class FocusNavigator : Node
{
    /// <summary>
    /// Event fired when focus changes to a new element.
    /// </summary>
    [Signal]
    public delegate void FocusChangedEventHandler(Node3D focusedElement);

    /// <summary>
    /// Event fired when focus is cleared.
    /// </summary>
    [Signal]
    public delegate void FocusClearedEventHandler();

    /// <summary>
    /// Layers for focus navigation.
    /// </summary>
    public enum FocusLayer
    {
        HandCards = 0,
        PlayZones = 1,
        UI = 2,
    }

    // Collections of focusable elements
    private List<Node3D> _handCards = new();
    private List<PlayZoneHighlighter> _playZones = new();

    // Current focus state
    private Node3D _currentFocus = null;
    private int _currentLayer = 0;
    private int _currentIndex = -1;

    // References
    private CardVisualState _currentCardVisual = null;
    private PlayZoneHighlighter _currentZoneHighlighter = null;

    /// <summary>
    /// Gets the currently focused element.
    /// </summary>
    public Node3D CurrentFocus => _currentFocus;

    /// <summary>
    /// Gets whether any element is currently focused.
    /// </summary>
    public bool HasFocus => _currentFocus != null;

    /// <summary>
    /// Registers a hand card for navigation.
    /// </summary>
    public void RegisterHandCard(Node3D card)
    {
        if (!_handCards.Contains(card))
        {
            _handCards.Add(card);
            _handCards.Sort((a, b) => a.Position.X.CompareTo(b.Position.X)); // Left to right
        }
    }

    /// <summary>
    /// Unregisters a hand card.
    /// </summary>
    public void UnregisterHandCard(Node3D card)
    {
        _handCards.Remove(card);

        if (_currentFocus == card)
        {
            ClearFocus();
        }
    }

    /// <summary>
    /// Registers a play zone for navigation.
    /// </summary>
    public void RegisterPlayZone(PlayZoneHighlighter zone)
    {
        if (!_playZones.Contains(zone))
        {
            _playZones.Add(zone);
        }
    }

    /// <summary>
    /// Clears all registered elements.
    /// </summary>
    public void ClearAll()
    {
        ClearFocus();
        _handCards.Clear();
        _playZones.Clear();
    }

    /// <summary>
    /// Navigates in a direction.
    /// </summary>
    /// <param name="direction">Navigation direction (X = left/right, Y = up/down).</param>
    public void Navigate(Vector2 direction)
    {
        if (direction == Vector2.Zero)
            return;

        // Determine primary direction
        bool horizontal = Mathf.Abs(direction.X) > Mathf.Abs(direction.Y);

        if (horizontal)
        {
            if (direction.X > 0)
                NavigateRight();
            else
                NavigateLeft();
        }
        else
        {
            if (direction.Y < 0) // In 3D, -Y is up
                NavigateUp();
            else
                NavigateDown();
        }
    }

    /// <summary>
    /// Navigates to the next element to the right.
    /// </summary>
    private void NavigateRight()
    {
        switch ((FocusLayer)_currentLayer)
        {
            case FocusLayer.HandCards:
                NavigateInList(_handCards, 1);
                break;
            case FocusLayer.PlayZones:
                NavigateInList(_playZones.Cast<Node3D>().ToList(), 1);
                break;
        }
    }

    /// <summary>
    /// Navigates to the next element to the left.
    /// </summary>
    private void NavigateLeft()
    {
        switch ((FocusLayer)_currentLayer)
        {
            case FocusLayer.HandCards:
                NavigateInList(_handCards, -1);
                break;
            case FocusLayer.PlayZones:
                NavigateInList(_playZones.Cast<Node3D>().ToList(), -1);
                break;
        }
    }

    /// <summary>
    /// Navigates up (from hand to play zones).
    /// </summary>
    private void NavigateUp()
    {
        if (_currentLayer == (int)FocusLayer.HandCards && _playZones.Count > 0)
        {
            SetFocusLayer(FocusLayer.PlayZones, 0);
        }
    }

    /// <summary>
    /// Navigates down (from play zones to hand).
    /// </summary>
    private void NavigateDown()
    {
        if (_currentLayer == (int)FocusLayer.PlayZones && _handCards.Count > 0)
        {
            SetFocusLayer(FocusLayer.HandCards, 0);
        }
    }

    /// <summary>
    /// Navigates within a list by delta.
    /// </summary>
    private void NavigateInList(List<Node3D> list, int delta)
    {
        if (list.Count == 0)
            return;

        int newIndex = _currentIndex + delta;

        // Wrap around
        if (newIndex < 0)
            newIndex = list.Count - 1;
        if (newIndex >= list.Count)
            newIndex = 0;

        SetFocus(list[newIndex], newIndex);
    }

    /// <summary>
    /// Sets focus to a specific element.
    /// </summary>
    public void SetFocus(Node3D element, int index = -1)
    {
        if (_currentFocus == element)
            return;

        // Check if we're focusing a different card while another is selected
        if (_handCards.Contains(element))
        {
            var newCardVisual = element.GetNodeOrNull<CardVisualState>("CardVisualState");

            // If focusing a different card than the selected one, deselect the selected card
            if (
                newCardVisual != null
                && newCardVisual.CurrentState != CardVisualState.State.Selected
            )
            {
                // Find and deselect any selected card
                foreach (var card in _handCards)
                {
                    var cardVisual = card.GetNodeOrNull<CardVisualState>("CardVisualState");
                    if (
                        cardVisual != null
                        && cardVisual.CurrentState == CardVisualState.State.Selected
                    )
                    {
                        cardVisual.SetSelected(false);
                        UnhighlightAllZones();
                        GD.Print("[FocusNavigator] Deselected previous card");
                        break;
                    }
                }
            }
        }

        // Handle zone-to-zone transition specially
        if (element is PlayZoneHighlighter newZone && _currentZoneHighlighter != null)
        {
            // Switching between zones - dim old, highlight new
            _currentZoneHighlighter.Dim();
            _currentZoneHighlighter = null;

            _currentFocus = element;
            _currentIndex = index;
            _currentLayer = (int)FocusLayer.PlayZones;
            _currentZoneHighlighter = newZone;
            _currentZoneHighlighter.Highlight();

            EmitSignal(SignalName.FocusChanged, element);
            GD.Print($"[FocusNavigator] Focused zone: {element.Name}");
            return;
        }

        // Clear previous focus for non-zone transitions
        ClearFocus();

        // Set new focus
        _currentFocus = element;
        _currentIndex = index;

        // Update layer based on element type
        if (_handCards.Contains(element))
        {
            _currentLayer = (int)FocusLayer.HandCards;
            _currentIndex = _handCards.IndexOf(element);
            _currentCardVisual = element.GetNodeOrNull<CardVisualState>("CardVisualState");
            _currentCardVisual?.SetFocus(true);
        }
        else if (element is PlayZoneHighlighter zone)
        {
            _currentLayer = (int)FocusLayer.PlayZones;
            _currentIndex = _playZones.IndexOf(zone);
            _currentZoneHighlighter = zone;
            _currentZoneHighlighter.Highlight();
        }

        EmitSignal(SignalName.FocusChanged, element);

        GD.Print($"[FocusNavigator] Focused: {element.Name}");
    }

    /// <summary>
    /// Sets focus to a specific layer and index.
    /// </summary>
    public void SetFocusLayer(FocusLayer layer, int index)
    {
        _currentLayer = (int)layer;

        switch (layer)
        {
            case FocusLayer.HandCards:
                if (index < _handCards.Count)
                    SetFocus(_handCards[index], index);
                break;
            case FocusLayer.PlayZones:
                if (index < _playZones.Count)
                    SetFocus(_playZones[index], index);
                break;
        }
    }

    /// <summary>
    /// Clears the current focus.
    /// </summary>
    public void ClearFocus()
    {
        if (_currentFocus == null)
            return;

        // Clear visual state
        _currentCardVisual?.SetFocus(false);
        _currentZoneHighlighter?.Unhighlight();

        _currentFocus = null;
        _currentIndex = -1;
        _currentCardVisual = null;
        _currentZoneHighlighter = null;

        EmitSignal(SignalName.FocusCleared);
    }

    /// <summary>
    /// Attempts to focus the first available element.
    /// </summary>
    public void FocusFirst()
    {
        if (_handCards.Count > 0)
        {
            SetFocusLayer(FocusLayer.HandCards, 0);
        }
        else if (_playZones.Count > 0)
        {
            SetFocusLayer(FocusLayer.PlayZones, 0);
        }
    }

    /// <summary>
    /// Gets the focused card visual state (if a card is focused).
    /// </summary>
    public CardVisualState GetFocusedCardVisual()
    {
        return _currentCardVisual;
    }

    /// <summary>
    /// Gets the focused zone (if a zone is focused).
    /// </summary>
    public PlayZoneHighlighter GetFocusedZone()
    {
        return _currentZoneHighlighter;
    }

    /// <summary>
    /// Highlights all valid target zones.
    /// </summary>
    public void HighlightValidTargets(Func<PlayZoneHighlighter, bool> isValid)
    {
        foreach (var zone in _playZones)
        {
            if (isValid(zone))
            {
                zone.Highlight(valid: true);
            }
            else
            {
                zone.Highlight(valid: false);
            }
        }
    }

    /// <summary>
    /// Unhighlights all zones.
    /// </summary>
    public void UnhighlightAllZones()
    {
        foreach (var zone in _playZones)
        {
            zone.Unhighlight();
        }
    }

    /// <summary>
    /// Dims all zones (stays visible but at reduced opacity).
    /// Used when hovering off zones.
    /// </summary>
    public void DimAllZones()
    {
        foreach (var zone in _playZones)
        {
            zone.Dim();
        }

        // Clear focus state so hovering same zone again will re-highlight
        if (_currentFocus is PlayZoneHighlighter)
        {
            _currentFocus = null;
            _currentZoneHighlighter = null;
            _currentIndex = -1;
        }
    }
}
