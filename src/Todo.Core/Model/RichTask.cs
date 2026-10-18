using System;
using System.Collections.Generic;

namespace Todo.Core.Model;

/// <summary>
/// Rich "agent to human" task: HTML body + checklist steps (see docs/agent-design.md).
/// </summary>
public class RichTask
{
    public string? Title { get; set; }
    public string? HtmlBody { get; set; }
    public string? PlainNote { get; set; }
    public List<string> Steps { get; set; } = new();
    public DateTime? DueDate { get; set; }
    public DateTime? Reminder { get; set; }
    public bool Important { get; set; }
}

/// <summary>
/// GetSnapshotAsync outcome: a snapshot, or NotFound when the task was deleted (404).
/// </summary>
public class SnapshotResult
{
    public TaskSnapshot? Snapshot { get; set; }
    public bool NotFound { get; set; }
}

/// <summary>
/// Point-in-time snapshot of a task's feedback-relevant surface.
/// </summary>
public class TaskSnapshot
{
    public string? TaskId { get; set; }
    public string? ListId { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool BodyIsHtml { get; set; }
    public string? Status { get; set; }
    public DateTime? Completed { get; set; }
    public DateTime? LastModified { get; set; }
    public List<StepState> Steps { get; set; } = new();

    public class StepState
    {
        public string Name { get; set; } = string.Empty;
        public bool Checked { get; set; }
    }
}

/// <summary>
/// Pure diff between two snapshots — the feedback detector. Unit-testable, no I/O.
/// </summary>
public static class TaskSnapshotDiff
{
    public static List<string> Diff(TaskSnapshot? before, TaskSnapshot after)
    {
        var changes = new List<string>();
        if (before is null)
        {
            changes.Add("new: 任务首次纳入监控");
            return changes;
        }

        if (!string.Equals(Normalize(before.Body), Normalize(after.Body), StringComparison.Ordinal))
            changes.Add("body: 正文/备注被编辑");

        if (!string.Equals(before.Status, after.Status, StringComparison.Ordinal))
            changes.Add($"status: {before.Status} -> {after.Status}");

        if (after.Completed.HasValue && before.Completed != after.Completed)
            changes.Add($"completed: 于 {after.Completed:yyyy-MM-dd HH:mm} 完成");

        var beforeSteps = before.Steps ?? new List<TaskSnapshot.StepState>();
        var afterSteps = after.Steps ?? new List<TaskSnapshot.StepState>();
        var beforeMap = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var s in beforeSteps) beforeMap[s.Name] = s.Checked;

        foreach (var s in afterSteps)
        {
            if (!beforeMap.TryGetValue(s.Name, out var wasChecked))
                changes.Add($"step-added: 新增步骤 \"{s.Name}\"");
            else if (wasChecked != s.Checked)
                changes.Add($"step-checked: \"{s.Name}\" -> {(s.Checked ? "已勾选" : "取消勾选")}");
        }

        return changes;
    }

    /// <summary>Whitespace-insensitive normalization so client re-saving the body alone doesn't count as feedback.</summary>
    public static string? Normalize(string? s)
        => s is null ? null : System.Text.RegularExpressions.Regex.Replace(s, @"\s+", " ").Trim();
}
