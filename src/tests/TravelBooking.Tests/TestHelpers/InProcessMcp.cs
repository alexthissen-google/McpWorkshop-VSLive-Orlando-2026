using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using TravelBooking.MCP.Resources;

namespace TravelBooking.Tests.TestHelpers;

/// <summary>
/// An MCP server and a connected <see cref="McpClient"/> in one process, exchanging
/// JSON-RPC messages over a pair of channels. Every message is serialized and parsed again
/// with the SDK's own options, so tests see what a client sees on the wire. The server is
/// built from the same three assembly scans as the Host (tools, resources, prompts),
/// without the HTTP transport or the authorization filters.
/// </summary>
public sealed class InProcessMcp : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private readonly CancellationTokenSource _serverCts;
    private readonly Task _serverRun;
    private readonly Channel<JsonRpcMessage> _clientToServer;
    private readonly Channel<JsonRpcMessage> _serverToClient;

    private InProcessMcp(
        ServiceProvider services, McpClient client, CancellationTokenSource serverCts, Task serverRun,
        Channel<JsonRpcMessage> clientToServer, Channel<JsonRpcMessage> serverToClient)
    {
        _services = services;
        Client = client;
        _serverCts = serverCts;
        _serverRun = serverRun;
        _clientToServer = clientToServer;
        _serverToClient = serverToClient;
    }

    public McpClient Client { get; }

    public IServiceProvider Services => _services;

    public static async Task<InProcessMcp> StartAsync(Action<IServiceCollection>? configure = null)
    {
        // The resource exists at every lab state; the tool classes only from the end of Lab 1.
        var assembly = typeof(BaggagePolicyResource).Assembly;
        var services = new ServiceCollection();
        configure?.Invoke(services);
        services.AddMcpServer()
            .WithToolsFromAssembly(assembly)
            .WithResourcesFromAssembly(assembly)
            .WithPromptsFromAssembly(assembly);
        var provider = services.BuildServiceProvider();

        var clientToServer = Channel.CreateUnbounded<JsonRpcMessage>();
        var serverToClient = Channel.CreateUnbounded<JsonRpcMessage>();

        var server = McpServer.Create(
            new ChannelTransport(outbound: serverToClient.Writer, inbound: clientToServer.Reader),
            provider.GetRequiredService<IOptions<McpServerOptions>>().Value,
            loggerFactory: null,
            provider);
        var cts = new CancellationTokenSource();
        var run = server.RunAsync(cts.Token);

        try
        {
            using var connectTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var connect = McpClient.CreateAsync(
                new ChannelClientTransport(new ChannelTransport(outbound: clientToServer.Writer, inbound: serverToClient.Reader)),
                cancellationToken: connectTimeout.Token);
            if (await Task.WhenAny(connect, run) == run)
            {
                await run; // surfaces the server's exception, if it faulted
                throw new InvalidOperationException("The in-process MCP server stopped before the client connected.");
            }

            var client = await connect;
            return new InProcessMcp(provider, client, cts, run, clientToServer, serverToClient);
        }
        catch
        {
            // A failed handshake must not leave the server loop parked on the channel.
            cts.Cancel();
            clientToServer.Writer.TryComplete();
            serverToClient.Writer.TryComplete();
            try { await run.WaitAsync(TimeSpan.FromSeconds(10)); } catch { /* reported via the original exception */ }
            cts.Dispose();
            await provider.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        // Stop the server and close both directions first, so the client's read loop
        // ends regardless of who owns which transport.
        _serverCts.Cancel();
        _clientToServer.Writer.TryComplete();
        _serverToClient.Writer.TryComplete();
        try
        {
            try { await _serverRun.WaitAsync(TimeSpan.FromSeconds(10)); } catch (OperationCanceledException) { }
            await Client.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            _serverCts.Dispose();
            await _services.DisposeAsync();
        }
    }

    private sealed class ChannelClientTransport(ITransport session) : IClientTransport
    {
        public string Name => "in-process";

        public Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(session);
    }

    private sealed class ChannelTransport(ChannelWriter<JsonRpcMessage> outbound, ChannelReader<JsonRpcMessage> inbound) : ITransport
    {
        public string? SessionId => null;

        public ChannelReader<JsonRpcMessage> MessageReader => inbound;

        public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
        {
            var json = JsonSerializer.Serialize(message, McpJsonUtilities.DefaultOptions);
            var copy = JsonSerializer.Deserialize<JsonRpcMessage>(json, McpJsonUtilities.DefaultOptions)
                ?? throw new InvalidOperationException("JSON-RPC message did not round-trip: " + json);
            await outbound.WriteAsync(copy, cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            outbound.TryComplete();
            return ValueTask.CompletedTask;
        }
    }
}
