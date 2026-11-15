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

namespace HavenDuel
{
    public class Relay : IDisposable
    {
        public enum State { Connecting, Open, Closed }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void Relay_Open(string url);
        [DllImport("__Internal")] static extern int Relay_State();
        [DllImport("__Internal")] static extern void Relay_Send(string msg);
        [DllImport("__Internal")] static extern string Relay_Recv();
        [DllImport("__Internal")] static extern void Relay_Close();
        [DllImport("__Internal")] static extern string Relay_PageHost();

        public static string PageHost => Relay_PageHost();
        public static bool PageIsHttps => Application.absoluteURL.StartsWith("https");

        public Relay(string url) => Relay_Open(url);
        public State Status => (State)Relay_State();
        public void Send(string msg) => Relay_Send(msg);
        public bool TryReceive(out string msg) => (msg = Relay_Recv()) != null;
        public void Dispose() => Relay_Close();
#else
        public static string PageHost => "localhost";
        public static bool PageIsHttps => false;

        readonly ClientWebSocket ws = new();
        readonly CancellationTokenSource cts = new();
        readonly ConcurrentQueue<string> inbox = new();
        readonly BlockingCollection<string> outbox = new();

        public State Status { get; private set; }

        public Relay(string url) => _ = Run(url);

        async Task Run(string url)
        {
            try
            {
                await ws.ConnectAsync(new Uri(url), cts.Token);
                Status = State.Open;
                _ = Task.Run(SendLoop);
                var buf = new byte[16 * 1024];
                var text = new StringBuilder();
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(new ArraySegment<byte>(buf), cts.Token);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    text.Append(Encoding.UTF8.GetString(buf, 0, r.Count));
                    if (r.EndOfMessage)
                    {
                        inbox.Enqueue(text.ToString());
                        text.Clear();
                    }
                }
            }
            catch (Exception e)
            {
                if (!cts.IsCancellationRequested) Debug.LogWarning("Relay: " + e.Message);
            }
            Status = State.Closed;
        }

        void SendLoop()
        {
            try
            {
                foreach (var msg in outbox.GetConsumingEnumerable(cts.Token))
                    ws.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(msg)), WebSocketMessageType.Text, true, cts.Token).Wait();
            }
            catch (Exception) { }
        }

        public void Send(string msg)
        {
            if (Status == State.Open) outbox.Add(msg);
        }

        public bool TryReceive(out string msg) => inbox.TryDequeue(out msg);

        public void Dispose()
        {
            cts.Cancel();
            ws.Dispose();
            Status = State.Closed;
        }
#endif
    }
}
