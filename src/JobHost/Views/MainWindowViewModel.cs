using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Quartz;
using YCC.SapAutomation.Abstractions.Automation;
using YCC.SapAutomation.Host.Utilities;

namespace YCC.SapAutomation.Host.Views;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
  private readonly ISchedulerFactory _schedulerFactory;
  private readonly IJobExecutionNotificationService _notificationService;
  private readonly ILogger<MainWindowViewModel> _logger;
  private IScheduler? _scheduler;

  private bool _isSchedulerRunning;
  private int _totalJobsScheduled;
  private int _jobsExecutedToday;
  private int _failedJobsToday;
  private string _schedulerStatus = "Inicializando...";
  private DateTime _lastExecution = DateTime.MinValue;
  private bool _isAutostartEnabled;

  public event PropertyChangedEventHandler? PropertyChanged;

  public MainWindowViewModel(
    ISchedulerFactory schedulerFactory,
    IJobExecutionNotificationService notificationService,
    ILogger<MainWindowViewModel> logger)
  {
    _schedulerFactory = schedulerFactory;
    _notificationService = notificationService;
    _logger = logger;

    RecentExecutions = new ObservableCollection<JobExecutionViewModel>();

    PauseSchedulerCommand = new RelayCommand(async _ => await PauseSchedulerAsync(), _ => IsSchedulerRunning);
    ResumeSchedulerCommand = new RelayCommand(async _ => await ResumeSchedulerAsync(), _ => !IsSchedulerRunning);
    RefreshCommand = new RelayCommand(async _ => await RefreshDataAsync());

    _isAutostartEnabled = StartupHelper.IsRegisteredInStartup();

    // Suscribirse a cambios de ejecución
    _notificationService.JobExecutionChanged += OnJobExecutionChanged;
  }

  public bool IsSchedulerRunning
  {
    get => _isSchedulerRunning;
    set
    {
      if (_isSchedulerRunning != value)
      {
        _isSchedulerRunning = value;
        OnPropertyChanged();
        SchedulerStatus = value ? "En ejecución" : "Pausado";
        (PauseSchedulerCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (ResumeSchedulerCommand as RelayCommand)?.RaiseCanExecuteChanged();
      }
    }
  }

  public int TotalJobsScheduled
  {
    get => _totalJobsScheduled;
    set { _totalJobsScheduled = value; OnPropertyChanged(); }
  }

  public int JobsExecutedToday
  {
    get => _jobsExecutedToday;
    set { _jobsExecutedToday = value; OnPropertyChanged(); }
  }

  public int FailedJobsToday
  {
    get => _failedJobsToday;
    set { _failedJobsToday = value; OnPropertyChanged(); }
  }

  public string SchedulerStatus
  {
    get => _schedulerStatus;
    set { _schedulerStatus = value; OnPropertyChanged(); }
  }

  public DateTime LastExecution
  {
    get => _lastExecution;
    set { _lastExecution = value; OnPropertyChanged(); }
  }

  public bool IsAutostartEnabled
  {
    get => _isAutostartEnabled;
    set
    {
      if (_isAutostartEnabled != value)
      {
        _isAutostartEnabled = value;
        OnPropertyChanged();

        if (value)
          StartupHelper.RegisterInWindowsStartup();
        else
          StartupHelper.UnregisterFromWindowsStartup();
      }
    }
  }

  public ObservableCollection<JobExecutionViewModel> RecentExecutions { get; }

  public ICommand PauseSchedulerCommand { get; }
  public ICommand ResumeSchedulerCommand { get; }
  public ICommand RefreshCommand { get; }

  public async Task InitializeAsync()
  {
    try
    {
      _scheduler = await _schedulerFactory.GetScheduler();
      IsSchedulerRunning = _scheduler != null && !(_scheduler.InStandbyMode);

      await RefreshDataAsync();
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error al inicializar MainWindowViewModel");
      MessageBox.Show($"Error al inicializar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
  }

  private async Task PauseSchedulerAsync()
  {
    try
    {
      if (_scheduler != null)
      {
        await _scheduler.Standby();
        IsSchedulerRunning = false;
        _logger.LogInformation("Scheduler pausado manualmente");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error al pausar scheduler");
      MessageBox.Show($"Error al pausar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
  }

  private async Task ResumeSchedulerAsync()
  {
    try
    {
      if (_scheduler != null)
      {
        await _scheduler.Start();
        IsSchedulerRunning = true;
        _logger.LogInformation("Scheduler reanudado manualmente");
      }
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error al reanudar scheduler");
      MessageBox.Show($"Error al reanudar: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
    }
  }

  private async Task RefreshDataAsync()
  {
    try
    {
      if (_scheduler == null)
        return;

      var jobKeys = await _scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.AnyGroup());
      TotalJobsScheduled = jobKeys.Count;

      // Cargar historial reciente
      var recentNotifications = await _notificationService.GetRecentExecutionsAsync(20);

      System.Windows.Application.Current.Dispatcher.Invoke(() =>
      {
        RecentExecutions.Clear();
        foreach (var notification in recentNotifications.OrderByDescending(n => n.Timestamp))
        {
          RecentExecutions.Add(new JobExecutionViewModel(notification));
        }
      });

      // Calcular estadísticas del día
      var today = DateTime.Today;
      var todayExecutions = recentNotifications.Where(n => n.Timestamp.Date == today).ToList();
      JobsExecutedToday = todayExecutions.Count(n => n.State == JobExecutionState.Completed);
      FailedJobsToday = todayExecutions.Count(n => n.State == JobExecutionState.Failed);

      if (recentNotifications.Any())
        LastExecution = recentNotifications.Max(n => n.Timestamp);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error al refrescar datos");
    }
  }

  private void OnJobExecutionChanged(object? sender, JobExecutionNotification notification)
  {
    System.Windows.Application.Current.Dispatcher.Invoke(() =>
    {
      RecentExecutions.Insert(0, new JobExecutionViewModel(notification));

      // Mantener solo las últimas 20
      while (RecentExecutions.Count > 20)
        RecentExecutions.RemoveAt(RecentExecutions.Count - 1);

      // Actualizar estadísticas
      if (notification.Timestamp.Date == DateTime.Today)
      {
        if (notification.State == JobExecutionState.Completed)
          JobsExecutedToday++;
        else if (notification.State == JobExecutionState.Failed)
          FailedJobsToday++;
      }

      LastExecution = notification.Timestamp;
    });
  }

  private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
  {
    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
  }
}

public sealed class JobExecutionViewModel
{
  public JobExecutionViewModel(JobExecutionNotification notification)
  {
    JobName = notification.JobName;
    State = notification.State.ToString();
    Timestamp = notification.Timestamp;
    Message = notification.Message ?? string.Empty;
    Duration = notification.Duration.HasValue
      ? $"{notification.Duration.Value.TotalSeconds:F2}s"
      : "N/A";
    ExitCode = notification.ExitCode?.ToString() ?? "N/A";

    StateColor = notification.State switch
    {
      JobExecutionState.Completed => new SolidColorBrush(Color.FromRgb(39, 174, 96)),
      JobExecutionState.Failed => new SolidColorBrush(Color.FromRgb(231, 76, 60)),
      JobExecutionState.Starting => new SolidColorBrush(Color.FromRgb(52, 152, 219)),
      _ => new SolidColorBrush(Color.FromRgb(149, 165, 166))
    };
  }

  public string JobName { get; }
  public string State { get; }
  public DateTime Timestamp { get; }
  public string Message { get; }
  public string Duration { get; }
  public string ExitCode { get; }
  public Brush StateColor { get; }
}

public sealed class RelayCommand : ICommand
{
  private readonly Action<object?> _execute;
  private readonly Func<object?, bool>? _canExecute;

  public event EventHandler? CanExecuteChanged;

  public RelayCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
  {
    _execute = execute;
    _canExecute = canExecute;
  }

  public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

  public void Execute(object? parameter) => _execute(parameter);

  public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
