using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Minimal in-process W3C WebDriver server standing in for a Selenium standalone/Grid or a detached
/// chromedriver, used to prove that <c>bellatrix web</c> verbs run in SEPARATE invocations re-attach to the
/// session created by <c>start-session</c> by id — exactly one <c>NewSession</c>, then every later command
/// addressed to that same session id. Mirrors <see cref="FakeAppiumServer"/>; records every request.
/// </summary>
public sealed class FakeSeleniumServer : IDisposable
{
    public const string SessionId = "fake-selenium-session-1";

    /// <summary>The PNG signature + IHDR chunk header: enough for the CLI to read the image size.</summary>
    public static byte[] PngHeader(int width, int height)
    {
        var bytes = new byte[33];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' }.CopyTo(bytes, 0);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), (uint)width);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), (uint)height);
        return bytes;
    }

    private readonly HttpListener _listener;
    private readonly Thread _thread;
    private volatile bool _stop;

    public string BaseUrl { get; }

    /// <summary>Every request as <c>"METHOD /path body"</c>, in arrival order.</summary>
    public ConcurrentQueue<string> Requests { get; } = new();

    public int NewSessionRequestCount;

    public FakeSeleniumServer()
    {
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start();
        var port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();

        BaseUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        _thread = new Thread(Loop) { IsBackground = true };
        _thread.Start();
    }

    private void Loop()
    {
        while (!_stop)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = _listener.GetContext();
            }
            catch (Exception)
            {
                return;
            }

            try
            {
                Handle(ctx);
            }
            catch (Exception)
            {
                // best-effort fake server; keep accepting
            }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var method = ctx.Request.HttpMethod;
        string body;
        using (var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
        {
            body = reader.ReadToEnd();
        }

        Requests.Enqueue($"{method} {path} {body}".TrimEnd());

        string json;
        if (method == "POST" && path.TrimEnd('/').EndsWith("/session", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref NewSessionRequestCount);
            json = "{\"value\":{\"sessionId\":\"" + SessionId + "\",\"capabilities\":{\"browserName\":\"chrome\"}}}";
        }
        else if (method == "GET" && path.EndsWith("/title", StringComparison.Ordinal))
        {
            json = "{\"value\":\"Fake Shop\"}";
        }
        else if (method == "GET" && path.EndsWith("/url", StringComparison.Ordinal))
        {
            json = "{\"value\":\"https://example.test/\"}";
        }
        else if (method == "POST" && path.EndsWith("/element", StringComparison.Ordinal))
        {
            json = "{\"value\":{\"element-6066-11e4-a52e-4f735466cecf\":\"el-1\"}}";
        }
        else if (method == "POST" && path.EndsWith("/elements", StringComparison.Ordinal))
        {
            json = "{\"value\":[{\"element-6066-11e4-a52e-4f735466cecf\":\"el-1\"}]}";
        }
        else if (path.EndsWith("/displayed", StringComparison.Ordinal) || path.EndsWith("/enabled", StringComparison.Ordinal))
        {
            json = "{\"value\":true}";
        }
        else if (path.EndsWith("/text", StringComparison.Ordinal))
        {
            json = "{\"value\":\"Falcon 9\"}";
        }
        else if (path.EndsWith("/rect", StringComparison.Ordinal))
        {
            json = "{\"value\":{\"x\":0,\"y\":0,\"width\":1920,\"height\":1080}}";
        }
        else if (method == "GET" && path.EndsWith("/screenshot", StringComparison.Ordinal))
        {
            json = "{\"value\":\"" + Convert.ToBase64String(PngHeader(1280, 720)) + "\"}";
        }
        else if (path.EndsWith("/execute/sync", StringComparison.Ordinal))
        {
            json = "{\"value\":\"complete\"}";
        }
        else
        {
            json = "{\"value\":null}";
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = 200;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        ctx.Response.ContentLength64 = bytes.Length;
        ctx.Response.OutputStream.Write(bytes, 0, bytes.Length);
        ctx.Response.OutputStream.Close();
    }

    public void Dispose()
    {
        _stop = true;
        try { _listener.Stop(); } catch (Exception) { /* best-effort teardown */ }
        try { _listener.Close(); } catch (Exception) { /* best-effort teardown */ }
    }
}
