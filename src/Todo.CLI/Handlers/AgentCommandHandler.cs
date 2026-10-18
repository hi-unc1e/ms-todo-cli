using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Todo.Core.Model;
using Todo.Core.Repository;
using Microsoft.Extensions.DependencyInjection;
using Todo.CLI.Agent;
using Todo.CLI.UI;

namespace Todo.CLI.Handlers;

/// <summary>
/// Handlers for the agent feedback subsystem (docs/agent-design.md).
/// Feedback = human edits the body, checks/adds checklist steps, or completes the task.
/// First poll/watch seeds the baseline and never reports it as feedback.
/// </summary>
public class AgentCommandHandler
{
    private readonly IAgentTaskRepository _agentTaskRepository;
    private readonly IUserInteraction _userInteraction;

    private AgentCommandHandler(IAgentTaskRepository agentTaskRepository, IUserInteraction userInteraction)
    {
        _agentTaskRepository = agentTaskRepository;
        _userInteraction = userInteraction;
    }

    private static AgentWatchEntry SeedEntry(TaskSnapshot snap)
    {
        return new AgentWatchEntry
        {
            TaskId = snap.TaskId ?? string.Empty,
            ListId = snap.ListId ?? string.Empty,
            Title = snap.Title,
            LastBody = snap.Body,
            LastStatus = snap.Status,
            LastChecklist = ToMap(snap.Steps),
            FirstSeenAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }

    private static TaskSnapshot SnapshotFromState(AgentWatchEntry e)
    {
        return new TaskSnapshot
        {
            TaskId = e.TaskId,
            ListId = e.ListId,
            Title = e.Title,
            Body = e.LastBody,
            Status = e.LastStatus,
            Steps = ToSteps(e.LastChecklist)
        };
    }

    private static Dictionary<string, bool> ToMap(IEnumerable<TaskSnapshot.StepState> steps)
    {
        var map = new Dictionary<string, bool>(StringComparer.Ordinal);
        foreach (var s in steps) map[s.Name] = s.Checked;
        return map;
    }

    private static List<TaskSnapshot.StepState> ToSteps(Dictionary<string, bool> map)
    {
        var list = new List<TaskSnapshot.StepState>();
        foreach (var kv in map) list.Add(new TaskSnapshot.StepState { Name = kv.Key, Checked = kv.Value });
        return list;
    }

    // Hand-rolled JSON: PublishTrimmed disables reflection-based System.Text.Json,
    // and the payloads here are small with a fixed schema.
    private static string JEsc(string? s)
    {
        if (s is null) return "null";
        var sb = new StringBuilder("\"");
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else sb.Append(ch);
                    break;
            }
        }
        sb.Append('"');
        return sb.ToString();
    }

    private static string JArr(IEnumerable<string> items)
        => "[" + string.Join(",", items.Select(JEsc)) + "]";

    private static string JSteps(IEnumerable<TaskSnapshot.StepState> steps)
        => "[" + string.Join(",", steps.Select(s => $"{{\"name\":{JEsc(s.Name)},\"isChecked\":{(s.Checked ? "true" : "false")}}}")) + "]";

    private static void PrintResult(string taskId, string list, bool hasFeedback, List<string> changes,
        TaskSnapshot now, int polls)
    {
        var sb = new StringBuilder();
        sb.Append("{\n  \"agentVersion\": ").Append(JEsc(AgentPolling.Version));
        sb.Append(",\n  \"taskId\": ").Append(JEsc(taskId));
        sb.Append(",\n  \"list\": ").Append(JEsc(list));
        sb.Append(",\n  \"hasFeedback\": ").Append(hasFeedback ? "true" : "false");
        sb.Append(",\n  \"changes\": ").Append(JArr(changes));
        sb.Append(",\n  \"polls\": ").Append(polls);
        sb.Append(",\n  \"statusNow\": ").Append(JEsc(now.Status));
        sb.Append(",\n  \"bodyNow\": ").Append(JEsc(now.Body));
        sb.Append(",\n  \"checklistNow\": ").Append(JSteps(now.Steps));
        sb.Append("\n}");
        Console.WriteLine(sb.ToString());
    }

    public class Send
    {
        public static Func<string, string?, string?, string?, string[], string?, string?, bool, Task<int>> Create(IServiceProvider serviceProvider)
        {
            var handler = new AgentCommandHandler(
                serviceProvider.GetRequiredService<IAgentTaskRepository>(),
                serviceProvider.GetRequiredService<IUserInteraction>());
            return handler.HandleSendAsync;
        }
    }

    private async Task<int> HandleSendAsync(string subject, string? listName, string? htmlFile, string? note,
        string[] steps, string? dueDateString, string? remindString, bool important)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(subject))
            {
                _userInteraction.ShowError("Subject is required.");
                return 2;
            }

            string? html = null;
            if (!string.IsNullOrWhiteSpace(htmlFile))
            {
                if (!File.Exists(htmlFile))
                {
                    _userInteraction.ShowError($"HTML file not found: {htmlFile}");
                    return 2;
                }
                var fi = new FileInfo(htmlFile);
                if (fi.Length > 256 * 1024)
                {
                    _userInteraction.ShowError($"HTML file too large ({fi.Length / 1024} KB, max 256 KB).");
                    return 2;
                }
                if (fi.Length > 64 * 1024)
                    _userInteraction.ShowSuccess($"Note: HTML body is {fi.Length / 1024} KB; To Do clients may render slowly.");
                html = File.ReadAllText(htmlFile, Encoding.UTF8);
            }
            else if (string.IsNullOrWhiteSpace(note))
            {
                _userInteraction.ShowError("Provide --html-file or --note so the human has context.");
                return 2;
            }

            var dueDate = ParseDate(dueDateString, new[] { "yyyy-MM-dd", "MM-dd" });
            if (dueDateString is not null && dueDate is null)
            {
                _userInteraction.ShowError("Invalid --due-date. Use yyyy-MM-dd or MM-dd.");
                return 2;
            }

            var remindAt = ParseRemind(remindString, dueDate);
            if (remindString is not null && remindAt is null)
            {
                _userInteraction.ShowError("Invalid --remind. Use HH:mm or yyyy-MM-dd HH:mm.");
                return 2;
            }

            var (taskId, listId, listDisplayName) = await _agentTaskRepository.CreateAsync(new RichTask
            {
                Title = subject,
                HtmlBody = html,
                PlainNote = html is null ? note : null,
                Steps = new List<string>(steps ?? Array.Empty<string>()),
                DueDate = dueDate,
                Reminder = remindAt,
                Important = important
            }, listName);

            _userInteraction.ShowSuccess($"Agent task created: '{subject}' @ {listDisplayName} (id {taskId})");

            // Seed the baseline immediately: feedback given between send and the first poll is NOT swallowed.
            try
            {
                var seeded = await _agentTaskRepository.GetSnapshotAsync(listId, taskId);
                if (seeded.Snapshot is not null)
                {
                    var states0 = AgentWatchStore.Load();
                    states0[taskId] = SeedEntry(seeded.Snapshot);
                    AgentWatchStore.Save(states0);
                }
            }
            catch (Exception seedEx)
            {
                _userInteraction.ShowSuccess($"Note: baseline not seeded ({seedEx.Message}); first poll will seed it.");
            }

            if (steps is { Length: > 0 })
                _userInteraction.ShowSuccess("Feedback protocol: human checks a step (or edits the body); poll/watch detects it.");
            _userInteraction.ShowSuccess($"Watch it:  todo agent watch --task-id {taskId}");
            return 0;
        }
        catch (Exception ex)
        {
            _userInteraction.ShowError($"Error sending agent task: {ex.Message}");
            return 2;
        }
    }

    public class Poll
    {
        public static Func<string?, string?, string?, bool, Task<int>> Create(IServiceProvider serviceProvider)
        {
            var handler = new AgentCommandHandler(
                serviceProvider.GetRequiredService<IAgentTaskRepository>(),
                serviceProvider.GetRequiredService<IUserInteraction>());
            return handler.HandlePollAsync;
        }
    }

    public class Watch
    {
        public static Func<string?, string?, string?, bool, int, int, Task<int>> Create(IServiceProvider serviceProvider)
        {
            var handler = new AgentCommandHandler(
                serviceProvider.GetRequiredService<IAgentTaskRepository>(),
                serviceProvider.GetRequiredService<IUserInteraction>());
            return handler.HandleWatchAsync;
        }
    }

    private async Task<int> HandlePollAsync(string? taskId, string? title, string? listName, bool json)
    {
        try
        {
            var resolved = await ResolveAsync(taskId, title, listName);
            if (resolved is null) { _userInteraction.ShowError("Task not found. Use --task-id or --title."); return 2; }
            var (tid, lid, lname) = resolved.Value;

            var result = await _agentTaskRepository.GetSnapshotAsync(lid, tid);
            if (result.NotFound) { CleanupWatched(tid); _userInteraction.ShowError("Task no longer exists (deleted by human). Watch state cleaned."); return 2; }
            var now = result.Snapshot!;
            if (now is null) { _userInteraction.ShowError("Task snapshot unavailable."); return 2; }

            var states = AgentWatchStore.Load();
            var changes = new List<string>();
            if (states.TryGetValue(tid, out var entry))
            {
                changes = TaskSnapshotDiff.Diff(SnapshotFromState(entry), now);
                var updated = SeedEntry(now);
                updated.FirstSeenAt = entry.FirstSeenAt;
                updated.PollCount = entry.PollCount + 1;
                states[tid] = updated;
            }
            else
            {
                changes.Add("seed: 已建立基线，下次 poll 开始检测反馈");
                states[tid] = SeedEntry(now);
            }
            AgentWatchStore.Save(states);

            if (json)
            {
                PrintResult(tid, lname, changes.Count > 0 && !changes[0].StartsWith("seed"), changes, now, 1);
            }
            else if (changes.Count == 0)
            {
                _userInteraction.ShowSuccess("No human feedback yet.");
            }
            else
            {
                foreach (var c in changes) Console.WriteLine("  - " + c);
                if (changes.Any(c => c.StartsWith("body:")))
                    Console.WriteLine("  正文预览: " + PreviewText(now.Body));
            }
            return changes.Count > 0 && !changes[0].StartsWith("seed") ? 0 : 1;
        }
        catch (Exception ex)
        {
            _userInteraction.ShowError($"Error polling: {ex.Message}");
            return 2;
        }
    }

    private async Task<int> HandleWatchAsync(string? taskId, string? title, string? listName, bool json, int timeoutMinutes, int maxPolls)
    {
        try
        {
            var resolved = await ResolveAsync(taskId, title, listName);
            if (resolved is null) { _userInteraction.ShowError("Task not found. Use --task-id or --title."); return 2; }
            var (tid, lid, lname) = resolved.Value;

            var states = AgentWatchStore.Load();
            states.TryGetValue(tid, out var state);
            var deadline = DateTime.Now.AddMinutes(timeoutMinutes);
            var polls = 0;
            var consecutiveFailures = 0;

            while (true)
            {
                polls++;
                Todo.Core.Model.TaskSnapshot? now;
                try
                {
                    var result = await _agentTaskRepository.GetSnapshotAsync(lid, tid);
                    if (result.NotFound) { CleanupWatched(tid); _userInteraction.ShowError("Task no longer exists (deleted by human). Watch state cleaned."); return 2; }
                    now = result.Snapshot;
                    consecutiveFailures = 0;
                }
                catch (Exception ex) when (ex is not OutOfMemoryException)
                {
                    consecutiveFailures++;
                    if (consecutiveFailures >= 3)
                    {
                        _userInteraction.ShowError($"Watch aborted after {consecutiveFailures} consecutive errors: {ex.Message}");
                        return 2;
                    }
                    if (!json) Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] transient error ({ex.Message.GetType().Name}), retrying");
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    continue;
                }

                if (now is null) { _userInteraction.ShowError("Task snapshot unavailable."); return 2; }

                var changes = state is null
                    ? new List<string>()
                    : TaskSnapshotDiff.Diff(SnapshotFromState(state), now);

                var entry = SeedEntry(now);
                if (state is not null)
                {
                    entry.FirstSeenAt = state.FirstSeenAt;
                    entry.PollCount = state.PollCount + 1;
                }
                else
                {
                    entry.PollCount = 1;
                }
                states[tid] = entry;
                AgentWatchStore.Save(states);

                if (changes.Count > 0)
                {
                    if (json)
                        PrintResult(tid, lname, true, changes, now, polls);
                    else
                    {
                        _userInteraction.ShowSuccess($"Human feedback detected after {polls} poll(s):");
                        foreach (var c in changes) Console.WriteLine("  - " + c);
                        if (changes.Any(c => c.StartsWith("body:")))
                            Console.WriteLine("  正文预览: " + PreviewText(now.Body));
                    }
                    return 0;
                }

                if (DateTime.Now >= deadline)
                {
                    if (!json) _userInteraction.ShowSuccess($"No feedback within {timeoutMinutes} min ({polls} polls).");
                    return 1;
                }
                if (polls >= maxPolls)
                {
                    if (!json) _userInteraction.ShowSuccess($"No feedback after {maxPolls} polls.");
                    return 1;
                }

                var delay = AgentPolling.NextDelay(entry.FirstSeenAt, DateTime.Now);
                if (!json) Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] no feedback yet; next poll in {delay.TotalMinutes:0} min");
                await Task.Delay(delay);
            }
        }
        catch (Exception ex)
        {
            _userInteraction.ShowError($"Error watching: {ex.Message}");
            return 2;
        }
    }

    private static void CleanupWatched(string taskId)
    {
        var states = AgentWatchStore.Load();
        if (states.Remove(taskId)) AgentWatchStore.Save(states);
    }

    /// <summary>Strip tags + collapse whitespace for a short human-readable body preview.</summary>
    internal static string PreviewText(string? body, int max = 200)
    {
        if (string.IsNullOrWhiteSpace(body)) return "(空)";
        var text = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>", " ");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length <= max ? text : text[..max] + "…";
    }

    private async Task<(string TaskId, string ListId, string ListName)?> ResolveAsync(string? taskId, string? title, string? listName)
    {
        // Store-first for --task-id: avoids an N-lists scan when we already know the list.
        if (!string.IsNullOrWhiteSpace(taskId))
        {
            var states = AgentWatchStore.Load();
            if (states.TryGetValue(taskId, out var known) && !string.IsNullOrEmpty(known.ListId))
                return (taskId, known.ListId, string.Empty); // list name unknown from store; id-resolved anyway
            return await _agentTaskRepository.FindByIdAsync(taskId);
        }
        if (!string.IsNullOrWhiteSpace(title))
            return await _agentTaskRepository.FindByTitleAsync(title, listName);
        return null;
    }

    private static DateTime? ParseDate(string? s, string[] formats)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        foreach (var f in formats)
            if (DateTime.TryParseExact(s, f, null, System.Globalization.DateTimeStyles.None, out var d))
                return f == "MM-dd" ? new DateTime(DateTime.Now.Year, d.Month, d.Day) : d;
        return null;
    }

    private static DateTime? ParseRemind(string? s, DateTime? due)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (DateTime.TryParseExact(s, "HH:mm", null, System.Globalization.DateTimeStyles.None, out var t))
        {
            var day = due ?? DateTime.Today;
            return new DateTime(day.Year, day.Month, day.Day, t.Hour, t.Minute, 0);
        }
        if (DateTime.TryParseExact(s, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var a)) return a;
        if (DateTime.TryParseExact(s, "yyyy-MM-ddTHH:mm", null, System.Globalization.DateTimeStyles.None, out var b)) return b;
        return null;
    }
}
