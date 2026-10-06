using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AdNoctem.Substrate.Windows;

/// <summary>A caller-selected requirement and discovery operation. Evaluating it never remedies a failure.</summary>
public sealed class PrerequisiteRequirement
{
    public string Check { get; }
    public string Target { get; }
    public object? Expected { get; }
    public string Guidance { get; }
    internal Func<object?> Read { get; }
    internal Func<object?, bool> Test { get; }

    public PrerequisiteRequirement(
        string check,
        string target,
        object? expected,
        Func<object?> read,
        Func<object?, bool> test,
        string guidance
    )
    {
        Check = check ?? throw new ArgumentNullException(nameof(check));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Expected = expected;
        Read = read ?? throw new ArgumentNullException(nameof(read));
        Test = test ?? throw new ArgumentNullException(nameof(test));
        Guidance = guidance ?? throw new ArgumentNullException(nameof(guidance));
    }
}

/// <summary>The observed result of one prerequisite, including unmet or unavailable evidence.</summary>
public sealed class PrerequisiteCheck
{
    public string Check { get; }
    public string Target { get; }
    public object? Expected { get; }
    public object? Actual { get; }
    public bool Satisfied { get; }
    public string Reason { get; }
    public string? Guidance { get; }

    internal PrerequisiteCheck(PrerequisiteRequirement requirement)
    {
        Check = requirement.Check;
        Target = requirement.Target;
        Expected = requirement.Expected;

        try
        {
            Actual = requirement.Read();
            Satisfied = requirement.Test(Actual);
            Reason = Satisfied ? "Satisfied" : "RequirementNotMet";
            Guidance = Satisfied ? null : requirement.Guidance;
        }
        catch (Exception error) when (!(error is OperationCanceledException))
        {
            Reason = "DiscoveryFailed";
            Guidance = error.Message;
        }
    }
}

/// <summary>Combined prerequisite observations without implicit installation or elevation.</summary>
public sealed class PrerequisiteReport
{
    public IReadOnlyList<PrerequisiteCheck> Checks { get; }
    public bool Applicable => Checks.All(check => check.Satisfied);

    internal PrerequisiteReport(PrerequisiteCheck[] checks) => Checks = Array.AsReadOnly(checks);
}

/// <summary>Evaluates every supplied requirement, retaining discovery failures without hiding other checks.</summary>
public sealed class PrerequisiteEvaluator
{
    public PrerequisiteReport Evaluate(
        IEnumerable<PrerequisiteRequirement> requirements,
        CancellationToken cancellationToken = default
    )
    {
        if (requirements == null)
            throw new ArgumentNullException(nameof(requirements));

        var checks = new List<PrerequisiteCheck>();

        foreach (var requirement in requirements)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checks.Add(
                new PrerequisiteCheck(
                    requirement
                        ?? throw new ArgumentException(
                            "Requirements cannot contain null.",
                            nameof(requirements)
                        )
                )
            );
        }

        return new PrerequisiteReport(checks.ToArray());
    }
}
