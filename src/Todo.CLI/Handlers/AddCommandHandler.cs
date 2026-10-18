namespace Todo.CLI.Handlers;

using System;
using System.Threading.Tasks;
using Core.Model;
using Core.Repository;
using Microsoft.Extensions.DependencyInjection;
using Todo.CLI.UI;

public class AddCommandHandler
{
    private readonly IUserInteraction _userInteraction;
    private readonly ITodoListRepository _todoListRepository;
    private readonly ITodoItemRepository _todoItemRepository;

    private AddCommandHandler(
        ITodoListRepository todoListRepository,
        ITodoItemRepository todoItemRepository,
        IUserInteraction userInteraction)
    {
        _todoListRepository = todoListRepository;
        _todoItemRepository = todoItemRepository;
        _userInteraction = userInteraction;
    }

    public class List
    {
        public static Func<string, Task<int>> Create(IServiceProvider serviceProvider)
        {
            var todoListRepository = serviceProvider.GetRequiredService<ITodoListRepository>();
            var userInteraction = serviceProvider.GetRequiredService<IUserInteraction>();
            var handler = new AddCommandHandler(todoListRepository, null, userInteraction);
            return handler.HandleListAsync;
        }
    }

    public class Item
    {
        public static Func<string, string, bool, string?, string?, string?, Task<int>> Create(IServiceProvider serviceProvider)
        {
            var todoListRepository = serviceProvider.GetRequiredService<ITodoListRepository>();
            var todoItemRepository = serviceProvider.GetRequiredService<ITodoItemRepository>();
            var userInteraction = serviceProvider.GetRequiredService<IUserInteraction>();
            var handler = new AddCommandHandler(todoListRepository, todoItemRepository, userInteraction);
            return handler.HandleItemAsync;
        }
    }

    internal static DateTime? ParseDueDate(string? dueDateString)
    {
        if (string.IsNullOrWhiteSpace(dueDateString))
            return null;

        var currentYear = DateTime.Now.Year;

        if (DateTime.TryParseExact(dueDateString, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out var fullDate))
            return fullDate;

        if (DateTime.TryParseExact(dueDateString, "MM-dd", null, System.Globalization.DateTimeStyles.None, out var partialDate))
            return new DateTime(currentYear, partialDate.Month, partialDate.Day);

        return null;
    }

    internal static DateTime? ParseRemindDate(string? remindString, DateTime? dueDate)
    {
        if (string.IsNullOrWhiteSpace(remindString))
            return null;

        // HH:mm — anchored to the due date when given, otherwise today
        if (DateTime.TryParseExact(remindString, "HH:mm", null, System.Globalization.DateTimeStyles.None, out var timeOnly))
        {
            var day = dueDate ?? DateTime.Today;
            return new DateTime(day.Year, day.Month, day.Day, timeOnly.Hour, timeOnly.Minute, 0);
        }

        if (DateTime.TryParseExact(remindString, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out var spaceFull))
            return spaceFull;

        if (DateTime.TryParseExact(remindString, "yyyy-MM-ddTHH:mm", null, System.Globalization.DateTimeStyles.None, out var isoFull))
            return isoFull;

        return null;
    }

    private async Task<int> HandleListAsync(string name)
    {
        try
        {
            if (string.IsNullOrEmpty(name))
            {
                _userInteraction.ShowError("Name is required to add a list.");
                return 1;
            }

            await _todoListRepository.AddAsync(new TodoList
            {
                Name = name
            });
            _userInteraction.ShowSuccess($"List '{name}' created successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            _userInteraction.ShowError($"Error creating list: {ex.Message}");
            return 1;
        }
    }

    private async Task<int> HandleItemAsync(string subject, string listName, bool star, string? dueDateString, string? remindString, string? note)
    {
        try
        {
            if (string.IsNullOrEmpty(subject))
            {
                _userInteraction.ShowError("Subject is required to add an item.");
                return 1;
            }

            var dueDate = ParseDueDate(dueDateString);
            if (dueDateString is not null && dueDate is null)
            {
                _userInteraction.ShowError($"Invalid due date format. Use yyyy-MM-dd or MM-dd.");
                return 1;
            }

            var remindAt = ParseRemindDate(remindString, dueDate);
            if (remindString is not null && remindAt is null)
            {
                _userInteraction.ShowError($"Invalid remind format. Use HH:mm or yyyy-MM-dd HH:mm.");
                return 1;
            }

            TodoList? list;

            if (string.IsNullOrEmpty(listName))
            {
                list = await _todoListRepository.GetDefaultListAsync();
                if (list == null)
                {
                    _userInteraction.ShowError("Default list not found. Please specify a list.");
                    return 1;
                }
            }
            else
            {
                list = await _todoListRepository.GetByNameAsync(listName);
                if (list == null)
                {
                    _userInteraction.ShowError($"No list found with the name '{listName}'.");
                    return 1;
                }
            }

            await _todoItemRepository.AddAsync(new TodoItem
            {
                Subject = subject,
                ListId = list.Id,
                IsImportant = star,
                DueDate = dueDate,
                Reminder = remindAt,
                Body = note
            });

            _userInteraction.ShowSuccess($"Item '{subject}' added to list '{list.Name}' successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            _userInteraction.ShowError($"Error adding item: {ex.Message}");
            return 1;
        }
    }
}