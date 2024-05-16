window.loadBroadway = function loadPlayer(selector) {
    var node = document.getElementById(selector);
    var broadway = new Broadway(node);
    broadway.play();

};
function getBaseURL() {
    var baseTag = document.querySelector('base');
    if (!baseTag) {
        return window.location.href;
    }

    var href = baseTag.getAttribute('href');
    // Ensure it's a full URL (not relative)
    if (href.startsWith('http') || href.startsWith('https')) {
        return href;
    } else {
        // Construct a full URL if the base href is relative
        var fullURL = new URL(href, window.location.href);
        return fullURL.href;
    }
};
window.loadBroadwayStream = function (selector, streamUrl, width, height) {
    var player = new Player({
        workers: false,
        render: true,
        webgl: "auto"
    });
    const elem = document.getElementById(selector)
    elem.appendChild(player.canvas);

    const baseUrl = getBaseURL();
    var wsUrl = baseUrl.replace('http://', 'ws://').replace('https://', 'wss://') + streamUrl;

    var ws = new WebSocket(wsUrl);
    ws.binaryType = 'arraybuffer';
    ws.onmessage = function (e) {
        if (e.data instanceof ArrayBuffer) {
            const buffer = new Uint8Array(e.data); // Convert ArrayBuffer to Uint8Array
            //console.log('Decoding...: ' + buffer.length.toString());
            player.decode(buffer);
            //console.log('Decoded: ' + buffer.length.toString());
        }
    };
    ws.onerror = function (e) {
        console.error("WebSocket error:", e);
    };
    ws.onclose = function (event) {
        console.log("WebSocket is closed now.");
    };
};