using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatNote.Models;
using FloatNote.Services;

namespace FloatNote.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly AppState _state;
    private readonly AppStorage _storage;
    private CancellationTokenSource? _saveDelay;

    [ObservableProperty]
    private string _noteText;

    [ObservableProperty]
    private string _newTodoTitle = string.Empty;

    [ObservableProperty]
    private string _newTodoContent = string.Empty;

    [ObservableProperty]
    private bool _showCompletedTodos;

    [ObservableProperty]
    private bool _isDarkTheme;

    [ObservableProperty]
    private bool _isNoteCollapsed;

    [ObservableProperty]
    private bool _isTodoCollapsed;

    public MainViewModel(AppState state, AppStorage storage)
    {
        _state = state;
        _storage = storage;
        _noteText = state.NoteText;
        _showCompletedTodos = state.ShowCompletedTodos;
        _isDarkTheme = state.IsDarkTheme;
        _isNoteCollapsed = state.IsNoteCollapsed;
        _isTodoCollapsed = state.IsTodoCollapsed && !state.IsNoteCollapsed;
        _state.IsTodoCollapsed = _isTodoCollapsed;

        NormalizeTodos(state.Todos);
        Todos = new ObservableCollection<TodoItem>(state.Todos.OrderBy(todo => todo.Order));
        Todos.CollectionChanged += OnTodosCollectionChanged;

        VisibleTodos = CollectionViewSource.GetDefaultView(Todos);
        VisibleTodos.Filter = FilterTodo;

        CurrentTodos = new ListCollectionView(Todos);
        CurrentTodos.Filter = item => item is TodoItem todo && todo.IsCurrent;

        foreach (var todo in Todos)
        {
            todo.PropertyChanged += OnTodoPropertyChanged;
        }
    }

    public ObservableCollection<TodoItem> Todos { get; }

    public ICollectionView VisibleTodos { get; }

    public ICollectionView CurrentTodos { get; }

    public WindowSnapshot Window => _state.Window;

    public FloatingBallSnapshot FloatingBall => _state.FloatingBall;

    public string ThemeButtonIcon => IsDarkTheme ? "☀" : "☾";

    public string NoteToggleButtonText => IsNoteCollapsed ? "展开便签" : "折叠便签";

    public string TodoToggleButtonText => IsTodoCollapsed ? "展开待办" : "折叠待办";

    partial void OnNoteTextChanged(string value)
    {
        _state.NoteText = value;
        ScheduleSave();
    }

    partial void OnShowCompletedTodosChanged(bool value)
    {
        _state.ShowCompletedTodos = value;
        VisibleTodos.Refresh();
        ScheduleSave();
    }

    partial void OnIsDarkThemeChanged(bool value)
    {
        _state.IsDarkTheme = value;
        ThemeService.Apply(value);
        OnPropertyChanged(nameof(ThemeButtonIcon));
        ScheduleSave();
    }

    partial void OnIsNoteCollapsedChanged(bool value)
    {
        _state.IsNoteCollapsed = value;
        OnPropertyChanged(nameof(NoteToggleButtonText));
        ScheduleSave();
    }

    partial void OnIsTodoCollapsedChanged(bool value)
    {
        _state.IsTodoCollapsed = value;
        OnPropertyChanged(nameof(TodoToggleButtonText));
        ScheduleSave();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
    }

    [RelayCommand]
    private void ToggleNoteCollapsed()
    {
        if (!IsNoteCollapsed && IsTodoCollapsed)
        {
            IsTodoCollapsed = false;
        }

        IsNoteCollapsed = !IsNoteCollapsed;
    }

    [RelayCommand]
    private void ToggleTodoCollapsed()
    {
        if (!IsTodoCollapsed && IsNoteCollapsed)
        {
            IsNoteCollapsed = false;
        }

        IsTodoCollapsed = !IsTodoCollapsed;
    }

    [RelayCommand]
    private void UnescapeNoteText()
    {
        if (string.IsNullOrEmpty(NoteText))
        {
            return;
        }

        NoteText = UnescapeString(NoteText);
    }

    [RelayCommand]
    private void FormatNoteJson()
    {
        if (string.IsNullOrWhiteSpace(NoteText))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(NoteText);
            NoteText = JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions);
        }
        catch (JsonException)
        {
        }
    }

    [RelayCommand]
    private void AddTodo()
    {
        var title = NewTodoTitle.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return;
        }

        var nextOrder = Todos.Count == 0 ? 1 : Todos.Max(todo => todo.Order) + 1;
        var todo = new TodoItem
        {
            Title = title,
            Content = NewTodoContent.Trim(),
            Order = nextOrder,
            IsExpanded = false
        };

        todo.PropertyChanged += OnTodoPropertyChanged;
        Todos.Add(todo);
        _state.Todos = Todos.ToList();
        NewTodoTitle = string.Empty;
        NewTodoContent = string.Empty;
        RefreshViews();
        ScheduleSave();
    }

    [RelayCommand]
    private void DeleteTodo(TodoItem? todo)
    {
        if (todo is null)
        {
            return;
        }

        todo.PropertyChanged -= OnTodoPropertyChanged;
        Todos.Remove(todo);
        _state.Todos = Todos.ToList();
        RefreshViews();
        ScheduleSave();
    }

    [RelayCommand]
    private void ToggleTodoExpanded(TodoItem? todo)
    {
        if (todo is null)
        {
            return;
        }

        todo.IsExpanded = !todo.IsExpanded;
    }

    [RelayCommand]
    private void ToggleCurrentTodo(TodoItem? todo)
    {
        if (todo is null)
        {
            return;
        }

        todo.IsCurrent = !todo.IsCurrent;
    }

    public void UpdateWindowBounds(double left, double top, double width, double height)
    {
        if (double.IsNaN(left) || double.IsNaN(top) || width <= 0 || height <= 0)
        {
            return;
        }

        _state.Window.Left = left;
        _state.Window.Top = top;
        _state.Window.Width = width;
        _state.Window.Height = height;
        ScheduleSave();
    }

    public void UpdateFloatingBallPosition(double left, double top)
    {
        if (double.IsNaN(left) || double.IsNaN(top))
        {
            return;
        }

        _state.FloatingBall.Left = left;
        _state.FloatingBall.Top = top;
        ScheduleSave();
    }

    public void MoveTodo(TodoItem todo, int targetVisibleIndex)
    {
        if (!Todos.Contains(todo))
        {
            return;
        }

        var visibleTodos = VisibleTodos.Cast<TodoItem>()
            .Where(visibleTodo => visibleTodo != todo)
            .ToList();
        if (visibleTodos.Count == 0)
        {
            return;
        }

        var oldIndex = Todos.IndexOf(todo);
        if (oldIndex < 0)
        {
            return;
        }

        Todos.RemoveAt(oldIndex);

        targetVisibleIndex = Math.Clamp(targetVisibleIndex, 0, visibleTodos.Count);
        var insertIndex = Todos.Count;
        if (targetVisibleIndex < visibleTodos.Count)
        {
            insertIndex = Math.Max(0, Todos.IndexOf(visibleTodos[targetVisibleIndex]));
        }
        else if (visibleTodos.Count > 0)
        {
            insertIndex = Math.Min(Todos.Count, Todos.IndexOf(visibleTodos[^1]) + 1);
        }

        Todos.Insert(insertIndex, todo);
        for (var i = 0; i < Todos.Count; i++)
        {
            Todos[i].Order = i + 1;
        }

        _state.Todos = Todos.ToList();
        RefreshViews();
        ScheduleSave();
    }

    public int GetVisibleIndex(TodoItem todo)
    {
        var visibleTodos = VisibleTodos.Cast<TodoItem>().ToList();
        return visibleTodos.IndexOf(todo);
    }

    public Task SaveNowAsync()
    {
        _saveDelay?.Cancel();
        _state.Todos = Todos.ToList();
        return _storage.SaveAsync(_state);
    }

    private bool FilterTodo(object item)
    {
        return item is TodoItem todo && (ShowCompletedTodos || !todo.IsCompleted);
    }

    private void OnTodosCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RefreshViews();
    }

    private void OnTodoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _state.Todos = Todos.ToList();

        if (e.PropertyName == nameof(TodoItem.IsCompleted))
        {
            VisibleTodos.Refresh();
        }
        else if (e.PropertyName == nameof(TodoItem.IsCurrent))
        {
            CurrentTodos.Refresh();
        }

        ScheduleSave();
    }

    private void RefreshViews()
    {
        VisibleTodos.Refresh();
        CurrentTodos.Refresh();
    }

    private void ScheduleSave()
    {
        _saveDelay?.Cancel();
        _saveDelay = new CancellationTokenSource();
        var token = _saveDelay.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(350, token);
                await _storage.SaveAsync(_state);
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private static void NormalizeTodos(IEnumerable<TodoItem> todos)
    {
        foreach (var todo in todos)
        {
            if (string.IsNullOrWhiteSpace(todo.Title) && !string.IsNullOrWhiteSpace(todo.Text))
            {
                todo.Title = todo.Text;
            }
        }
    }

    private static string UnescapeString(string value)
    {
        var builder = new StringBuilder(value.Length);

        for (var i = 0; i < value.Length; i++)
        {
            var character = value[i];
            if (character != '\\' || i == value.Length - 1)
            {
                builder.Append(character);
                continue;
            }

            var escaped = value[++i];
            switch (escaped)
            {
                case 'n':
                    builder.Append('\n');
                    break;
                case 'r':
                    builder.Append('\r');
                    break;
                case 't':
                    builder.Append('\t');
                    break;
                case 'b':
                    builder.Append('\b');
                    break;
                case 'f':
                    builder.Append('\f');
                    break;
                case '\\':
                    builder.Append('\\');
                    break;
                case '"':
                    builder.Append('"');
                    break;
                case '/':
                    builder.Append('/');
                    break;
                case 'u':
                    if (TryReadUnicodeEscape(value, i + 1, out var unicodeCharacter))
                    {
                        builder.Append(unicodeCharacter);
                        i += 4;
                    }
                    else
                    {
                        builder.Append('\\');
                        builder.Append(escaped);
                    }

                    break;
                default:
                    builder.Append('\\');
                    builder.Append(escaped);
                    break;
            }
        }

        return builder.ToString();
    }

    private static bool TryReadUnicodeEscape(string value, int startIndex, out char character)
    {
        character = '\0';
        if (startIndex + 4 > value.Length)
        {
            return false;
        }

        var codePoint = 0;
        for (var i = startIndex; i < startIndex + 4; i++)
        {
            var digit = HexValue(value[i]);
            if (digit < 0)
            {
                return false;
            }

            codePoint = (codePoint << 4) + digit;
        }

        character = (char)codePoint;
        return true;
    }

    private static int HexValue(char character)
    {
        if (character is >= '0' and <= '9')
        {
            return character - '0';
        }

        if (character is >= 'a' and <= 'f')
        {
            return character - 'a' + 10;
        }

        if (character is >= 'A' and <= 'F')
        {
            return character - 'A' + 10;
        }

        return -1;
    }
}
