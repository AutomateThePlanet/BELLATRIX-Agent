using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Bellatrix.Cli.Tests;

/// <summary>
/// Minimal in-process stand-in for a WinAppDriver/Appium server, used to prove the attach-by-id
/// mounting path (<see cref="Bellatrix.Execution.DesktopSessionManager.MountExisting"/>) forwards
/// real commands over the wire without ever re-issuing <c>NewSession</c>. Matches responses by
/// request-path suffix rather than replicating the full W3C route table, so it stays robust to
/// exact route detail while still exercising a genuine HTTP round trip — no external
/// application or real Appium/WinAppDriver install required.
/// </summary>
public sealed class FakeAppiumServer : IDisposable
{
    private readonly HttpListener _listener;
    private readonly Thread _thread;
    private volatile bool _stop;

    public string BaseUrl { get; }
    public int NewSessionRequestCount;
    public bool ClickReceived;
    public bool ClearReceived;
    public bool TypeTextReceived;

    /// <summary>When true, every element-lookup POST responds with a W3C "no such element" 404 —
    /// used to exercise <c>DesktopSessionManager</c>'s window-lookup retry-exhaustion path.</summary>
    public bool AlwaysFailFindElement { get; set; }

    public FakeAppiumServer()
    {
        var port = GetFreeTcpPort();
        BaseUrl = $"http://127.0.0.1:{port}/";
        _listener = new HttpListener();
        _listener.Prefixes.Add(BaseUrl);
        _listener.Start();
        _thread = new Thread(Loop) { IsBackground = true };
        _thread.Start();
    }

    private static int GetFreeTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
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
                // best-effort fake server; swallow so the accept loop keeps running
            }
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var path = ctx.Request.Url!.AbsolutePath;
        var method = ctx.Request.HttpMethod;
        string json;
        var status = 200;

        if (method == "POST" && path.EndsWith("/session", StringComparison.Ordinal))
        {
            Interlocked.Increment(ref NewSessionRequestCount);
            json = "{\"value\":{\"sessionId\":\"unexpected-real-session\",\"capabilities\":{}}}";
        }
        else if (AlwaysFailFindElement && method == "POST" && path.EndsWith("/element", StringComparison.Ordinal))
        {
            status = 404;
            json = "{\"value\":{\"error\":\"no such element\",\"message\":\"no such element: Unable to locate element\",\"stacktrace\":\"\"}}";
        }
        else if (path.EndsWith("/click", StringComparison.Ordinal))
        {
            ClickReceived = true;
            json = "{\"value\":null}";
        }
        else if (path.EndsWith("/clear", StringComparison.Ordinal))
        {
            ClearReceived = true;
            json = "{\"value\":null}";
        }
        else if (path.EndsWith("/value", StringComparison.Ordinal) && method == "POST")
        {
            TypeTextReceived = true;
            json = "{\"value\":null}";
        }
        else if (path.EndsWith("/text", StringComparison.Ordinal))
        {
            json = "{\"value\":\"FakeElementText\"}";
        }
        else if (path.Contains("/attribute/", StringComparison.Ordinal))
        {
            json = "{\"value\":\"FakeAttributeValue\"}";
        }
        else if (path.EndsWith("/displayed", StringComparison.Ordinal))
        {
            json = "{\"value\":true}";
        }
        else if (path.EndsWith("/enabled", StringComparison.Ordinal))
        {
            json = "{\"value\":true}";
        }
        else if (path.EndsWith("/title", StringComparison.Ordinal))
        {
            json = "{\"value\":\"FakeWindowTitle\"}";
        }
        else if (path.EndsWith("/source", StringComparison.Ordinal))
        {
            json = "{\"value\":\"<Window/>\"}";
        }
        else if (path.EndsWith("/screenshot", StringComparison.Ordinal))
        {
            json = "{\"value\":\"" + Convert.ToBase64String(new byte[] { 1, 2, 3 }) + "\"}";
        }
        else if (method == "POST" && path.EndsWith("/element", StringComparison.Ordinal))
        {
            json = "{\"value\":{\"element-6066-11e4-a52e-4f735466cecf\":\"fake-element-1\"}}";
        }
        else
        {
            json = "{\"value\":null}";
        }

        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.StatusCode = status;
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
