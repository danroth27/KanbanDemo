namespace KanbanDemo.Services;

public class InMemoryTaskService : ITaskService
{
    private readonly List<TaskItem> _tasks;
    private int _nextId;

    public InMemoryTaskService()
    {
        _tasks = new List<TaskItem>
        {
            new(1, "Set up CI/CD pipeline", "Alice", "High", DateTime.Today.AddDays(-2), "To Do"),
            new(2, "Design database schema", "Bob", "High", DateTime.Today.AddDays(3), "To Do"),
            new(3, "Implement user auth", "Alice", "High", DateTime.Today.AddDays(5), "In Progress"),
            new(4, "Create REST API endpoints", "Charlie", "Medium", DateTime.Today.AddDays(7), "In Progress"),
            new(5, "Write unit tests", "Bob", "Medium", DateTime.Today.AddDays(10), "To Do"),
            new(6, "Set up logging", "Charlie", "Low", DateTime.Today.AddDays(14), "Done"),
            new(7, "Deploy to staging", "Alice", "High", DateTime.Today.AddDays(-1), "To Do"),
            new(8, "Code review PR #42", "Bob", "Medium", DateTime.Today.AddDays(1), "In Progress"),
        };
        _nextId = _tasks.Count + 1;
    }

    public Task<List<TaskItem>> GetAllTasksAsync()
    {
        return Task.FromResult(_tasks.ToList());
    }

    public Task<TaskItem> CreateTaskAsync(string title, string assignee, string priority, DateTime dueDate)
    {
        var task = new TaskItem(_nextId++, title, assignee, priority, dueDate, "To Do");
        _tasks.Add(task);
        return Task.FromResult(task);
    }

    public Task UpdateTaskStatusAsync(int taskId, string newStatus)
    {
        var index = _tasks.FindIndex(t => t.Id == taskId);
        if (index >= 0)
        {
            var old = _tasks[index];
            _tasks[index] = old with { Status = newStatus };
        }
        return Task.CompletedTask;
    }
}
