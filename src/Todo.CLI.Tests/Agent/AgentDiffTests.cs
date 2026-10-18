using System;
using System.Collections.Generic;
using System.Linq;
using Todo.CLI.Handlers;
using Todo.Core.Model;
using Xunit;

namespace Todo.CLI.Tests.Agent;

public class AgentDiffTests
{
    private static TaskSnapshot Snap(string? body, string status, params (string name, bool check)[] steps)
    {
        return new TaskSnapshot
        {
            TaskId = "t1", ListId = "l1", Title = "T", Body = body, Status = status,
            Steps = steps.Select(s => new TaskSnapshot.StepState { Name = s.name, Checked = s.check }).ToList()
        };
    }

    [Fact]
    public void Seed_snapshot_reports_new_but_nothing_else()
    {
        var changes = TaskSnapshotDiff.Diff(null, Snap("hi", "notStarted", ("A", false)));
        Assert.Contains(changes, c => c.StartsWith("new:"));
        Assert.Equal(1, changes.Count);
    }

    [Fact]
    public void No_change_reports_nothing()
    {
        var before = Snap("<p>hi</p>", "notStarted", ("A", false));
        var after = Snap("<p>hi</p>", "notStarted", ("A", false));
        Assert.Empty(TaskSnapshotDiff.Diff(before, after));
    }

    [Fact]
    public void Body_edit_is_feedback()
    {
        var before = Snap("Original", "notStarted", ("A", false));
        var after = Snap("Original + I agree with plan A", "notStarted", ("A", false));
        var changes = TaskSnapshotDiff.Diff(before, after);
        Assert.Contains(changes, c => c.StartsWith("body:"));
    }

    [Fact]
    public void Whitespace_only_body_change_is_not_feedback()
    {
        var before = Snap("line1\nline2", "notStarted", ("A", false));
        var after = Snap("line1 line2", "notStarted", ("A", false));
        Assert.Empty(TaskSnapshotDiff.Diff(before, after));
    }

    [Fact]
    public void Step_check_is_feedback()
    {
        var before = Snap("x", "notStarted", ("PlanA", false), ("PlanB", false));
        var after = Snap("x", "notStarted", ("PlanA", true), ("PlanB", false));
        var changes = TaskSnapshotDiff.Diff(before, after);
        Assert.Contains(changes, c => c.Contains("PlanA") && c.Contains("已勾选"));
    }

    [Fact]
    public void Step_added_by_human_is_feedback()
    {
        var before = Snap("x", "notStarted", ("A", false));
        var after = Snap("x", "notStarted", ("A", false), ("extra step", false));
        var changes = TaskSnapshotDiff.Diff(before, after);
        Assert.Contains(changes, c => c.StartsWith("step-added:"));
    }

    [Fact]
    public void Status_change_is_feedback()
    {
        var before = Snap("x", "notStarted", ("A", false));
        var after = Snap("x", "completed", ("A", false));
        var changes = TaskSnapshotDiff.Diff(before, after);
        Assert.Contains(changes, c => c.StartsWith("status:"));
    }
}

public class PreviewTextTests
{
    [Theory]
    [InlineData("<h2>决策</h2><p>选 A 还是 B</p>", "决策 选 A 还是 B")]
    [InlineData("plain text", "plain text")]
    [InlineData(null, "(空)")]
    [InlineData("", "(空)")]
    public void Strips_Tags_And_Normalizes(string? body, string expected)
    {
        var got = AgentCommandHandler.PreviewText(body);
        if (body is null or "")
            Assert.Equal(expected, got);
        else
            Assert.StartsWith(expected.Split(' ')[0], got);
        Assert.DoesNotContain("<", got, StringComparison.Ordinal);
    }

    [Fact]
    public void Long_Body_Is_Truncated()
    {
        var longBody = new string('字', 500);
        var got = AgentCommandHandler.PreviewText(longBody, max: 100);
        Assert.EndsWith("…", got);
        Assert.True(got.Length <= 102);
    }
}
