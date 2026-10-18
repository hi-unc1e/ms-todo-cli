using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Graph;
using Microsoft.Graph.Models;
using Microsoft.Kiota.Abstractions.Authentication;
using Todo.Core.Model;

namespace Todo.Core.Repository;

public interface IAgentTaskRepository
{
    Task<(string TaskId, string ListId, string ListName)> CreateAsync(RichTask task, string? listName);
    Task<SnapshotResult> GetSnapshotAsync(string listId, string taskId);
    Task<(string TaskId, string ListId, string ListName)?> FindByTitleAsync(string title, string? listName);
    Task<(string TaskId, string ListId, string ListName)?> FindByIdAsync(string taskId);
}

/// <summary>
/// Rich task creation + feedback read-back for the agent subsystem (docs/agent-design.md).
/// Additive to TodoItemRepository — existing commands untouched.
/// </summary>
public class AgentTaskRepository : RepositoryBase, IAgentTaskRepository
{
    public AgentTaskRepository(IAuthenticationProvider authenticationProvider) : base(authenticationProvider) { }

    public async Task<(string TaskId, string ListId, string ListName)> CreateAsync(RichTask task, string? listName)
    {
        var graphServiceClient = new GraphServiceClient(AuthenticationProvider);
        var list = await ResolveListAsync(graphServiceClient, listName);

        var newTask = new TodoTask
        {
            Title = task.Title,
            Importance = task.Important ? Importance.High : Importance.Normal
        };

        if (!string.IsNullOrWhiteSpace(task.HtmlBody))
        {
            newTask.Body = new ItemBody { Content = task.HtmlBody, ContentType = BodyType.Html };
        }
        else if (!string.IsNullOrWhiteSpace(task.PlainNote))
        {
            newTask.Body = new ItemBody { Content = task.PlainNote, ContentType = BodyType.Text };
        }

        if (task.Steps.Count > 0)
        {
            newTask.ChecklistItems = task.Steps.Select(s => new ChecklistItem { DisplayName = s }).ToList();
        }

        var tz = GraphTimeZone.GetLocalIanaId();
        if (task.DueDate.HasValue)
        {
            newTask.DueDateTime = new DateTimeTimeZone { DateTime = task.DueDate.Value.ToString("yyyy-MM-ddTHH:mm:ss"), TimeZone = tz };
        }
        if (task.Reminder.HasValue)
        {
            newTask.IsReminderOn = true;
            newTask.ReminderDateTime = new DateTimeTimeZone { DateTime = task.Reminder.Value.ToString("yyyy-MM-ddTHH:mm:ss"), TimeZone = tz };
        }

        var created = await graphServiceClient.Me.Todo.Lists[list.Id].Tasks.PostAsync(newTask);
        return (created.Id, list.Id, list.DisplayName);
    }

    public async Task<SnapshotResult> GetSnapshotAsync(string listId, string taskId)
    {
        var graphServiceClient = new GraphServiceClient(AuthenticationProvider);
        try
        {
            var task = await graphServiceClient.Me.Todo.Lists[listId].Tasks[taskId].GetAsync();
            if (task is null) return new SnapshotResult { NotFound = true };
            return new SnapshotResult { Snapshot = Map(task, listId) };
        }
        catch (Microsoft.Kiota.Abstractions.ApiException aex) when (aex.ResponseStatusCode == 404)
        {
            return new SnapshotResult { NotFound = true };
        }
    }

    internal static TaskSnapshot Map(Microsoft.Graph.Models.TodoTask task, string listId)
    {

        return new TaskSnapshot
        {
            TaskId = task.Id,
            ListId = listId,
            Title = task.Title,
            Body = task.Body?.Content,
            BodyIsHtml = task.Body?.ContentType == BodyType.Html,
            Status = task.Status?.ToString(),
            Completed = task.CompletedDateTime?.DateTime is string c && DateTime.TryParse(c, out var completed) ? completed : null,
            LastModified = task.LastModifiedDateTime?.DateTime,
            Steps = (task.ChecklistItems ?? Enumerable.Empty<ChecklistItem>())
                .Select(ci => new TaskSnapshot.StepState { Name = ci.DisplayName ?? string.Empty, Checked = ci.IsChecked ?? false })
                .ToList()
        };
    }

    public async Task<(string TaskId, string ListId, string ListName)?> FindByTitleAsync(string title, string? listName)
    {
        var graphServiceClient = new GraphServiceClient(AuthenticationProvider);
        var lists = await graphServiceClient.Me.Todo.Lists.GetAsync();
        if (lists?.Value is null) return null;

        var candidates = string.IsNullOrWhiteSpace(listName)
            ? lists.Value.Where(l => l.WellknownListName == Microsoft.Graph.Models.WellknownListName.DefaultList)
            : lists.Value.Where(l => string.Equals(l.DisplayName, listName, StringComparison.Ordinal));

        foreach (var list in candidates)
        {
            var page = await graphServiceClient.Me.Todo.Lists[list.Id].Tasks.GetAsync();
            while (page != null)
            {
                var hit = page.Value?.FirstOrDefault(t => string.Equals(t.Title, title, StringComparison.Ordinal));
                if (hit != null) return (hit.Id, list.Id, list.DisplayName);
                page = page.OdataNextLink is null ? null
                    : await graphServiceClient.Me.Todo.Lists[list.Id].Tasks.WithUrl(page.OdataNextLink).GetAsync();
            }
        }
        return null;
    }

    public async Task<(string TaskId, string ListId, string ListName)?> FindByIdAsync(string taskId)
    {
        var graphServiceClient = new GraphServiceClient(AuthenticationProvider);
        var lists = await graphServiceClient.Me.Todo.Lists.GetAsync();
        if (lists?.Value is null) return null;
        foreach (var list in lists.Value)
        {
            try
            {
                var task = await graphServiceClient.Me.Todo.Lists[list.Id].Tasks[taskId].GetAsync();
                if (task != null) return (task.Id, list.Id, list.DisplayName ?? string.Empty);
            }
            catch
            {
                // not in this list — keep scanning
            }
        }
        return null;
    }

    private static async Task<Microsoft.Graph.Models.TodoTaskList> ResolveListAsync(GraphServiceClient client, string? listName)
    {
        var lists = await client.Me.Todo.Lists.GetAsync();
        var target = string.IsNullOrWhiteSpace(listName)
            ? lists?.Value?.FirstOrDefault(l => l.WellknownListName == Microsoft.Graph.Models.WellknownListName.DefaultList)
            : lists?.Value?.FirstOrDefault(l => string.Equals(l.DisplayName, listName, StringComparison.Ordinal));
        return target ?? throw new InvalidOperationException(string.IsNullOrWhiteSpace(listName)
            ? "Default list (Tasks) not found."
            : $"No list found with the name '{listName}'.");
    }
}
