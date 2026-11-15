// Public relay for Haven Duel: same behavior as Tools/relay_server.py.
// Every player gets an id ("hello"), every message is forwarded to everyone else,
// and a "bye" goes out when someone leaves. One Durable Object holds all sockets.
import { DurableObject } from "cloudflare:workers";

export class Room extends DurableObject {
  async fetch() {
    const [client, server] = Object.values(new WebSocketPair());
    const id = ((await this.ctx.storage.get("next")) ?? 1);
    await this.ctx.storage.put("next", id + 1);
    this.ctx.acceptWebSocket(server);
    server.serializeAttachment({ id });
    server.send(JSON.stringify({ type: "hello", id }));
    return new Response(null, { status: 101, webSocket: client });
  }

  webSocketMessage(ws, msg) {
    for (const other of this.ctx.getWebSockets())
      if (other !== ws) try { other.send(msg); } catch {}
  }

  webSocketClose(ws) { this.leave(ws); }
  webSocketError(ws) { this.leave(ws); }

  leave(ws) {
    const { id } = ws.deserializeAttachment() ?? {};
    const bye = JSON.stringify({ type: "bye", id });
    for (const other of this.ctx.getWebSockets())
      if (other !== ws) try { other.send(bye); } catch {}
    try { ws.close(1000, "bye"); } catch {}
  }
}

export default {
  fetch(req, env) {
    if (req.headers.get("Upgrade") !== "websocket") return new Response("Haven Duel relay", { status: 200 });
    return env.ROOM.get(env.ROOM.idFromName("main")).fetch(req);
  },
};
