using System;
using System.CommandLine;
using Todo.CLI.Handlers;

namespace Todo.CLI.Commands;

public class ListsCommand : Command
{
    public ListsCommand(IServiceProvider serviceProvider) : base("lists", "Lists all to do lists.")
    {
        Add(TodoCommand.OutputFormat);
        this.SetHandler(ListsCommandHandler.Create(serviceProvider));
    }
}
