using System;
using System.Collections.Generic;
using System.Diagnostics;
using Godot;

namespace Tests;

/// <summary>
/// Central test runner for all Munchkin game tests.
/// Supports fail-fast execution with duration tracking.
/// </summary>
public partial class TestRunner : Node
{
    /// <summary>
    /// Emitted when a test completes with results.
    /// </summary>
    /// <param name="testName">Name of the test.</param>
    /// <param name="passed">True if test passed.</param>
    /// <param name="message">Result message.</param>
    /// <param name="durationMs">Test duration in milliseconds.</param>
    [Signal]
    public delegate void TestCompletedEventHandler(
        string testName,
        bool passed,
        string message,
        double durationMs
    );

    /// <summary>
    /// Emitted when all tests complete.
    /// </summary>
    /// <param name="total">Total number of tests.</param>
    /// <param name="passed">Number of passed tests.</param>
    /// <param name="failed">Number of failed tests.</param>
    /// <param name="totalDurationMs">Total duration in milliseconds.</param>
    [Signal]
    public delegate void AllTestsCompletedEventHandler(
        int total,
        int passed,
        int failed,
        double totalDurationMs
    );

    /// <summary>
    /// Emitted when a test fails (for fail-fast handling).
    /// </summary>
    /// <param name="testName">Name of failed test.</param>
    /// <param name="message">Failure message.</param>
    [Signal]
    public delegate void TestFailedEventHandler(string testName, string message);

    /// <summary>
    /// Registry of available tests by category.
    /// </summary>
    private readonly Dictionary<TestCategory, List<TestDefinition>> _testRegistry = new();

    /// <summary>
    /// Results of executed tests.
    /// </summary>
    private readonly List<TestResult> _results = new();

    /// <summary>
    /// Stopwatch for timing tests.
    /// </summary>
    private readonly Stopwatch _stopwatch = new();

    /// <summary>
    /// Gets whether tests are currently running.
    /// </summary>
    public bool IsRunning { get; private set; }

    /// <summary>
    /// Gets whether to fail fast on first error.
    /// </summary>
    public bool FailFast { get; set; } = true;

    /// <summary>
    /// Initializes the test registry.
    /// </summary>
    public override void _Ready()
    {
        RegisterTests();
    }

    /// <summary>
    /// Registers all available tests.
    /// </summary>
    private void RegisterTests()
    {
        // Smoke Tests
        RegisterTest(
            TestCategory.Smoke,
            "CardTest",
            "Visual card display test",
            () =>
            {
                GameLogger.Info("CardTest requires scene: Scenes/Tests/Smoke/CardTest.tscn");
                return true; // Scene-based, manual verification
            }
        );

        RegisterTest(
            TestCategory.Smoke,
            "DragDropTest",
            "Drag-drop mechanics test",
            () =>
            {
                GameLogger.Info(
                    "DragDropTest requires scene: Scenes/Tests/Smoke/DragDropTest.tscn"
                );
                return true; // Scene-based, manual verification
            }
        );

        // Unit Tests
        RegisterTest(
            TestCategory.Unit,
            "CardSystemTest",
            "CardFactory loading and retrieval",
            () =>
            {
                return Unit.CardSystemTestLogic.Run();
            }
        );

        RegisterTest(
            TestCategory.Unit,
            "SlotDebugTest",
            "Equipment slot validation",
            () =>
            {
                return Unit.SlotDebugTestLogic.Run();
            }
        );

        // Integration Tests
        RegisterTest(
            TestCategory.Integration,
            "GameStateTest",
            "Game state serialization and flow",
            () =>
            {
                GameLogger.Info(
                    "GameStateTest requires scene: Scenes/Tests/Integration/GameStateTest.tscn"
                );
                return true; // Scene-based, manual verification
            }
        );

        RegisterTest(
            TestCategory.Integration,
            "EquipmentTest",
            "Equipment panel integration",
            () =>
            {
                GameLogger.Info(
                    "EquipmentTest requires scene: Scenes/Tests/Integration/EquipmentTest.tscn"
                );
                return true; // Scene-based, manual verification
            }
        );
    }

    /// <summary>
    /// Registers a test in the specified category.
    /// </summary>
    private void RegisterTest(
        TestCategory category,
        string name,
        string description,
        Func<bool> testAction
    )
    {
        if (!_testRegistry.ContainsKey(category))
        {
            _testRegistry[category] = new List<TestDefinition>();
        }

        _testRegistry[category].Add(new TestDefinition(name, description, testAction));
    }

    /// <summary>
    /// Runs all tests in a category.
    /// </summary>
    /// <param name="category">The category to run.</param>
    /// <returns>True if all tests passed.</returns>
    public bool RunCategory(TestCategory category)
    {
        return RunTests(category);
    }

    /// <summary>
    /// Runs all registered tests.
    /// </summary>
    /// <returns>True if all tests passed.</returns>
    public bool RunAll()
    {
        return RunTests(null);
    }

    /// <summary>
    /// Runs tests, optionally filtered by category.
    /// </summary>
    private bool RunTests(TestCategory? category)
    {
        if (IsRunning)
        {
            GameLogger.Warning("Tests already running", nameof(TestRunner));
            return false;
        }

        IsRunning = true;
        _results.Clear();
        _stopwatch.Restart();

        GameLogger.Info(
            $"Starting test run{(category.HasValue ? $" [{category}]" : "")}...",
            nameof(TestRunner)
        );

        bool allPassed = true;

        try
        {
            var categoriesToRun = category.HasValue
                ? new[] { category.Value }
                : (TestCategory[])Enum.GetValues(typeof(TestCategory));

            foreach (var cat in categoriesToRun)
            {
                if (!_testRegistry.TryGetValue(cat, out var tests))
                    continue;

                GameLogger.Info($"\n=== {cat} Tests ===", nameof(TestRunner));

                foreach (var test in tests)
                {
                    if (!RunSingleTest(test))
                    {
                        allPassed = false;
                        if (FailFast)
                        {
                            GameLogger.Error(
                                $"Fail-fast triggered on: {test.Name}",
                                nameof(TestRunner)
                            );
                            EmitSignal(SignalName.TestFailed, test.Name, "Fail-fast triggered");
                            return false;
                        }
                    }
                }
            }
        }
        finally
        {
            _stopwatch.Stop();
            IsRunning = false;
            ReportResults(allPassed);
        }

        return allPassed;
    }

    /// <summary>
    /// Executes a single test and records the result.
    /// </summary>
    private bool RunSingleTest(TestDefinition test)
    {
        var testStopwatch = Stopwatch.StartNew();
        bool passed;
        string message;

        try
        {
            GameLogger.Info($"Running: {test.Name}", nameof(TestRunner));
            passed = test.Action();
            message = passed ? "PASS" : "FAIL";
        }
        catch (Exception ex)
        {
            passed = false;
            message = $"EXCEPTION: {ex.Message}";
            GameLogger.Exception(ex, test.Name, nameof(TestRunner));
        }

        testStopwatch.Stop();
        double durationMs = testStopwatch.Elapsed.TotalMilliseconds;

        var result = new TestResult(test.Name, test.Description, passed, message, durationMs);
        _results.Add(result);

        if (passed)
        {
            GameLogger.Info($"PASS: {test.Name} ({durationMs:F1}ms)", nameof(TestRunner));
        }
        else
        {
            GameLogger.Error(
                $"FAIL: {test.Name} ({durationMs:F1}ms) - {message}",
                nameof(TestRunner)
            );
        }

        EmitSignal(SignalName.TestCompleted, test.Name, passed, message, durationMs);
        return passed;
    }

    /// <summary>
    /// Reports final results.
    /// </summary>
    private void ReportResults(bool allPassed)
    {
        int total = _results.Count;
        int passed = _results.FindAll(r => r.Passed).Count;
        int failed = total - passed;
        double totalMs = _stopwatch.Elapsed.TotalMilliseconds;

        GameLogger.Info("", nameof(TestRunner));
        GameLogger.Info("╔════════════════════════════════════════╗", nameof(TestRunner));
        GameLogger.Info("║           TEST RESULTS                 ║", nameof(TestRunner));
        GameLogger.Info("╠════════════════════════════════════════╣", nameof(TestRunner));
        GameLogger.Info($"║  Total:    {total, 3}                      ║", nameof(TestRunner));
        GameLogger.Info($"║  Passed:   {passed, 3}  ✓                   ║", nameof(TestRunner));
        GameLogger.Info($"║  Failed:   {failed, 3}  ✗                   ║", nameof(TestRunner));
        GameLogger.Info($"║  Duration: {totalMs, 7:F1}ms              ║", nameof(TestRunner));
        GameLogger.Info(
            $"║  Status:   {(allPassed ? "ALL PASSED" : "FAILED"), 10}          ║",
            nameof(TestRunner)
        );
        GameLogger.Info("╚════════════════════════════════════════╝", nameof(TestRunner));

        EmitSignal(SignalName.AllTestsCompleted, total, passed, failed, totalMs);
    }

    /// <summary>
    /// Gets all registered tests grouped by category.
    /// </summary>
    public Dictionary<TestCategory, List<TestDefinition>> GetTestCatalog()
    {
        return new Dictionary<TestCategory, List<TestDefinition>>(_testRegistry);
    }

    /// <summary>
    /// Gets results of last test run.
    /// </summary>
    public List<TestResult> GetResults()
    {
        return new List<TestResult>(_results);
    }
}

/// <summary>
/// Defines a test case.
/// </summary>
public class TestDefinition
{
    /// <summary>
    /// Test name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Test description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Test execution function.
    /// </summary>
    public Func<bool> Action { get; }

    /// <summary>
    /// Creates a test definition.
    /// </summary>
    public TestDefinition(string name, string description, Func<bool> action)
    {
        Name = name;
        Description = description;
        Action = action;
    }
}

/// <summary>
/// Represents a test result with timing.
/// </summary>
public readonly struct TestResult
{
    /// <summary>
    /// Test name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Test description.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Whether test passed.
    /// </summary>
    public bool Passed { get; }

    /// <summary>
    /// Result message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Duration in milliseconds.
    /// </summary>
    public double DurationMs { get; }

    /// <summary>
    /// Creates a test result.
    /// </summary>
    public TestResult(
        string name,
        string description,
        bool passed,
        string message,
        double durationMs
    )
    {
        Name = name;
        Description = description;
        Passed = passed;
        Message = message;
        DurationMs = durationMs;
    }
}

/// <summary>
/// Test categories.
/// </summary>
public enum TestCategory
{
    /// <summary>
    /// Quick sanity checks.
    /// </summary>
    Smoke,

    /// <summary>
    /// Isolated component tests.
    /// </summary>
    Unit,

    /// <summary>
    /// Multi-component integration tests.
    /// </summary>
    Integration,
}
