using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FloatNote.ViewModels;

namespace FloatNote;

public partial class FloatingBallWindow : Window
{
    private const double BallSize = 54;
    private const double EdgeWidth = 18;
    private const double EdgeHeight = 92;
    private const double EdgeThreshold = 22;
    private const double ClickMoveTolerance = 4;
    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    private readonly MainViewModel _viewModel;
    private readonly Action _toggleMainWindow;
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
        Action toggleMainWindow,
        Action exitApplication)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _previewWindow = previewWindow;
        _toggleMainWindow = toggleMainWindow;
        _exitApplication = exitApplication;

        Left = viewModel.FloatingBall.Left;
        Top = viewModel.FloatingBall.Top;
        ApplyEdgeShape();
        Deactivated += (_, _) => Dispatcher.BeginInvoke(BringAboveMainWindow);
    }

    public void BringAboveMainWindow()
    {
        Topmost = true;
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            SetWindowPos(handle, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpShowWindow);
        }
    }

    private void Shell_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        ClearWindowShapeAnimations();
        _dragMouseStart = GetMouseScreenPoint(e);
        _dragWindowLeft = Left;
        _dragWindowTop = Top;
        _isDragging = true;
        _movedDuringDrag = false;
        AnimateShell(0.96, 0.18);
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
            var snappedPosition = SnapToEdgeIfNeeded();
            _viewModel.UpdateFloatingBallPosition(snappedPosition.X, snappedPosition.Y);
            return;
        }

        AnimateShell(1, 0.24);
        _toggleMainWindow();
        BringAboveMainWindow();
    }

    private void Shell_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        _previewWindow.HideAnimated();
        AnimateShell(1, 0.24);

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
        AnimateShell(1.04, 0.34);

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

        AnimateShell(1, 0.24);

        await Task.Delay(160);
        if (!IsMouseOver && !_previewWindow.IsMouseOver)
        {
            _previewWindow.HideAnimated();
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

    private System.Windows.Point SnapToEdgeIfNeeded()
    {
        var area = GetCurrentWorkArea();
        var shouldDockLeft = Left <= area.Left + EdgeThreshold;
        var shouldDockRight = Left + Width >= area.Right - EdgeThreshold;
        var targetLeft = Left;
        var targetTop = Top;
        var targetWidth = BallSize;
        var targetHeight = BallSize;
        CornerRadius targetCornerRadius;

        if (shouldDockLeft)
        {
            targetLeft = area.Left;
            targetWidth = EdgeWidth;
            targetHeight = EdgeHeight;
            targetCornerRadius = new CornerRadius(0, 12, 12, 0);
        }
        else if (shouldDockRight)
        {
            targetLeft = area.Right - EdgeWidth;
            targetWidth = EdgeWidth;
            targetHeight = EdgeHeight;
            targetCornerRadius = new CornerRadius(12, 0, 0, 12);
        }
        else
        {
            targetCornerRadius = new CornerRadius(BallSize / 2);
        }

        targetTop = Math.Clamp(targetTop, area.Top, area.Bottom - targetHeight);
        AnimateWindowShape(targetLeft, targetTop, targetWidth, targetHeight, targetCornerRadius);
        AnimateShell(1, 0.24);
        return new System.Windows.Point(targetLeft, targetTop);
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

    private void AnimateWindowShape(
        double targetLeft,
        double targetTop,
        double targetWidth,
        double targetHeight,
        CornerRadius targetCornerRadius)
    {
        var startLeft = Left;
        var startTop = Top;
        var startWidth = Width;
        var startHeight = Height;
        var startShellWidth = Shell.Width;
        var startShellHeight = Shell.Height;

        Left = targetLeft;
        Top = targetTop;
        Width = targetWidth;
        Height = targetHeight;
        Shell.Width = targetWidth;
        Shell.Height = targetHeight;
        Shell.CornerRadius = targetCornerRadius;

        BeginAnimation(LeftProperty, CreateSnapAnimation(startLeft, targetLeft));
        BeginAnimation(TopProperty, CreateSnapAnimation(startTop, targetTop));
        BeginAnimation(WidthProperty, CreateSnapAnimation(startWidth, targetWidth));
        BeginAnimation(HeightProperty, CreateSnapAnimation(startHeight, targetHeight));
        Shell.BeginAnimation(WidthProperty, CreateSnapAnimation(startShellWidth, targetWidth));
        Shell.BeginAnimation(HeightProperty, CreateSnapAnimation(startShellHeight, targetHeight));
    }

    private void ClearWindowShapeAnimations()
    {
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        Shell.BeginAnimation(WidthProperty, null);
        Shell.BeginAnimation(HeightProperty, null);
    }

    private void AnimateShell(double scale, double shadowOpacity)
    {
        var scaleAnimation = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var shadowAnimation = new DoubleAnimation(shadowOpacity, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        ShellScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
        ShellScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation.Clone());
        ShellShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, shadowAnimation);
    }

    private static DoubleAnimation CreateSnapAnimation(double from, double to)
    {
        return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(190))
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
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

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(
        IntPtr hWnd,
        IntPtr hWndInsertAfter,
        int x,
        int y,
        int cx,
        int cy,
        uint uFlags);
}
