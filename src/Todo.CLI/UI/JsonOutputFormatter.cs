using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Todo.Core.Model;

namespace Todo.CLI.UI;

public class JsonOutputFormatter : IOutputFormatter
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public string FormatSuccess(string message)
    {
        var output = new JsonObject
        {
            ["success"] = true,
            ["message"] = message
        };
        var json = output.ToJsonString(Options);
        Console.WriteLine(json);
        return json;
    }

    public string FormatError(string message)
    {
        var output = new JsonObject
        {
            ["success"] = false,
            ["message"] = message
        };
        var json = output.ToJsonString(Options);
        Console.Error.WriteLine(json);
        return json;
    }

    public string FormatList(TodoList list, bool noStatus)
    {
        var tasks = new JsonArray();
        foreach (var task in list.Tasks)
        {
            tasks.Add(FormatItemToJson(task, noStatus));
        }
        var output = new JsonObject
        {
            ["Name"] = list.Name,
            ["Count"] = list.Count,
            ["Tasks"] = tasks
        };
        var json = output.ToJsonString(Options);
        Console.WriteLine(json);
        return json;
    }

    public string FormatItem(TodoItem item, bool noStatus)
    {
        var json = FormatItemToJson(item, noStatus).ToJsonString(Options);
        Console.WriteLine(json);
        return json;
    }

    public string FormatItems(IEnumerable<TodoItem> items, bool noStatus)
    {
        var output = new JsonArray();
        foreach (var item in items)
        {
            output.Add(FormatItemToJson(item, noStatus));
        }
        var json = output.ToJsonString(Options);
        Console.WriteLine(json);
        return json;
    }

    public string FormatLists(IEnumerable<TodoList> lists)
    {
        var output = new JsonArray();
        foreach (var list in lists)
        {
            output.Add(new JsonObject
            {
                ["Id"] = list.Id,
                ["Name"] = list.Name
            });
        }
        var json = output.ToJsonString(Options);
        Console.WriteLine(json);
        return json;
    }

    private static JsonObject FormatItemToJson(TodoItem item, bool noStatus)
    {
        return new JsonObject
        {
            ["Id"] = item.Id,
            ["Subject"] = item.Subject,
            ["IsCompleted"] = item.IsCompleted,
            ["Status"] = item.Status,
            ["Completed"] = item.Completed?.ToString("yyyy-MM-dd"),
            ["DueDate"] = item.DueDate?.ToString("yyyy-MM-dd"),
            ["ListId"] = item.ListId,
            ["DisplayText"] = noStatus ? item.Subject : item.ToString()
        };
    }
}
