using System.Net;
using System.Net.Sockets;
using System.Text;
using ModShardLauncher.Mods;

namespace ModShardLauncherTest;

/// <summary>
/// Covers the script-engine socket path (defect B4 was a busy wait while no data was available).
/// The test only verifies message delivery; the fix itself replaces polling with a blocking receive.
/// </summary>
public class ModInterfaceServerTests
{
    [Fact]
    public async Task ReceivesClientMessage()
    {
        int port = GetFreePort();
        Assert.True(ModInterfaceServer.TryStart(port, out string? error), error);

        TaskCompletionSource<string> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnMessage(string message) => received.TrySetResult(message);

        ModInterfaceServer.MessageReceived += OnMessage;
        try
        {
            using TcpClient client = new();
            await ConnectWithRetryAsync(client, port);

            byte[] body = Encoding.UTF8.GetBytes("[MSG]hello from the game");
            byte[] packet = BitConverter.GetBytes(0).Concat(body).ToArray();
            await client.GetStream().WriteAsync(packet);

            Task completed = await Task.WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(received.Task, completed);
            Assert.Contains("hello from the game", await received.Task);
        }
        finally
        {
            ModInterfaceServer.MessageReceived -= OnMessage;
            ModInterfaceServer.Stop();
        }
    }

    private static int GetFreePort()
    {
        using TcpListener listener = new(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static async Task ConnectWithRetryAsync(TcpClient client, int port)
    {
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                await client.ConnectAsync(IPAddress.Loopback, port);
                return;
            }
            catch (SocketException) when (attempt < 20)
            {
                await Task.Delay(50);
            }
        }
    }
}
