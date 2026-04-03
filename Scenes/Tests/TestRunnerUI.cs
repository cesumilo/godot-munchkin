using System.Collections.Generic;
using Godot;

namespace Tests;

/// <summary>
/// UI controller for the test runner.
/// Organized by category with colored visual feedback.
/// </summary>
public partial class TestRunnerUI : Control
{
    [Export]
    private VBoxContainer _smokeContainer;

    [Export]
    private VBoxContainer _unitContainer;

    [Export]
    private VBoxContainer _integrationContainer;

    [Export]
    private Button _runAllButton;

    [Export]
    private Button _runSmokeButton;

    [Export]
    private Button _runUnitButton;

    [Export]
    private Button _runIntegrationButton;

    [Export]
    private Label _summaryLabel;

    [Export]
    private Label _statusLabel;

    [Export]
    private RichTextLabel _outputLog;

    private TestRunner _testRunner;
    private readonly Dictionary<string, Control> _testControls = new();

    /// <summary>
    /// Colors for test states.
    /// </summary>
    private static readonly Color ColorPending = new Color(0.5f, 0.5f, 0.5f); // Gray
    private static readonly Color ColorRunning = new Color(1f, 0.8f, 0.2f); // Yellow
    private static readonly Color ColorPassed = new Color(0.2f, 0.8f, 0.2f); // Green
    private static readonly Color ColorFailed = new Color(0.9f, 0.2f, 0.2f); // Red

    /// <summary>
    /// Initializes UI and creates test runner.
    /// </summary>
    public override void _Ready()
    {
        _testRunner = new TestRunner();
        AddChild(_testRunner);

        _testRunner.TestCompleted += OnTestCompleted;
        _testRunner.AllTestsCompleted += OnAllTestsCompleted;

        InitializeUI();
    }

    /// <summary>
    /// Sets up UI components.
    /// </summary>
    private void InitializeUI()
    {
        // Connect buttons
        _runAllButton?.Connect(Button.SignalName.Pressed, Callable.From(RunAllTests));
        _runSmokeButton?.Connect(
            Button.SignalName.Pressed,
            Callable.From(() => RunCategory(TestCategory.Smoke))
        );
        _runUnitButton?.Connect(
            Button.SignalName.Pressed,
            Callable.From(() => RunCategory(TestCategory.Unit))
        );
        _runIntegrationButton?.Connect(
            Button.SignalName.Pressed,
            Callable.From(() => RunCategory(TestCategory.Integration))
        );

        // Build test list
        BuildTestList();

        UpdateStatus("Ready - Select tests to run");
    }

    /// <summary>
    /// Builds the test list UI organized by category.
    /// </summary>
    private void BuildTestList()
    {
        var catalog = _testRunner.GetTestCatalog();

        foreach (var category in catalog)
        {
            var container = GetContainerForCategory(category.Key);
            if (container == null)
                continue;

            // Clear existing
            foreach (var child in container.GetChildren())
            {
                if (child is Button || child is Label)
                    child.QueueFree();
            }

            // Add category header
            var header = new Label
            {
                Text = $"{category.Key} Tests",
                ThemeTypeVariation = "HeaderLarge",
                CustomMinimumSize = new Vector2(0, 30),
            };
            container.AddChild(header);

            // Add tests
            foreach (var test in category.Value)
            {
                var testRow = CreateTestRow(test.Name, test.Description);
                container.AddChild(testRow);
                _testControls[test.Name] = testRow;
            }
        }
    }

    /// <summary>
    /// Gets the container for a category.
    /// </summary>
    private VBoxContainer GetContainerForCategory(TestCategory category)
    {
        return category switch
        {
            TestCategory.Smoke => _smokeContainer,
            TestCategory.Unit => _unitContainer,
            TestCategory.Integration => _integrationContainer,
            _ => null,
        };
    }

    /// <summary>
    /// Creates a test row UI element.
    /// </summary>
    private Control CreateTestRow(string name, string description)
    {
        var panel = new PanelContainer
        {
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };

        // Style for pending state
        var style = new StyleBoxFlat
        {
            BgColor = ColorPending,
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var hbox = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        panel.AddChild(hbox);

        // Status indicator
        var statusLabel = new Label
        {
            Text = "⏸",
            CustomMinimumSize = new Vector2(30, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Name = "StatusLabel",
        };
        hbox.AddChild(statusLabel);

        // Test name
        var nameLabel = new Label { Text = name, CustomMinimumSize = new Vector2(150, 0) };
        hbox.AddChild(nameLabel);

        // Description
        var descLabel = new Label
        {
            Text = description,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            Modulate = new Color(0.7f, 0.7f, 0.7f),
        };
        hbox.AddChild(descLabel);

        // Duration (hidden initially)
        var durationLabel = new Label
        {
            Text = "",
            CustomMinimumSize = new Vector2(80, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            Name = "DurationLabel",
        };
        hbox.AddChild(durationLabel);

        return panel;
    }

    /// <summary>
    /// Updates visual state of a test row.
    /// </summary>
    private void UpdateTestRow(
        string testName,
        bool running,
        bool? passed = null,
        double? durationMs = null
    )
    {
        if (!_testControls.TryGetValue(testName, out var control))
            return;

        var panel = control as PanelContainer;
        var style = panel.GetThemeStylebox("panel") as StyleBoxFlat ?? new StyleBoxFlat();

        string statusIcon;
        Color color;

        if (running)
        {
            statusIcon = "▶";
            color = ColorRunning;
        }
        else if (passed.HasValue)
        {
            if (passed.Value)
            {
                statusIcon = "✓";
                color = ColorPassed;
            }
            else
            {
                statusIcon = "✗";
                color = ColorFailed;
            }
        }
        else
        {
            statusIcon = "⏸";
            color = ColorPending;
        }

        style.BgColor = color;
        panel.AddThemeStyleboxOverride("panel", style);

        var statusLabel = control.GetNodeOrNull<Label>("HBoxContainer/StatusLabel");
        if (statusLabel != null)
            statusLabel.Text = statusIcon;

        if (durationMs.HasValue)
        {
            var durationLabel = control.GetNodeOrNull<Label>("HBoxContainer/DurationLabel");
            if (durationLabel != null)
                durationLabel.Text = $"{durationMs.Value:F1}ms";
        }
    }

    /// <summary>
    /// Runs all tests.
    /// </summary>
    private void RunAllTests()
    {
        ResetAllTests();
        UpdateStatus("Running all tests...");
        _testRunner.RunAll();
    }

    /// <summary>
    /// Runs tests in a category.
    /// </summary>
    private void RunCategory(TestCategory category)
    {
        ResetCategory(category);
        UpdateStatus($"Running {category} tests...");
        _testRunner.RunCategory(category);
    }

    /// <summary>
    /// Resets all test visuals.
    /// </summary>
    private void ResetAllTests()
    {
        foreach (var testName in _testControls.Keys)
        {
            UpdateTestRow(testName, false);
        }
        _outputLog.Clear();
    }

    /// <summary>
    /// Resets tests in a category.
    /// </summary>
    private void ResetCategory(TestCategory category)
    {
        var catalog = _testRunner.GetTestCatalog();
        if (catalog.TryGetValue(category, out var tests))
        {
            foreach (var test in tests)
            {
                UpdateTestRow(test.Name, false);
            }
        }
    }

    /// <summary>
    /// Handles test completion event.
    /// </summary>
    private void OnTestCompleted(string testName, bool passed, string message, double durationMs)
    {
        UpdateTestRow(testName, false, passed, durationMs);

        var logEntry =
            $"[{testName}] {(passed ? "PASS" : "FAIL")} ({durationMs:F1}ms): {message}\n";
        _outputLog.AppendText(logEntry);

        if (!passed && _testRunner.FailFast)
        {
            UpdateStatus($"Stopped at first failure: {testName}");
        }
    }

    /// <summary>
    /// Handles all tests completion.
    /// </summary>
    private void OnAllTestsCompleted(int total, int passed, int failed, double totalDurationMs)
    {
        var status = failed == 0 ? "✓ ALL PASSED" : $"✗ {failed} FAILED";
        UpdateStatus($"{status} - {total} tests in {totalDurationMs:F1}ms");

        _summaryLabel.Text =
            $"Total: {total} | Passed: {passed} | Failed: {failed} | Time: {totalDurationMs:F1}ms";
    }

    /// <summary>
    /// Updates status label.
    /// </summary>
    private void UpdateStatus(string message)
    {
        if (_statusLabel != null)
            _statusLabel.Text = message;
    }

    /// <summary>
    /// Cleanup on exit.
    /// </summary>
    public override void _ExitTree()
    {
        _testRunner?.QueueFree();
    }
}
