#!/usr/bin/env python3
"""LAN relay for HavenDuel multiplayer.

Every client's state message is forwarded to every other client. Also serves
builds/webgl over HTTP so anyone on the same wifi can open the game.

    python3 Tools/lan_server.py            # ws :7777, http :8000
"""
import asyncio, http.server, json, os, socket, threading, functools
import websockets

WS_PORT, HTTP_PORT = 7777, 8000
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "builds", "webgl")
clients = {}  # ws -> id
next_id = 1


async def handle(ws):
    global next_id
    pid = next_id; next_id += 1
    clients[ws] = pid
    await ws.send(json.dumps({"t": "hello", "id": pid}))
    print(f"player {pid} joined ({len(clients)} online)")
    try:
        async for msg in ws:
            for other in list(clients):
                if other is not ws:
                    try: await other.send(msg)
                    except Exception: pass
    finally:
        clients.pop(ws, None)
        print(f"player {pid} left ({len(clients)} online)")
        for other in list(clients):
            try: await other.send(json.dumps({"t": "bye", "id": pid}))
            except Exception: pass


class Handler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        # Unity's Gzip build with decompression fallback wants these left plain.
        if self.path.endswith(".wasm"): self.send_header("Content-Type", "application/wasm")
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()
    def log_message(self, *a): pass


def lan_ip():
    s = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try: s.connect(("10.255.255.255", 1)); return s.getsockname()[0]
    except Exception: return "127.0.0.1"
    finally: s.close()


async def main():
    ip = lan_ip()
    if os.path.isdir(ROOT):
        srv = http.server.ThreadingHTTPServer(("0.0.0.0", HTTP_PORT), functools.partial(Handler, directory=ROOT))
        threading.Thread(target=srv.serve_forever, daemon=True).start()
        print(f"game:  http://{ip}:{HTTP_PORT}")
    else:
        print(f"no build at {os.path.normpath(ROOT)}, serving relay only")
    print(f"relay: ws://{ip}:{WS_PORT}")
    async with websockets.serve(handle, "0.0.0.0", WS_PORT):
        await asyncio.Future()

if __name__ == "__main__":
    asyncio.run(main())
