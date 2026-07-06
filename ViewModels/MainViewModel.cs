using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FloatNote.Models;
using FloatNote.Services;

namespace FloatNote.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
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

    public MainViewModel(AppState state, AppStorage storage)
    {
        _state = state;
        _storage = storage;
        _noteText = state.NoteText;
        _showCompletedTodos = state.ShowCompletedTodos;
        _isDarkTheme = state.IsDarkTheme;

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

    public string ThemeButtonText => IsDarkTheme ? "浅色" : "深色";

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
        OnPropertyChanged(nameof(ThemeButtonText));
        ScheduleSave();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
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
        RefreshViews();
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
}
