using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using DotnetAgents.Core.Interfaces;
using DotnetAgents.Core.SignalR;
using DotnetAgents.Console.Services;
using IntelAgent;
using Microsoft.Extensions.Logging;


namespace DotnetAgents.Console
{
    public partial class MainWindow : Window
    {
        private readonly Agent? _agent;
        private readonly ITaskHubClient _taskHubClient;
        private readonly CancellationTokenSource _shutdownCts = new();
        private readonly ILogger _logger;
        private Task? _connectionMaintenanceTask;
        private bool _shutdownInitiated;
        private string _headerStatus;
        private string _connectionStatus;
        private const string Separator = "========================================";

        public MainWindow()
        {
            InitializeComponent();
            _logger = new DebugLogger(nameof(MainWindow));
            _headerStatus = "AGENT CHAT - Initializing...";
            _connectionStatus = "SignalR: Initializing";
            UpdateHeader();

            // Add keyboard shortcuts
            inputTextBox.KeyDown += InputTextBox_KeyDown;
            this.KeyDown += Window_KeyDown;

            var agentInitialized = true;

            // Initialize the agent
            try
            {
                //_agent = new Agent(...);
            }
            catch (InvalidOperationException ex)
            {
                agentInitialized = false;
                DisableAgentInteractions($"ERROR: Agent initialization failed\n\n" +
                                         $"{ex.Message}\n\n" +
                                         "Required environment variables:\n" +
                                         "  - OPENAI_API_KEY\n" +
                                         "  - OPENAI_MODEL_NAME\n" +
                                         "  - OPENAI_ENDPOINT (optional)\n");
            }
            catch (Exception ex)
            {
                agentInitialized = false;
                DisableAgentInteractions("Agent initialization failed unexpectedly. Check logs for details.");
                _logger.LogError(ex, "Agent initialization failed.");
            }

            if (_agent is not null && agentInitialized)
            {
                SetHeaderStatus("AGENT CHAT - Ready");
                UpdateChatDisplay("Agent initialized successfully.\nType your message below and press Enter or click [S]end.\n\n");
            }
            else
            {
                SetHeaderStatus("AGENT CHAT - Unavailable");
                if (!_shutdownInitiated && sendButton is { IsEnabled: true })
                {
                    DisableAgentInteractions("Agent chat is disabled. Configure the agent service to enable conversations.");
                }
            }

            var baseUrl = TaskHubEndpointResolver.ResolveBaseUrl(configuration: null);
            _taskHubClient = new ConsoleTaskHubClient(baseUrl);
            _taskHubClient.ConnectionStateChanged += TaskHubClientOnConnectionStateChanged;
            _connectionStatus = "SignalR: Connecting";
            UpdateHeader();

            _connectionMaintenanceTask = MaintainSignalRConnectionAsync(_shutdownCts.Token);
            ObserveTask(_connectionMaintenanceTask, "SignalR connection maintenance");
        }

        private void UpdateHeader()
        {
            // Find the header TextBlock in the Border
            var border = this.FindControl<Border>("HeaderBorder");
            if (border?.Child is TextBlock textBlock)
            {
                textBlock.Text = $"{_headerStatus} | {_connectionStatus}";
            }
        }

        private void UpdateChatDisplay(string text)
        {
            var textBlock = this.FindControl<TextBlock>("agentTextBox");
            if (textBlock != null)
            {
                textBlock.Text = text;
                ScrollToBottom();
            }
        }

        private void AppendChatDisplay(string text)
        {
            var textBlock = this.FindControl<TextBlock>("agentTextBox");
            if (textBlock != null)
            {
                textBlock.Text += text;
                ScrollToBottom();
            }
        }

        private void ScrollToBottom()
        {
            var scrollViewer = this.FindControl<ScrollViewer>("chatScrollViewer");
            scrollViewer?.ScrollToEnd();
        }

        private void Window_KeyDown(object? sender, KeyEventArgs e)
        {
            // Global hotkeys (Alt+S for Send, Alt+E for Exit)
            if (e.KeyModifiers == KeyModifiers.Alt)
            {
                if (e.Key == Key.S)
                {
                    e.Handled = true;
                    OnSend(sender, new RoutedEventArgs());
                }
                else if (e.Key == Key.E)
                {
                    e.Handled = true;
                    OnExit(sender, new RoutedEventArgs());
                }
            }
        }

        private void InputTextBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                OnSend(sender, new RoutedEventArgs());
            }
        }

        private void OnExit(object? sender, RoutedEventArgs e)
        {
            InitiateShutdown();
            var lifetime = Application.Current!.ApplicationLifetime as IControlledApplicationLifetime;
            lifetime!.Shutdown();
        }

        private async void OnSend(object? sender, RoutedEventArgs e)
        {
            if (_agent == null)
            {
                AppendChatDisplay($"\n{Separator}\nAgent chat is not configured. Configure the agent service to enable conversations.\n{Separator}\n\n");
                return;
            }

            var userInput = inputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(userInput))
            {
                return;
            }

            // Update header to show working state
            SetHeaderStatus("AGENT CHAT - Processing...");

            // Display user message
            AppendChatDisplay($"{Separator}\n");
            AppendChatDisplay($"YOU:\n{userInput}\n\n");

            // Clear input box
            inputTextBox.Text = string.Empty;

            // Show "thinking" indicator
            AppendChatDisplay($"AGENT: [Processing...]\n");

            try
            {
                // Call the agent
                //var request = new AgentResponseRequest { Prompt = userInput };
                //var response = await _agent.PromptAgentAsync(request);
                await Task.CompletedTask;

                // Remove "Processing..." and display response
                var textBlock = this.FindControl<TextBlock>("agentTextBox");
                if (textBlock != null && textBlock.Text != null)
                {
                    textBlock.Text = textBlock.Text.Replace("AGENT: [Processing...]\n", "");
                }
                //AppendChatDisplay($"AGENT:\n{response}\n\n");

                // Update header back to ready
                SetHeaderStatus("AGENT CHAT - Ready");
            }
            catch (Exception ex)
            {
                var textBlock = this.FindControl<TextBlock>("agentTextBox");
                if (textBlock != null && textBlock.Text != null)
                {
                    textBlock.Text = textBlock.Text.Replace("AGENT: [Processing...]\n", "");
                }
                AppendChatDisplay($"ERROR:\n{ex.Message}\n\n");

                // Update header to show error
                SetHeaderStatus("AGENT CHAT - Error");
                _logger.LogError(ex, "Agent interaction failed.");
            }
        }

        private async Task MaintainSignalRConnectionAsync(CancellationToken cancellationToken)
        {
            var delay = TimeSpan.FromSeconds(2);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await _taskHubClient.StartAsync(cancellationToken);
                    return;
                }
                catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
                {
                    var retryMessage = $"SignalR connection failed ({ex.Message}). Retrying in {delay.TotalSeconds:0}s";
                    Dispatcher.UIThread.Post(() =>
                    {
                        AppendChatDisplay($"\n{Separator}\n{retryMessage}\n{Separator}\n");
                        _connectionStatus = $"SignalR: Retrying";
                        UpdateHeader();
                    });
                    _logger.LogWarning(ex, "SignalR connection failed. Retrying in {DelaySeconds} seconds.", delay.TotalSeconds);

                    try
                    {
                        await Task.Delay(delay, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }

                    delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 30));
                }
            }
        }

        private void TaskHubClientOnConnectionStateChanged(object? sender, TaskHubConnectionStateChangedEventArgs e)
        {
            Dispatcher.UIThread.Post(() =>
            {
                _connectionStatus = e.NewState switch
                {
                    TaskHubConnectionState.Connected => "SignalR: Connected",
                    TaskHubConnectionState.Connecting => "SignalR: Connecting",
                    TaskHubConnectionState.Reconnecting => "SignalR: Reconnecting",
                    _ => "SignalR: Disconnected"
                };
                UpdateHeader();

                if (e.Exception is not null)
                {
                    AppendChatDisplay($"\n{Separator}\nSignalR connection update: {e.Exception.Message}\n{Separator}\n");
                }
            });
        }

        protected override void OnClosed(EventArgs e)
        {
            InitiateShutdown();
            base.OnClosed(e);
        }

        private void InitiateShutdown()
        {
            if (_shutdownInitiated)
            {
                return;
            }

            _shutdownInitiated = true;
            _shutdownCts.Cancel();
            ObserveTask(ShutdownAsync(), "Console shutdown");
        }

        private async Task ShutdownAsync()
        {
            try
            {
                await _taskHubClient.StopAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop TaskHub client during shutdown.");
            }
            finally
            {
                try
                {
                    await _taskHubClient.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to dispose TaskHub client.");
                }

                if (_connectionMaintenanceTask is not null)
                {
                    try
                    {
                        await _connectionMaintenanceTask.ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Connection maintenance task failed during shutdown.");
                    }
                }

                _shutdownCts.Dispose();
            }
        }

        private void ObserveTask(Task task, string operation)
        {
            task.ContinueWith(t =>
            {
                var exception = t.Exception?.Flatten();
                _logger.LogError(exception ?? new Exception("Unknown error"), "Background operation '{Operation}' faulted.", operation);
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private void SetHeaderStatus(string status)
        {
            _headerStatus = status;
            UpdateHeader();
        }

        private void DisableAgentInteractions(string message)
        {
            AppendChatDisplay($"\n{Separator}\n{message}\n{Separator}\n");
            if (inputTextBox is not null)
            {
                inputTextBox.IsEnabled = false;
            }

            if (sendButton is not null)
            {
                sendButton.IsEnabled = false;
            }
        }

        private sealed class DebugLogger : ILogger
        {
            private readonly string _categoryName;

            public DebugLogger(string categoryName)
            {
                _categoryName = categoryName;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var message = formatter(state, exception);
                Debug.WriteLine($"[{_categoryName}] {logLevel}: {message}");
                if (exception is not null)
                {
                    Debug.WriteLine(exception);
                }
            }
        }
    }
}