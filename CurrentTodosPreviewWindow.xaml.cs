using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FloatNote.ViewModels;

namespace FloatNote;

public partial class CurrentTodosPreviewWindow : Window
{
    private bool _isHidingAnimated;

    public CurrentTodosPreviewWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public void ShowNear(double left, double top)
    {
        _isHidingAnimated = false;
        BeginAnimation(OpacityProperty, null);
        PreviewTranslate.BeginAnimation(TranslateTransform.YProperty, null);

        Left = Math.Max(0, left);
        Top = Math.Max(0, top);
        if (!IsVisible)
        {
            Opacity = 0;
            PreviewTranslate.Y = -8;
            Show();
        }

        BeginAnimation(
            OpacityProperty,
            new DoubleAnimation(1, TimeSpan.FromMilliseconds(120))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        PreviewTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(0, TimeSpan.FromMilliseconds(150))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    public void HideAnimated()
    {
        if (!IsVisible || _isHidingAnimated)
        {
            return;
        }

        _isHidingAnimated = true;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(110))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            Hide();
            BeginAnimation(OpacityProperty, null);
            PreviewTranslate.BeginAnimation(TranslateTransform.YProperty, null);
            Opacity = 1;
            PreviewTranslate.Y = 0;
            _isHidingAnimated = false;
        };

        BeginAnimation(OpacityProperty, fade);
        PreviewTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(-5, TimeSpan.FromMilliseconds(110))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            });
    }

    private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Preview_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HideAnimated();
    }
}
