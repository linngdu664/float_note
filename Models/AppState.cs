namespace FloatNote.Models;

public sealed class AppState
{
    public string NoteText { get; set; } = string.Empty;
    public bool ShowCompletedTodos { get; set; }
    public bool IsDarkTheme { get; set; }
    public WindowSnapshot Window { get; set; } = new();
    public FloatingBallSnapshot FloatingBall { get; set; } = new();
    public List<TodoItem> Todos { get; set; } = [];
}
