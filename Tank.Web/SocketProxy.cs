using System;
using System.Linq;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using log4net;
using Microsoft.AspNetCore.Http;

namespace Tank.Web
{
    /// <summary>
    /// WebSocket -> TCP bridge for the Ruffle Flash emulator. Flash in a browser cannot open raw TCP
    /// sockets, so Ruffle sends the game socket (Road.Service, port 9500) over a WebSocket instead
    /// (Ruffle "socketProxy" config, binary frames = raw TCP bytes). Only the configured game host
    /// and ports are reachable, so this is not an open proxy.
    /// </summary>
    public static class SocketProxy
    {
        private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public static async Task Handle(HttpContext context, WebSettings settings)
        {
            if (!context.WebSockets.IsWebSocketRequest
                || !int.TryParse(context.Request.RouteValues["port"]?.ToString(), out int port)
                || !settings.SocketPorts.Contains(port))
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            using TcpClient tcp = new TcpClient { NoDelay = true };
            try
            {
                await tcp.ConnectAsync(settings.SocketTarget, port, context.RequestAborted);
            }
            catch (Exception ex)
            {
                log.WarnFormat("Socket proxy: cannot reach {0}:{1}: {2}", settings.SocketTarget, port, ex.Message);
                context.Response.StatusCode = StatusCodes.Status502BadGateway;
                return;
            }

            using WebSocket ws = await context.WebSockets.AcceptWebSocketAsync();
            log.InfoFormat("Socket proxy: {0} <-> {1}:{2} opened", context.Connection.RemoteIpAddress, settings.SocketTarget, port);
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            NetworkStream stream = tcp.GetStream();

            Task upstream = Task.Run(async () =>
            {
                byte[] buffer = new byte[16 * 1024];
                while (!cts.IsCancellationRequested)
                {
                    WebSocketReceiveResult result = await ws.ReceiveAsync(buffer, cts.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        break;
                    }
                    await stream.WriteAsync(buffer.AsMemory(0, result.Count), cts.Token);
                }
            });
            Task downstream = Task.Run(async () =>
            {
                byte[] buffer = new byte[16 * 1024];
                while (!cts.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer, cts.Token);
                    if (read == 0)
                    {
                        break;
                    }
                    await ws.SendAsync(buffer.AsMemory(0, read), WebSocketMessageType.Binary, true, cts.Token);
                }
            });

            try
            {
                await Task.WhenAny(upstream, downstream);
            }
            catch (Exception)
            {
            }
            cts.Cancel();
            try
            {
                if (ws.State == WebSocketState.Open)
                {
                    await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                }
            }
            catch (Exception)
            {
            }
            log.InfoFormat("Socket proxy: {0} <-> {1}:{2} closed", context.Connection.RemoteIpAddress, settings.SocketTarget, port);
        }

        /// <summary>The Ruffle config entry that routes the game socket through this proxy.</summary>
        public static string RuffleConfig(WebSettings settings)
        {
            string wsBase = settings.PublicBaseUrl.Replace("https://", "wss://").Replace("http://", "ws://");
            return "[" + string.Join(",", settings.SocketPorts.Select(p =>
                "{host:\"" + settings.SocketHost + "\",port:" + p + ",proxyUrl:\"" + wsBase + "/socket/" + p + "\"}")) + "]";
        }
    }
}
