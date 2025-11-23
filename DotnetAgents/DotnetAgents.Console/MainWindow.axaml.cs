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


namespace DotnetAgents.Console
{
    public partial class MainWindow : Window
    {
        private readonly Agent? _agent;
        private readonly ITaskHubClient _taskHubClient;
        private readonly CancellationTokenSource _shutdownCts = new();
        private string _headerStatus;
        private string _connectionStatus;
        private const string Separator = "========================================";

        public MainWindow()
        {
            InitializeComponent();
            _headerStatus = "AGENT CHAT - Initializing...";
            _connectionStatus = "SignalR: Initializing";
            UpdateHeader();

            // Add keyboard shortcuts
            inputTextBox.KeyDown += InputTextBox_KeyDown;
            this.KeyDown += Window_KeyDown;

            // Initialize the agent
            try
            {
                //_agent = new Agent();
                _headerStatus = "AGENT CHAT - Ready";
                UpdateHeader();
                UpdateChatDisplay("Agent initialized successfully.\nType your message below and press Enter or click [S]end.\n\n");
            }
            catch (InvalidOperationException ex)
            {
                _headerStatus = "AGENT CHAT - Error";
                UpdateHeader();
                UpdateChatDisplay($"ERROR: Agent initialization failed\n\n" +
                                $"{ex.Message}\n\n" +
                                "Required environment variables:\n" +
                                "  - OPENAI_API_KEY\n" +
                                "  - OPENAI_MODEL_NAME\n" +
                                "  - OPENAI_ENDPOINT (optional)\n");
            }

            var baseUrl = TaskHubEndpointResolver.ResolveBaseUrl(configuration: null);
            _taskHubClient = new ConsoleTaskHubClient(baseUrl);
            _taskHubClient.ConnectionStateChanged += TaskHubClientOnConnectionStateChanged;
            _connectionStatus = "SignalR: Connecting";
            UpdateHeader();

            _ = MaintainSignalRConnectionAsync(_shutdownCts.Token);
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
            _shutdownCts.Cancel();
            var lifetime = Application.Current!.ApplicationLifetime as IControlledApplicationLifetime;
            lifetime!.Shutdown();
        }

        private void OnSend(object? sender, RoutedEventArgs e)
        {
            if (_agent == null)
            {
                AppendChatDisplay($"\n{Separator}\n" +
                                $"ERROR: Agent not initialized\n" +
                                $"{Separator}\n\n");
                _headerStatus = "AGENT CHAT - Error";
                UpdateHeader();
                return;
            }

            var userInput = inputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(userInput))
            {
                return;
            }

            // Update header to show working state
            _headerStatus = "AGENT CHAT - Processing...";
            UpdateHeader();

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

                // Remove "Processing..." and display response
                var textBlock = this.FindControl<TextBlock>("agentTextBox");
                if (textBlock != null && textBlock.Text != null)
                {
                    textBlock.Text = textBlock.Text.Replace("AGENT: [Processing...]\n", "");
                }
                //AppendChatDisplay($"AGENT:\n{response}\n\n");

                // Update header back to ready
                _headerStatus = "AGENT CHAT - Ready";
                UpdateHeader();
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
                _headerStatus = "AGENT CHAT - Error";
                UpdateHeader();
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
            _shutdownCts.Cancel();
            Task.Run(async () =>
            {
                try
                {
                    await _taskHubClient.StopAsync();
                }
                finally
                {
                    await _taskHubClient.DisposeAsync();
                }
            });

            base.OnClosed(e);
        }
    }
}