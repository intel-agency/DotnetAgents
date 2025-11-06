using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using IntelAgent;
using IntelAgent.Model;

namespace DotnetAgents.Console
{
    public partial class MainWindow : Window
    {
        private readonly Agent? _agent;
        private const string Separator = "========================================";

        public MainWindow()
        {
            InitializeComponent();
            
            // Add keyboard shortcuts
            inputTextBox.KeyDown += InputTextBox_KeyDown;
            this.KeyDown += Window_KeyDown;
            
            // Initialize the agent
            try
            {
                _agent = new Agent();
                UpdateHeader("AGENT CHAT - Ready");
                UpdateChatDisplay("Agent initialized successfully.\nType your message below and press Enter or click [S]end.\n\n");
            }
            catch (InvalidOperationException ex)
            {
                UpdateHeader("AGENT CHAT - Error");
                UpdateChatDisplay($"ERROR: Agent initialization failed\n\n" +
                                $"{ex.Message}\n\n" +
                                "Required environment variables:\n" +
                                "  - OPENAI_API_KEY\n" +
                                "  - OPENAI_MODEL_NAME\n" +
                                "  - OPENAI_ENDPOINT (optional)\n");
            }
        }

        private void UpdateHeader(string text)
        {
            // Find the header TextBlock in the Border
            var border = this.FindControl<Border>("HeaderBorder");
            if (border?.Child is TextBlock textBlock)
            {
                textBlock.Text = text;
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

        private void OnExit(object sender, RoutedEventArgs e)
        {
            var lifetime = Application.Current!.ApplicationLifetime as IControlledApplicationLifetime;
            lifetime!.Shutdown();
        }

        private async void OnSend(object? sender, RoutedEventArgs e)
        {
            if (_agent == null)
            {
                AppendChatDisplay($"\n{Separator}\n" +
                                $"ERROR: Agent not initialized\n" +
                                $"{Separator}\n\n");
                return;
            }

            var userInput = inputTextBox.Text?.Trim();
            if (string.IsNullOrEmpty(userInput))
            {
                return;
            }

            // Update header to show working state
            UpdateHeader("AGENT CHAT - Processing...");

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
                var request = new AgentResponseRequest { Prompt = userInput };
                var response = await _agent.PromptAgentAsync(request);
                
                // Remove "Processing..." and display response
                var textBlock = this.FindControl<TextBlock>("agentTextBox");
                if (textBlock != null && textBlock.Text != null)
                {
                    textBlock.Text = textBlock.Text.Replace("AGENT: [Processing...]\n", "");
                }
                AppendChatDisplay($"AGENT:\n{response}\n\n");
                
                // Update header back to ready
                UpdateHeader("AGENT CHAT - Ready");
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
                UpdateHeader("AGENT CHAT - Error");
            }
        }
    }
}