namespace KanbanDemo.Services;

public record TaskItem(int Id, string Title, string Assignee, string Priority, DateTime DueDate, string Status);

public interface ITaskService
{
    Task<List<TaskItem>> GetAllTasksAsync();
    Task<TaskItem> CreateTaskAsync(string title, string assignee, string priority, DateTime dueDate);
    Task UpdateTaskStatusAsync(int taskId, string newStatus);
}
