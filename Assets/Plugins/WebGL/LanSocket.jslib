mergeInto(LibraryManager.library, {
  LanSocket_Open: function (urlPtr) {
    var url = UTF8ToString(urlPtr);
    var s = window.__lanSock = { q: [], open: false, closed: false };
    try { s.ws = new WebSocket(url); } catch (e) { s.closed = true; return; }
    s.ws.onopen = function () { s.open = true; };
    s.ws.onclose = function () { s.closed = true; s.open = false; };
    s.ws.onerror = function () { s.closed = true; };
    s.ws.onmessage = function (e) { if (typeof e.data === "string") s.q.push(e.data); };
  },
  LanSocket_State: function () {
    var s = window.__lanSock; if (!s) return 0;
    return s.closed ? 3 : (s.open ? 1 : 0);
  },
  LanSocket_Send: function (msgPtr) {
    var s = window.__lanSock; if (s && s.open) s.ws.send(UTF8ToString(msgPtr));
  },
  LanSocket_Recv: function () {
    var s = window.__lanSock; if (!s || s.q.length === 0) return null;
    var str = s.q.shift(); var n = lengthBytesUTF8(str) + 1;
    var buf = _malloc(n); stringToUTF8(str, buf, n); return buf;
  },
  LanSocket_HostName: function () {
    var str = window.location.hostname || "localhost"; var n = lengthBytesUTF8(str) + 1;
    var buf = _malloc(n); stringToUTF8(str, buf, n); return buf;
  },
  LanSocket_Secure: function () { return window.location.protocol === "https:" ? 1 : 0; },
  LanSocket_Close: function () {
    var s = window.__lanSock; if (s && s.ws) s.ws.close(); window.__lanSock = null;
  }
});
