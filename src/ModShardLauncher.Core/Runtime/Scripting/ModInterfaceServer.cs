using System.Dynamic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using Serilog;

namespace ModShardLauncher.Mods;

/// <summary>
/// Local socket bridge used by the "mod interface" feature: a running game connects to the
/// launcher and exchanges <c>[MSG]</c> / <c>[CLB]</c> / <c>[HOK]</c> messages.
/// <para>
/// The WPF implementation pushed directly into a window and a WPF Dispatcher. This version raises
/// events instead, so the UI (or a test) can decide how to display the traffic.
/// </para>
/// </summary>
public static class ModInterfaceServer
{
    private static Socket? _server;
    private static Socket? _client;

    public static Socket? Server => _server;

    public static Socket? Client => _client;

    public static bool IsRunning => _server is not null;

    public static bool IsConnected => _client is { Connected: true };

    /// <summary>Raised for every <c>[MSG]</c> line received from the game.</summary>
    public static event Action<string>? MessageReceived;

    /// <summary>Raised when the game reports a <c>[HOK]</c> hook.</summary>
    public static event Action<string, string[]>? HookReceived;

    public static bool TryStart(int port, out string? error)
    {
        error = null;
        if (_server is not null)
        {
            error = "Mod Interface Server already open.";
            return false;
        }

        try
        {
            IPEndPoint endpoint = new(IPAddress.Loopback, port);
            Socket server = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            server.Bind(endpoint);
            _server = server;

            Thread listener = new(() => Listen(server))
            {
                IsBackground = true,
                Name = "ModInterfaceServer",
            };
            listener.Start();
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Cannot start the mod interface server");
            error = ex.Message;
            _server = null;
            return false;
        }
    }

    public static void Stop()
    {
        try
        {
            _client?.Close();
        }
        catch
        {
            // Already gone.
        }

        try
        {
            _server?.Close();
        }
        catch
        {
            // Already gone.
        }

        _client = null;
        _server = null;
    }

    /// <summary>Appends a line to the script console.</summary>
    public static void Report(string message) => MessageReceived?.Invoke(message);

    public static void SendScript(string script, Action<string> callback)
    {
        if (_client is null)
        {
            return;
        }

        byte[] payload = BitConverter.GetBytes(script.Length)
            .Concat(BitConverter.GetBytes((int)ModInterfaceEngine.Instance.CurrentCallbackID))
            .Concat(Encoding.Unicode.GetBytes(script))
            .ToArray();
        _client.Send(payload);

        ModInterfaceEngine.Instance.Callbacks.Add(ModInterfaceEngine.Instance.CurrentCallbackID, callback);
        ModInterfaceEngine.Instance.CurrentCallbackID++;
        if (ModInterfaceEngine.Instance.CurrentCallbackID >= int.MaxValue / 4)
        {
            ModInterfaceEngine.Instance.CurrentCallbackID = 0;
        }
    }

    public static void SendScript(string script)
    {
        string name = script.Split(' ')[0];
        SendScript(script, MslContext.ScriptCallbacks.ContainsKey(name)
            ? MslContext.ScriptCallbacks[name]
            : _ => { });
    }

    private static void Listen(Socket server)
    {
        server.Listen(0);
        server.BeginAccept(AcceptCallback, server);
    }

    private static void AcceptCallback(IAsyncResult result)
    {
        if (result.AsyncState is not Socket server || _server is null)
        {
            return;
        }

        Socket client;
        try
        {
            client = server.EndAccept(result);
        }
        catch (ObjectDisposedException)
        {
            return;
        }
        catch (SocketException)
        {
            return;
        }

        _client = client;
        try
        {
            byte[] received = new byte[1024 * 64];
            while (true)
            {
                int read;
                try
                {
                    // Blocking receive on the dedicated listener thread. This replaces the old
                    // Available == 0 spin loop (defect B4) with a proper wait.
                    read = client.Receive(received, 0, received.Length, SocketFlags.None);
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }

                if (read <= 0)
                {
                    break;
                }

                if (read < 4)
                {
                    continue;
                }

                int callbackId = BitConverter.ToInt32(received, 0);
                string message = Encoding.UTF8.GetString(received, 4, read - 4).Replace("\0", "");

                if (message.StartsWith("[MSG]"))
                {
                    Report("[Client]: " + message.Replace("[MSG]", ""));
                }
                else if (message.StartsWith("[CLB]"))
                {
                    string data = message.Replace("[CLB]", "");
                    if (ModInterfaceEngine.Instance.Callbacks.TryGetValue(callbackId, out Action<string>? callback))
                    {
                        callback(data);
                        ModInterfaceEngine.Instance.Callbacks.Remove(callbackId);
                    }
                }
                else if (message.StartsWith("[HOK]"))
                {
                    string[] data = message.Replace("[HOK]", "").Split("<BUILTIN>");
                    string[] hookInfo = data[0].Split("<EXTRAMSG>");
                    string[] parameters = hookInfo.Length > 1 ? hookInfo[1].Split(" ") : Array.Empty<string>();
                    if (data.Length > 1 && data[1] == "1")
                    {
                        ModInterfaceEngine.Instance.DoHook(data[0], parameters);
                    }
                }
            }
        }
        finally
        {
            try
            {
                client.Close();
            }
            catch
            {
                // Already gone.
            }

            if (ReferenceEquals(_client, client))
            {
                _client = null;
            }
        }
    }
}

/// <summary>Dynamic proxy exposed to mods as <c>ModInterfaceEngine.Instance</c>.</summary>
public class ModInterfaceEngine : EngineObject
{
    public static dynamic Instance = new ModInterfaceEngine();

    public string ModPath = string.Empty;
    public int CurrentCallbackID = 0;
    public bool IsLoadHooks = false;

    public ModInterfaceEngine()
    {
        Variables = new Dictionary<string, EngineObject>();
        HookDelegates = new Dictionary<string, ModDelegate>();
        Callbacks = new Dictionary<int, Action<string>>();
        Initalize();
    }

    public void Initalize()
    {
        HookDelegates.Add("OnGameStart", new ModDelegate());
        HookDelegates["OnGameStart"] += _ =>
        {
            ModInterfaceServer.Report("[Client]: StoneShard connect successfully.");
        };
    }

    public Dictionary<string, EngineObject> Variables;

    public Dictionary<string, ModDelegate> HookDelegates;

    public Dictionary<int, Action<string>> Callbacks;

    public void Execute(string script, params object[] objects)
    {
        if (!ModInterfaceServer.IsConnected)
        {
            return;
        }

        string code = script;
        objects.ToList().ForEach(o => code += " " + o);
        ModInterfaceServer.SendScript(code);
    }

    public void DoHook(string name, object[] parameters)
    {
        if (HookDelegates.TryGetValue(name, out ModDelegate? hook))
        {
            hook.Invoke(parameters);
        }
    }

    public void AddHook(string hook, Action<object[]> action)
    {
        if (HookDelegates.ContainsKey(hook))
        {
            HookDelegates[hook] += action;
        }
    }
}

/// <summary>A simple multicast delegate wrapper exposed to the dynamic engine object.</summary>
public class ModDelegate : EngineObject
{
    private readonly List<Action<object[]>> _actions = new();

    public static ModDelegate operator +(ModDelegate @delegate, Action<object[]> action)
    {
        @delegate._actions.Add(action);
        return @delegate;
    }

    public void Invoke(object[] obj)
    {
        foreach (Action<object[]> action in _actions)
        {
            action.Invoke(obj);
        }
    }
}

/// <summary>Dynamic object used by the script engine so GML-side calls look like C# properties.</summary>
public class EngineObject : DynamicObject
{
    private readonly Dictionary<string, object> _values;

    public EngineObject() => _values = new Dictionary<string, object>();

    public object? GetPropertyValue(string propertyName) =>
        _values.TryGetValue(propertyName, out object? value) ? value : null;

    public void SetPropertyValue(string propertyName, object value)
    {
        if (_values.ContainsKey(propertyName))
        {
            _values[propertyName] = value;
        }
        else
        {
            _values.Add(propertyName, value);
        }
    }

    public override bool TryGetMember(GetMemberBinder binder, out object? result)
    {
        result = GetPropertyValue(binder.Name);
        return result is not null;
    }

    public override bool TrySetMember(SetMemberBinder binder, object? value)
    {
        if (value is not null)
        {
            SetPropertyValue(binder.Name, value);
        }

        return true;
    }

    public override bool TryInvokeMember(InvokeMemberBinder binder, object?[]? args, out object? result)
    {
        result = null;
        object? member = GetPropertyValue(binder.Name);
        if (member is null)
        {
            return false;
        }

        if (member.GetType() == typeof(Action<object[]>))
        {
            object?[] actualArgs = args ?? Array.Empty<object?>();
            for (int i = 0; i < actualArgs.Length; i++)
            {
                if (actualArgs[i]?.GetType() == typeof(string))
                {
                    actualArgs[i] = "\"" + actualArgs[i]!.ToString()!.Replace(" ", "_") + "\"";
                }
            }

            ((Action<object[]>)member)(actualArgs!);
            return true;
        }

        return false;
    }
}
