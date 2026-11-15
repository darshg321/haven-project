mergeInto(LibraryManager.library, {
  Relay_Open: function (url) {
    var relay = window.relay = { queue: [], state: 0, url: UTF8ToString(url) };
    // Keep trying: if the relay restarts or the network blips, reconnect every 2 s
    // instead of staying on "can't reach" until the page is refreshed.
    function connect() {
      if (window.relay !== relay) return;
      try {
        relay.ws = new WebSocket(relay.url);
      } catch (e) {
        relay.state = 2;
        setTimeout(connect, 2000);
        return;
      }
      relay.ws.onopen = function () { relay.state = 1; };
      relay.ws.onclose = function () {
        relay.state = 2;
        setTimeout(connect, 2000);
      };
      relay.ws.onmessage = function (e) { relay.queue.push(e.data); };
    }
    connect();
  },

  Relay_State: function () {
    return window.relay ? window.relay.state : 0;
  },

  Relay_Send: function (msg) {
    if (window.relay && window.relay.state === 1) window.relay.ws.send(UTF8ToString(msg));
  },

  Relay_Recv: function () {
    if (!window.relay || window.relay.queue.length === 0) return null;
    var str = window.relay.queue.shift();
    var size = lengthBytesUTF8(str) + 1;
    var buf = _malloc(size);
    stringToUTF8(str, buf, size);
    return buf;
  },

  Relay_PageHost: function () {
    var str = window.location.hostname || "localhost";
    var size = lengthBytesUTF8(str) + 1;
    var buf = _malloc(size);
    stringToUTF8(str, buf, size);
    return buf;
  },

  Relay_Close: function () {
    if (window.relay && window.relay.ws) window.relay.ws.close();
    window.relay = null;
  },
});
