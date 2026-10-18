using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Todo.CLI.Agent;

/// <summary>
/// Persisted watch state, keyed by task id, at ~/.todo-cli/agent-watch.json.
/// Survives across poll/watch invocations so the adaptive schedule keeps its phase.
/// </summary>
public class AgentWatchEntry
{
    public string TaskId { get; set; } = string.Empty;
    public string ListId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? LastBody { get; set; }
    public Dictionary<string, bool> LastChecklist { get; set; } = new();
    public string? LastStatus { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int PollCount { get; set; }
}

public static class AgentWatchStore
{
    private static readonly string Path =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".todo-cli", "agent-watch.json");
    private static readonly object Lock = new();

    public static Dictionary<string, AgentWatchEntry> Load()
    {
        lock (Lock)
        {
            try
            {
                if (!File.Exists(Path)) return new Dictionary<string, AgentWatchEntry>();
                return JsonSerializer.Deserialize(File.ReadAllText(Path), AgentWatchJsonContext.Default.DictionaryStringAgentWatchEntry)
                       ?? new Dictionary<string, AgentWatchEntry>();
            }
            catch
            {
                return new Dictionary<string, AgentWatchEntry>();
            }
        }
    }

    public static void Save(Dictionary<string, AgentWatchEntry> states)
    {
        lock (Lock)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, JsonSerializer.Serialize(states, typeof(Dictionary<string, AgentWatchEntry>),
                new JsonSerializerOptions { WriteIndented = true, TypeInfoResolver = AgentWatchJsonContext.Default }));
        }
    }
}

[JsonSerializable(typeof(Dictionary<string, AgentWatchEntry>))]
internal partial class AgentWatchJsonContext : JsonSerializerContext
{
}
