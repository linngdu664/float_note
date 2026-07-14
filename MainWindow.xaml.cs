using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using FloatNote.Models;
using FloatNote.Services;
using FloatNote.ViewModels;

namespace FloatNote;

public partial class MainWindow : Window
{
    private static readonly TimeSpan DragHoldDuration = TimeSpan.FromMilliseconds(360);

    private readonly MainViewModel _viewModel;
    private readonly Action _exitApplication;
    private HotkeyService? _hotkeyService;
    private DispatcherTimer? _dragHoldTimer;
    private bool _allowClose;
    private TodoItem? _pendingDragTodo;
    private TodoItem? _draggingTodo;
    private System.Windows.Point _dragStartPoint;
    private GridLength _expandedNoteRowHeight = new(1, GridUnitType.Star);
    private bool _isTodoDragActive;
    private bool _isHidingAnimated;

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

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        ApplyNoteCollapsedState();
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    public void ShowAt(double left, double top)
    {
        ShowAnimated();
        WindowState = WindowState.Normal;
        var area = SystemParameters.WorkArea;
        Left = Math.Clamp(left, area.Left, area.Right - ActualWidth);
        Top = Math.Clamp(top, area.Top, area.Bottom - ActualHeight);
        Activate();
    }

    public void ShowAnimated()
    {
        _isHidingAnimated = false;
        BeginAnimation(OpacityProperty, null);
        MainChromeTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        if (!IsVisible)
        {
            Opacity = 0;
            MainChromeTranslate.Y = -10;
            Show();
        }

        WindowState = WindowState.Normal;
        Activate();
        AnimateIn();
    }

    public void HideAnimated()
    {
        if (!IsVisible || _isHidingAnimated)
        {
            return;
        }

        _isHidingAnimated = true;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        var slide = new DoubleAnimation(8, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        fade.Completed += (_, _) =>
        {
            Hide();
            BeginAnimation(OpacityProperty, null);
            MainChromeTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            Opacity = 1;
            MainChromeTranslate.Y = 0;
            _isHidingAnimated = false;
        };

        BeginAnimation(OpacityProperty, fade);
        MainChromeTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
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
            HideAnimated();
            return;
        }

        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        _hotkeyService?.Dispose();
        base.OnClosed(e);
    }

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        AnimateIn();
    }

    private void ToggleVisibility()
    {
        if (IsVisible)
        {
            HideAnimated();
            return;
        }

        ShowAnimated();
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
        HideAnimated();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _exitApplication();
    }

    private void AnimateIn()
    {
        var fade = new DoubleAnimation(1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        var slide = new DoubleAnimation(0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        BeginAnimation(OpacityProperty, fade);
        MainChromeTranslate.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsNoteCollapsed))
        {
            ApplyNoteCollapsedState();
        }
    }

    private void ApplyNoteCollapsedState()
    {
        if (_viewModel.IsNoteCollapsed)
        {
            if (NoteRow.Height.Value > 0)
            {
                _expandedNoteRowHeight = NoteRow.Height;
            }

            NoteHost.Visibility = Visibility.Collapsed;
            NoteSplitter.Visibility = Visibility.Collapsed;
            NoteRow.MinHeight = 0;
            NoteRow.Height = new GridLength(0);
            NoteSplitterRow.Height = new GridLength(0);
            return;
        }

        NoteHost.Visibility = Visibility.Visible;
        NoteSplitter.Visibility = Visibility.Visible;
        NoteRow.MinHeight = 140;
        NoteRow.Height = _expandedNoteRowHeight.Value > 0
            ? _expandedNoteRowHeight
            : new GridLength(1, GridUnitType.Star);
        NoteSplitterRow.Height = new GridLength(8);
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
            _viewModel.ToggleTodoExpandedCommand.Execute(todo);
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
