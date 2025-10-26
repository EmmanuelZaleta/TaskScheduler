using System.Windows;
using YCC.SapAutomation.Host.Utilities;

namespace YCC.SapAutomation.Host.Views;

public partial class MainWindow : Window
{
  private MainWindowViewModel? _viewModel;

  public MainWindow(MainWindowViewModel viewModel)
  {
    InitializeComponent();
    DataContext = viewModel;
    _viewModel = viewModel;
    Loaded += MainWindow_Loaded;
  }

  private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
  {
    if (_viewModel != null)
    {
      await _viewModel.InitializeAsync();
    }
  }

  private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
  {
    if (WindowState != WindowState.Minimized)
    {
      e.Cancel = true;
      WindowState = WindowState.Minimized;
      Hide();
    }
  }

  private void Window_StateChanged(object sender, EventArgs e)
  {
    if (WindowState == WindowState.Minimized)
    {
      Hide();
      ShowInTaskbar = false;
    }
  }

  private void TaskbarIcon_ShowWindow(object sender, RoutedEventArgs e)
  {
    Show();
    WindowState = WindowState.Normal;
    ShowInTaskbar = true;
    Activate();
  }

  private void TaskbarIcon_Pause(object sender, RoutedEventArgs e)
  {
    _viewModel?.PauseSchedulerCommand.Execute(null);
  }

  private void TaskbarIcon_Resume(object sender, RoutedEventArgs e)
  {
    _viewModel?.ResumeSchedulerCommand.Execute(null);
  }

  private void TaskbarIcon_Exit(object sender, RoutedEventArgs e)
  {
    System.Windows.Application.Current.Shutdown();
  }

  private void Autostart_Checked(object sender, RoutedEventArgs e)
  {
    if (sender is System.Windows.Controls.CheckBox cb && _viewModel != null)
    {
      _viewModel.IsAutostartEnabled = cb.IsChecked == true;
    }
  }
}
