using System.Windows;
using System.Windows.Input;
using FloatNote.ViewModels;

namespace FloatNote;

public partial class CurrentTodosPreviewWindow : Window
{
    public CurrentTodosPreviewWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    public void ShowNear(double left, double top)
    {
        Left = Math.Max(0, left);
        Top = Math.Max(0, top);
        Show();
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
        Hide();
    }
}
