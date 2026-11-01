using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net;
using System.Net.Sockets;
using System.Text;
using TaskMetricAI.Application;
using TaskMetricAI.Infrastructure;

namespace TaskMetricAI.Backend.Tests;

public sealed class JiraTelemetryTests
{
    [Fact]
    public async Task Production_transport_does_not_emit_sensitive_diagnostic_objects_headers_or_bodies()
    {
        // Exercise the real transport on loopback only. JiraCloudReader separately rejects every loopback API base.
        const string token = "telemetry-oauth-sentinel";
        const string body = "telemetry-body-sentinel";
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var server = new TcpListener(IPAddress.Loopback, 0); server.Start();
        var port = ((IPEndPoint)server.LocalEndpoint).Port;
        var serve = Serve();
        using var events = new HttpEvents(); using var diagnostics = new HttpDiagnostics();
        using var transport = JiraCloudReader.CreateTransport(); using var http = new HttpClient(transport);
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/jira-telemetry-probe");
        new JiraUserCredential(token).Apply(request);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        Assert.Equal(body, await response.Content.ReadAsStringAsync(timeout.Token));
        await serve;
        Assert.Empty(diagnostics.ProbeEvents);
        var emitted = string.Join('\n', events.Payloads);
        Assert.DoesNotContain(token, emitted); Assert.DoesNotContain(body, emitted);

        async Task Serve()
        {
            using var socket = await server.AcceptTcpClientAsync(timeout.Token);
            await using var stream = socket.GetStream();
            using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
            var headers = new List<string>();
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(timeout.Token))) headers.Add(line);
            Assert.Contains("Authorization: Bearer " + token, headers);
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: " +
                body.Length + "\r\nConnection: close\r\n\r\n" + body);
            await stream.WriteAsync(bytes, timeout.Token);
        }
    }

    private sealed class HttpEvents : EventListener
    {
        public ConcurrentQueue<string> Payloads { get; } = new();
        protected override void OnEventSourceCreated(EventSource source)
        { if (source.Name == "System.Net.Http") EnableEvents(source, EventLevel.Verbose); }
        protected override void OnEventWritten(EventWrittenEventArgs args)
        {
            // EventSource can invoke this during base construction before the queue is initialized.
            if (Payloads is not null) Payloads.Enqueue(string.Join('|', args.Payload?.Select(p => p?.ToString()) ?? []));
        }
    }

    private sealed class HttpDiagnostics : IObserver<DiagnosticListener>, IObserver<KeyValuePair<string, object?>>, IDisposable
    {
        public ConcurrentQueue<string> ProbeEvents { get; } = new();
        private readonly ConcurrentBag<IDisposable> subscriptions = new();
        private readonly IDisposable all;
        public HttpDiagnostics() => all = DiagnosticListener.AllListeners.Subscribe(this);
        public void OnNext(DiagnosticListener listener)
        { if (listener.Name == "HttpHandlerDiagnosticListener") subscriptions.Add(listener.Subscribe(this)); }
        public void OnNext(KeyValuePair<string, object?> value)
        {
            var request = value.Value?.GetType().GetProperty("Request")?.GetValue(value.Value) as HttpRequestMessage;
            if (request?.RequestUri?.AbsolutePath == "/jira-telemetry-probe") ProbeEvents.Enqueue(value.Key);
        }
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void Dispose() { all.Dispose(); foreach (var subscription in subscriptions) subscription.Dispose(); }
    }
}
