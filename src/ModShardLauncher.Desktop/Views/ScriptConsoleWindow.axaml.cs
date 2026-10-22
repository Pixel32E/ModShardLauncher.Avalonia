using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ModShardLauncher.Mods;

namespace ModShardLauncher.Desktop.Views;

/// <summary>
/// Live view of the mod interface socket protocol. It replaces the old WPF window that was
/// created from inside the static server, without coupling the server to the UI.
/// </summary>
public partial class ScriptConsoleWindow : Window
{
    private readonly System.Collections.ObjectModel.ObservableCollection<string> _messages = new();

    public ScriptConsoleWindow()
    {
        InitializeComponent();
        MessageList.ItemsSource = _messages;

        ModInterfaceServer.MessageReceived += OnMessage;
        ModInterfaceServer.HookReceived += OnHook;
        Closed += (_, _) =>
        {
            ModInterfaceServer.MessageReceived -= OnMessage;
            ModInterfaceServer.HookReceived -= OnHook;
        };
    }

    private void OnMessage(string message) =>
        Dispatcher.UIThread.Post(() => Append(message));

    private void OnHook(string name, string[] parameters) =>
        Dispatcher.UIThread.Post(() => Append($"[HOOK] {name} ({string.Join(", ", parameters)})"));

    private void Append(string message)
    {
        _messages.Add(message);
        if (MessageList.ItemCount > 0)
        {
            MessageList.ScrollIntoView(MessageList.ItemCount - 1);
        }
    }

    private void Send_Click(object? sender, RoutedEventArgs e) => SendCurrent();

    private void Input_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            SendCurrent();
            e.Handled = true;
        }
    }

    private void SendCurrent()
    {
        string script = InputBox.Text?.Trim() ?? string.Empty;
        if (script.Length == 0)
        {
            return;
        }

        if (!ModInterfaceServer.IsConnected)
        {
            Append("[launcher] No game client is connected.");
            return;
        }

        ModInterfaceServer.SendScript(script);
        Append("> " + script);
        InputBox.Text = string.Empty;
    }
}
