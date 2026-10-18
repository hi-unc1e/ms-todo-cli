using System;
using System.CommandLine;
using Todo.CLI.Handlers;

namespace Todo.CLI.Commands;

/// <summary>
/// Agent subsystem (v0.1.0, see docs/agent-design.md): rich decision-request tasks
/// and the human feedback loop (body edits / checklist checks), read back by
/// adaptive polling. Additive to the classic commands.
/// </summary>
public class AgentCommand : Command
{
    public static readonly string AgentVersion = Agent.AgentPolling.Version;

    public AgentCommand(IServiceProvider serviceProvider) : base("agent",
        $"Agent feedback subsystem v{AgentVersion}: send rich decision requests and poll for human feedback.")
    {
        var listOption = new Option<string>("--list", "Target list name (default: the built-in Tasks list).");

        // ---- send ----
        var send = new Command("send", "Send a rich decision-request task (HTML body + checklist steps + reminder).");
        var subjectArg = new Argument<string>("subject", "Task title, e.g. \"[Agent] 请选择部署方案\"");
        var htmlFile = new Option<string>("--html-file", "Path to a UTF-8 HTML file used as the task body (limited client subset: headings/lists/bold/links/code).");
        var note = new Option<string>("--note", "Plain-text body (ignored when --html-file is given).");
        var step = new Option<string[]>("--step", "A checklist step; repeat for multiple options. Human checks one to confirm.")
        {
            AllowMultipleArgumentsPerToken = true
        };
        var due = new Option<string>("--due-date", "Due date (yyyy-MM-dd or MM-dd).");
        var remind = new Option<string>("--remind", "Reminder (HH:mm or yyyy-MM-dd HH:mm).");
        var important = new Option<bool>("--important", "Mark as important.");
        send.AddArgument(subjectArg);
        send.AddOption(listOption);
        send.AddOption(htmlFile);
        send.AddOption(note);
        send.AddOption(step);
        send.AddOption(due);
        send.AddOption(remind);
        send.AddOption(important);
        send.SetHandler(AgentCommandHandler.Send.Create(serviceProvider),
            subjectArg, listOption, htmlFile, note, step, due, remind, important);

        // ---- poll ----
        var poll = new Command("poll", "Check once for human feedback on a task; prints a diff. Exit 0 = feedback found.");
        var pollTaskId = new Option<string>("--task-id", "The task id to poll.");
        var pollTitle = new Option<string>("--title", "Resolve the task by exact title instead of id.");
        var pollJson = new Option<bool>("--json", "Machine-readable JSON output.");
        poll.AddOption(pollTaskId);
        poll.AddOption(pollTitle);
        poll.AddOption(listOption);
        poll.AddOption(pollJson);
        poll.SetHandler(AgentCommandHandler.Poll.Create(serviceProvider),
            pollTaskId, pollTitle, listOption, pollJson);

        // ---- watch ----
        var watch = new Command("watch", "Adaptive polling loop (5min x first hour -> hourly x 24h -> daily) until feedback or timeout.");
        var watchTaskId = new Option<string>("--task-id", "The task id to watch.");
        var watchTitle = new Option<string>("--title", "Resolve the task by exact title instead of id.");
        var watchJson = new Option<bool>("--json", "Machine-readable JSON output.");
        var timeout = new Option<int>("--timeout", "Give up after this many minutes (default: 4320 = 3 days).");
        timeout.SetDefaultValue(4320);
        var maxPolls = new Option<int>("--max-polls", "Hard cap on poll count (default: 64).");
        maxPolls.SetDefaultValue(64);
        watch.AddOption(watchTaskId);
        watch.AddOption(watchTitle);
        watch.AddOption(listOption);
        watch.AddOption(watchJson);
        watch.AddOption(timeout);
        watch.AddOption(maxPolls);
        watch.SetHandler(AgentCommandHandler.Watch.Create(serviceProvider),
            watchTaskId, watchTitle, listOption, watchJson, timeout, maxPolls);

        Add(send);
        Add(poll);
        Add(watch);
    }
}
