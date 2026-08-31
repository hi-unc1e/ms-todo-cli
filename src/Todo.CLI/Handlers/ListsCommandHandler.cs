using System;
using System.Threading.Tasks;
using Todo.Core.Repository;
using Microsoft.Extensions.DependencyInjection;
using Todo.CLI.UI;

namespace Todo.CLI.Handlers;

public class ListsCommandHandler
{
    private readonly IUserInteraction _userInteraction;
    private readonly ITodoListRepository _todoListRepository;

    private ListsCommandHandler(ITodoListRepository todoListRepository, IUserInteraction userInteraction)
    {
        _todoListRepository = todoListRepository;
        _userInteraction = userInteraction;
    }

    public static Func<Task<int>> Create(IServiceProvider serviceProvider)
    {
        var handler = new ListsCommandHandler(
            serviceProvider.GetRequiredService<ITodoListRepository>(),
            serviceProvider.GetRequiredService<IUserInteraction>());
        return handler.HandleAsync;
    }

    private async Task<int> HandleAsync()
    {
        try
        {
            var lists = await _todoListRepository.GetAllAsync();
            _userInteraction.OutputFormatter.FormatLists(lists);
            return 0;
        }
        catch (Exception ex)
        {
            _userInteraction.ShowError($"Error listing lists: {ex.Message}");
            return 1;
        }
    }
}
