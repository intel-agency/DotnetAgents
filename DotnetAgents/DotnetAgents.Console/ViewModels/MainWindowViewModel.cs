using System.ComponentModel;
using System.Runtime.CompilerServices;
using IntelAgent;
using DotnetAgents.Console.Services;
using DotnetAgents.Core.Models;

namespace DotnetAgents.Console.ViewModels;



/// <summary>
/// ViewModel for the MainWindow - Contains all testable business logic
/// </summary>
public class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IAgent? _agent;
    private readonly ChatMessageFormatter _formatter;
    private readonly HeaderStatusService _headerService;
    
    private string _chatText;
    private string _inputText;
    private string _headerText;
    private bool _isBusy;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainWindowViewModel()
    {
        _formatter = new ChatMessageFormatter();
        _headerService = new HeaderStatusService();
        _chatText = string.Empty;
        _inputText = string.Empty;
        _headerText = _headerService.GetInitializingStatus();

        // Initialize agent
        try
        {
            //_agent = new Agent();
            HeaderText = _headerService.GetReadyStatus();
            ChatText = _formatter.FormatWelcomeMessage();
        }
        catch (InvalidOperationException ex)
        {
            HeaderText = _headerService.GetErrorStatus();
            ChatText = _formatter.FormatInitializationError(ex);
        }
    }

    // Constructor for testing with dependencies injected
    public MainWindowViewModel(IAgent agent, ChatMessageFormatter formatter, HeaderStatusService headerService)
    {
        _agent = agent;
        _formatter = formatter;
        _headerService = headerService;
        _chatText = string.Empty;
        _inputText = string.Empty;
        _headerText = _headerService.GetReadyStatus();
    }

    public string ChatText
    {
        get => _chatText;
        set
        {
            if (_chatText != value)
            {
                _chatText = value;
                OnPropertyChanged();
            }
        }
    }

    public string InputText
    {
        get => _inputText;
        set
        {
            if (_inputText != value)
            {
                _inputText = value;
                OnPropertyChanged();
            }
        }
    }

    public string HeaderText
    {
        get => _headerText;
        set
        {
            if (_headerText != value)
            {
                _headerText = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (_isBusy != value)
            {
                _isBusy = value;
                OnPropertyChanged();
            }
        }
    }

    public bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(InputText) && _agent != null;

    public async Task SendMessageAsync()
    {
        if (!CanSend)
            return;

        var userInput = InputText.Trim();
        InputText = string.Empty;

        // Update UI
        IsBusy = true;
        HeaderText = _headerService.GetProcessingStatus();
        ChatText += _formatter.FormatUserMessage(userInput);
        ChatText += _formatter.FormatThinkingMessage();

        try
        {
            // Call agent
            var request = new AgentResponseRequest { Prompt = userInput };
            var response = await _agent!.PromptAgentAsync(request);

            // Update with response
            ChatText = _formatter.RemoveThinkingMessage(ChatText);
            ChatText += _formatter.FormatAgentMessage(response);
            HeaderText = _headerService.GetReadyStatus();
        }
        catch (Exception ex)
        {
            // Handle error
            ChatText = _formatter.RemoveThinkingMessage(ChatText);
            ChatText += _formatter.FormatErrorMessage(ex.Message);
            HeaderText = _headerService.GetErrorStatus();
        }
        finally
        {
            IsBusy = false;
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
