/**
 * BlockUI — Lightweight page overlay to prevent user interaction during AJAX calls.
 * Usage:
 *   BlockUI.show()        — show the overlay with spinner
 *   BlockUI.show('msg')   — show with custom message
 *   BlockUI.hide()        — hide the overlay
 */
var BlockUI = (function () {
    var overlay = null;

    function createOverlay() {
        if (overlay) return overlay;

        overlay = document.createElement('div');
        overlay.id = 'block-ui-overlay';
        overlay.style.cssText = 'display:none;position:fixed;inset:0;z-index:99999;background:rgba(255,255,255,.7);backdrop-filter:blur(2px);align-items:center;justify-content:center;flex-direction:column;gap:16px;';
        overlay.innerHTML = '<div style="width:40px;height:40px;border:4px solid rgba(13,94,166,.15);border-top-color:#0D5EA6;border-radius:50%;animation:blockui-spin 0.8s linear infinite;"></div><div id="block-ui-message" style="font-size:14px;font-weight:600;color:#334155;font-family:Inter,sans-serif;">Processing...</div>';

        var style = document.createElement('style');
        style.textContent = '@keyframes blockui-spin { to { transform: rotate(360deg); } }';
        document.head.appendChild(style);

        document.body.appendChild(overlay);
        return overlay;
    }

    return {
        show: function (message) {
            var el = createOverlay();
            var msg = el.querySelector('#block-ui-message');
            if (msg) msg.textContent = message || 'Processing...';
            el.style.display = 'flex';
        },
        hide: function () {
            if (overlay) overlay.style.display = 'none';
        }
    };
})();
