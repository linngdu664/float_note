using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using FloatNote.Models;
using FloatNote.Services;
using FloatNote.ViewModels;

namespace FloatNote;

public partial class MainWindow : Window
{
    private const long ShortDoubleClickMilliseconds = 240;
    private static readonly TimeSpan DragHoldDuration = TimeSpan.FromMilliseconds(360);

    private readonly MainViewModel _viewModel;
    private readonly Action _exitApplication;
    private HotkeyService? _hotkeyService;
    private DispatcherTimer? _dragHoldTimer;
    private bool _allowClose;
    private Guid? _lastClickedTodoId;
    private long _lastTodoClickAt;
    private TodoItem? _pendingDragTodo;
    private TodoItem? _draggingTodo;
    private System.Windows.Point _dragStartPoint;
    private bool _isTodoDragActive;

    public MainWindow(MainViewModel viewModel, Action exitApplication)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _exitApplication = exitApplication;
        DataContext = viewModel;

        Left = viewModel.Window.Left;
        Top = viewModel.Window.Top;
        Width = Math.Max(viewModel.Window.Width, 520);
        Height = Math.Max(viewModel.Window.Height, 760);
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    public void ShowAt(double left, double top)
    {
        Show();
        WindowState = WindowState.Normal;
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(left, area.Left, area.Right - ActualWidth);
        Top = Math.Clamp(top, area.Top, area.Bottom - ActualHeight);
        Activate();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var handle = new WindowInteropHelper(this).Handle;
        _hotkeyService = new HotkeyService(handle, ToggleVisibility);
    }

    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);
        SaveWindowBounds();
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        SaveWindowBounds();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkeyService?.Dispose();
        base.OnClosed(e);
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            Hide();
            return;
        }

        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void SaveWindowBounds()
    {
        if (!IsLoaded || WindowState != WindowState.Normal)
        {
            return;
        }

        _viewModel.UpdateWindowBounds(Left, Top, ActualWidth, ActualHeight);
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            return;
        }

        DragMove();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _exitApplication();
    }

    private void NewTodoTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        _viewModel.AddTodoCommand.Execute(null);
        e.Handled = true;
    }

    private void TodoHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isTodoDragActive)
        {
            e.Handled = true;
            return;
        }

        if (IsInteractiveSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is TodoItem todo)
        {
            var now = Environment.TickCount64;
            var isShortDoubleClick = _lastClickedTodoId == todo.Id
                                     && now - _lastTodoClickAt <= ShortDoubleClickMilliseconds;

            _lastClickedTodoId = todo.Id;
            _lastTodoClickAt = now;

            if (isShortDoubleClick)
            {
                _viewModel.ToggleCurrentTodoCommand.Execute(todo);
                _lastClickedTodoId = null;
                _lastTodoClickAt = 0;
            }
            else
            {
                _viewModel.ToggleTodoExpandedCommand.Execute(todo);
            }

            e.Handled = true;
        }
    }

    private void TodoCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInteractiveSource(e.OriginalSource as DependencyObject)
            || (sender as FrameworkElement)?.DataContext is not TodoItem todo)
        {
            return;
        }

        _pendingDragTodo = todo;
        _dragStartPoint = e.GetPosition(TodoListBox);
        _isTodoDragActive = false;

        _dragHoldTimer?.Stop();
        _dragHoldTimer = new DispatcherTimer
        {
            Interval = DragHoldDuration
        };
        _dragHoldTimer.Tick += (_, _) =>
        {
            _dragHoldTimer?.Stop();
            _isTodoDragActive = true;
        };
        _dragHoldTimer.Start();
    }

    private void TodoCard_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_pendingDragTodo is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = e.GetPosition(TodoListBox);
        var movedEnough = Math.Abs(current.X - _dragStartPoint.X) > SystemParameters.MinimumHorizontalDragDistance
                          || Math.Abs(current.Y - _dragStartPoint.Y) > SystemParameters.MinimumVerticalDragDistance;

        if (!_isTodoDragActive || !movedEnough)
        {
            return;
        }

        _dragHoldTimer?.Stop();
        _draggingTodo = _pendingDragTodo;
        DragDrop.DoDragDrop((DependencyObject)sender, _draggingTodo, System.Windows.DragDropEffects.Move);
        ClearTodoDragState();
        e.Handled = true;
    }

    private void TodoCard_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _dragHoldTimer?.Stop();
        _pendingDragTodo = null;
    }

    private void TodoCard_DragEnter(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TodoItem)) ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void TodoCard_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!TryGetDraggedTodo(e, out var draggedTodo)
            || (sender as FrameworkElement)?.DataContext is not TodoItem targetTodo)
        {
            return;
        }

        var targetIndex = _viewModel.GetVisibleIndex(targetTodo);
        if (sender is FrameworkElement targetElement
            && e.GetPosition(targetElement).Y > targetElement.ActualHeight / 2)
        {
            targetIndex++;
        }

        if (targetIndex >= 0)
        {
            _viewModel.MoveTodo(draggedTodo, targetIndex);
        }

        ClearTodoDragState();
        e.Handled = true;
    }

    private void TodoListBox_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(TodoItem)) ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void TodoListBox_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!TryGetDraggedTodo(e, out var draggedTodo))
        {
            return;
        }

        var targetIndex = _viewModel.VisibleTodos.Cast<object>().Count();
        _viewModel.MoveTodo(draggedTodo, targetIndex);
        ClearTodoDragState();
        e.Handled = true;
    }

    private static bool TryGetDraggedTodo(System.Windows.DragEventArgs e, out TodoItem todo)
    {
        if (e.Data.GetData(typeof(TodoItem)) is TodoItem draggedTodo)
        {
            todo = draggedTodo;
            return true;
        }

        todo = null!;
        return false;
    }

    private void ClearTodoDragState()
    {
        _dragHoldTimer?.Stop();
        _pendingDragTodo = null;
        _draggingTodo = null;
        _isTodoDragActive = false;
    }

    private static bool IsInteractiveSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is System.Windows.Controls.Primitives.ButtonBase
                or System.Windows.Controls.TextBox
                or System.Windows.Controls.CheckBox)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }
}
