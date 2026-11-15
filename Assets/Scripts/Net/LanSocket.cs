using System;
using System.Collections.Concurrent;
using UnityEngine;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#else
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#endif

namespace HavenDuel.Net
{
    /// Minimal text WebSocket. Browser WebSocket on WebGL, ClientWebSocket elsewhere.
    public class LanSocket : IDisposable
    {
        public enum State { Connecting, Open, Closed }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void LanSocket_Open(string url);
        [DllImport("__Internal")] static extern int LanSocket_State();
        [DllImport("__Internal")] static extern void LanSocket_Send(string msg);
        [DllImport("__Internal")] static extern string LanSocket_Recv();
        [DllImport("__Internal")] static extern string LanSocket_HostName();
        [DllImport("__Internal")] static extern void LanSocket_Close();
        [DllImport("__Internal")] static extern int LanSocket_Secure();

        public static bool PageIsHttps => LanSocket_Secure() == 1;

        public static string PageHost => LanSocket_HostName();
        public LanSocket(string url) => LanSocket_Open(url);
        public State Status => LanSocket_State() switch { 1 => State.Open, 3 => State.Closed, _ => State.Connecting };
        public void Send(string msg) => LanSocket_Send(msg);
        public bool TryReceive(out string msg) { msg = LanSocket_Recv(); return msg != null; }
        public void Dispose() => LanSocket_Close();
#else
        public static string PageHost => "localhost";
        public static bool PageIsHttps => false;
        readonly ClientWebSocket ws = new();
        readonly CancellationTokenSource cts = new();
        readonly ConcurrentQueue<string> inbox = new();
        readonly SemaphoreSlim sendLock = new(1, 1);
        volatile State state = State.Connecting;

        public State Status => state;

        public LanSocket(string url) { _ = Run(url); }

        async Task Run(string url)
        {
            try
            {
                await ws.ConnectAsync(new Uri(url), cts.Token);
                state = State.Open;
                var buf = new byte[16 * 1024];
                var sb = new StringBuilder();
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    sb.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                    if (r.EndOfMessage) { inbox.Enqueue(sb.ToString()); sb.Clear(); }
                }
            }
            catch (Exception e) { if (!cts.IsCancellationRequested) Debug.LogWarning("LanSocket: " + e.Message); }
            state = State.Closed;
        }

        public async void Send(string msg)
        {
            if (state != State.Open) return;
            await sendLock.WaitAsync();
            try { await ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, cts.Token); }
            catch { }
            finally { sendLock.Release(); }
        }

        public bool TryReceive(out string msg) => inbox.TryDequeue(out msg);

        public void Dispose() { cts.Cancel(); ws.Dispose(); state = State.Closed; }
#endif
    }
}
