using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using FloatNote.ViewModels;

namespace FloatNote;

public partial class FloatingBallWindow : Window
{
    private const double BallSize = 54;
    private const double EdgeWidth = 18;
    private const double EdgeHeight = 92;
    private const double EdgeThreshold = 22;
    private const double ClickMoveTolerance = 4;

    private readonly MainViewModel _viewModel;
    private readonly Action<double, double> _openMainWindow;
    private readonly Action _exitApplication;
    private readonly CurrentTodosPreviewWindow _previewWindow;
    private System.Windows.Point _dragMouseStart;
    private double _dragWindowLeft;
    private double _dragWindowTop;
    private bool _isDragging;
    private bool _movedDuringDrag;

    public FloatingBallWindow(
        MainViewModel viewModel,
        CurrentTodosPreviewWindow previewWindow,
        Action<double, double> openMainWindow,
        Action exitApplication)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _previewWindow = previewWindow;
        _openMainWindow = openMainWindow;
        _exitApplication = exitApplication;

        Left = viewModel.FloatingBall.Left;
        Top = viewModel.FloatingBall.Top;
        ApplyEdgeShape();
    }

    public void BringAboveMainWindow()
    {
        Topmost = false;
        Topmost = true;
    }

    private void Shell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragMouseStart = GetMouseScreenPoint(e);
        _dragWindowLeft = Left;
        _dragWindowTop = Top;
        _isDragging = true;
        _movedDuringDrag = false;
        Shell.CaptureMouse();
    }

    private void Shell_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        var currentMouse = GetMouseScreenPoint(e);
        var deltaX = currentMouse.X - _dragMouseStart.X;
        var deltaY = currentMouse.Y - _dragMouseStart.Y;

        Left = _dragWindowLeft + deltaX;
        Top = _dragWindowTop + deltaY;

        if (Math.Abs(deltaX) > ClickMoveTolerance || Math.Abs(deltaY) > ClickMoveTolerance)
        {
            _movedDuringDrag = true;
        }
    }

    private void Shell_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        Shell.ReleaseMouseCapture();

        if (_movedDuringDrag)
        {
            SnapToEdgeIfNeeded();
            _viewModel.UpdateFloatingBallPosition(Left, Top);
            return;
        }

        _openMainWindow(Left, Top);
    }

    private void Shell_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _previewWindow.Hide();

        var menu = new System.Windows.Controls.ContextMenu();
        var exitItem = new System.Windows.Controls.MenuItem
        {
            Header = "退出"
        };
        exitItem.Click += (_, _) => _exitApplication();
        menu.Items.Add(exitItem);
        menu.PlacementTarget = Shell;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void Shell_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var previewLeft = Left + Width + 8;
        if (previewLeft + _previewWindow.Width > SystemParameters.WorkArea.Right)
        {
            previewLeft = Left - _previewWindow.Width - 8;
        }

        _previewWindow.ShowNear(previewLeft, Top);
    }

    private async void Shell_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDragging)
        {
            return;
        }

        await Task.Delay(160);
        if (!IsMouseOver && !_previewWindow.IsMouseOver)
        {
            _previewWindow.Hide();
        }
    }

    private System.Windows.Point GetMouseScreenPoint(System.Windows.Input.MouseEventArgs e)
    {
        var physicalPoint = PointToScreen(e.GetPosition(this));
        var source = PresentationSource.FromVisual(this);
        return source?.CompositionTarget is null
            ? physicalPoint
            : source.CompositionTarget.TransformFromDevice.Transform(physicalPoint);
    }

    private void SnapToEdgeIfNeeded()
    {
        var area = GetCurrentWorkArea();
        var shouldDockLeft = Left <= area.Left + EdgeThreshold;
        var shouldDockRight = Left + Width >= area.Right - EdgeThreshold;

        if (shouldDockLeft)
        {
            Left = area.Left;
        }
        else if (shouldDockRight)
        {
            Left = area.Right - EdgeWidth;
        }

        Top = Math.Clamp(Top, area.Top, area.Bottom - (shouldDockLeft || shouldDockRight ? EdgeHeight : BallSize));
        ApplyEdgeShape();
    }

    private void ApplyEdgeShape()
    {
        var area = GetCurrentWorkArea();
        var isLeftEdge = Left <= area.Left + EdgeThreshold;
        var isRightEdge = Left + Width >= area.Right - EdgeThreshold;

        if (isLeftEdge || isRightEdge)
        {
            Width = EdgeWidth;
            Height = EdgeHeight;
            Shell.Width = EdgeWidth;
            Shell.Height = EdgeHeight;
            Shell.CornerRadius = isLeftEdge
                ? new CornerRadius(0, 12, 12, 0)
                : new CornerRadius(12, 0, 0, 12);

            if (isRightEdge)
            {
                Left = area.Right - EdgeWidth;
            }

            return;
        }

        Width = BallSize;
        Height = BallSize;
        Shell.Width = BallSize;
        Shell.Height = BallSize;
        Shell.CornerRadius = new CornerRadius(BallSize / 2);
    }

    private Rect GetCurrentWorkArea()
    {
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return SystemParameters.WorkArea;
        }

        var point = PointToScreen(new System.Windows.Point(Width / 2, Height / 2));
        var screen = Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y));
        var topLeft = source.CompositionTarget.TransformFromDevice.Transform(
            new System.Windows.Point(screen.WorkingArea.Left, screen.WorkingArea.Top));
        var bottomRight = source.CompositionTarget.TransformFromDevice.Transform(
            new System.Windows.Point(screen.WorkingArea.Right, screen.WorkingArea.Bottom));
        return new Rect(topLeft, bottomRight);
    }
}
