#!/usr/bin/env python3
import asyncio
import functools
import http.server
import json
import os
import socket
import threading
import time

import websockets

WS_PORT = 7777
HTTP_PORT = 8000
BUILD_DIR = os.path.join(os.path.dirname(__file__), "..", "builds", "webgl")

clients = {}
next_id = 1


async def broadcast(sender, msg):
    for ws in list(clients):
        if ws is not sender:
            try:
                await ws.send(msg)
            except websockets.ConnectionClosed:
                pass


async def handle(ws):
    global next_id
    player = next_id
    next_id += 1
    clients[ws] = player
    await ws.send(json.dumps({"type": "hello", "id": player}))
    h = ws.request.headers
    print(f"{time.strftime('%H:%M:%S')} player {player} joined ({len(clients)} online) from {h.get('Origin')} {h.get('X-Forwarded-For') or ws.remote_address[0]}")
    try:
        async for msg in ws:
            await broadcast(ws, msg)
    finally:
        del clients[ws]
        print(f"{time.strftime('%H:%M:%S')} player {player} left ({len(clients)} online) code={ws.close_code} reason={ws.close_reason!r}")
        await broadcast(ws, json.dumps({"type": "bye", "id": player}))


class NoCacheHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        self.send_header("Cache-Control", "no-cache")
        super().end_headers()

    def log_message(self, *args):
        pass


def lan_ip():
    with socket.socket(socket.AF_INET, socket.SOCK_DGRAM) as s:
        s.connect(("10.255.255.255", 1))
        return s.getsockname()[0]


async def main():
    ip = lan_ip()
    if os.path.isdir(BUILD_DIR):
        NoCacheHandler.extensions_map[".wasm"] = "application/wasm"
        handler = functools.partial(NoCacheHandler, directory=BUILD_DIR)
        server = http.server.ThreadingHTTPServer(("0.0.0.0", HTTP_PORT), handler)
        threading.Thread(target=server.serve_forever, daemon=True).start()
        print(f"game:  http://{ip}:{HTTP_PORT}")
    print(f"relay: ws://{ip}:{WS_PORT}")
    async with websockets.serve(handle, "0.0.0.0", WS_PORT):
        await asyncio.Future()


if __name__ == "__main__":
    asyncio.run(main())
