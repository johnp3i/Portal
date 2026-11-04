/*
 * Session-expiry handling (global).
 *
 * When the auth cookie expires, the server returns 401 {"sessionExpired":true} for AJAX/JSON
 * requests (see Program.cs OnRedirectToLogin) instead of a 302 to the login HTML. This script
 * wraps fetch and XMLHttpRequest so ANY such 401 — from any AJAX action across the app — shows a
 * single friendly prompt over a blurred backdrop, then sends the user to the login page with a
 * returnUrl back to where they were. This replaces the old behaviour where an expired session
 * surfaced as a generic "something went wrong" after the action failed.
 */
(function () {
    'use strict';

    var handling = false; // guard so concurrent 401s show the prompt only once

    function sameOrigin(url) {
        try {
            // Relative URLs are same-origin; absolute ones are compared against the page origin.
            var u = new URL(url, window.location.href);
            return u.origin === window.location.origin;
        } catch (e) {
            return true; // treat unparseable (relative) URLs as same-origin
        }
    }

    function loginUrl() {
        var returnUrl = window.location.pathname + window.location.search;
        return '/Account/Login?returnUrl=' + encodeURIComponent(returnUrl);
    }

    function goToLogin() {
        window.location.href = loginUrl();
    }

    // Shows the expiry prompt over a blurred neutral backdrop, then redirects on acknowledge.
    function handleSessionExpired() {
        if (handling) return;
        handling = true;

        // If SweetAlert2 isn't available for any reason, fall back to an immediate redirect.
        if (typeof Swal === 'undefined') {
            goToLogin();
            return;
        }

        // Blur + dim the app behind the dialog (neutral, not alarming).
        var shell = document.querySelector('.app') || document.body;
        var prevFilter = shell.style.filter;
        var prevTransition = shell.style.transition;
        shell.style.transition = 'filter .2s ease';
        shell.style.filter = 'blur(4px)';

        Swal.fire({
            title: 'Session expired',
            text: 'For your security you have been signed out. Please sign in again to continue.',
            icon: 'info',
            confirmButtonText: 'Sign in',
            confirmButtonColor: '#0D5EA6',
            allowOutsideClick: false,
            allowEscapeKey: false,
            backdrop: 'rgba(11, 27, 40, 0.35)'
        }).then(function () {
            // Leave the blur in place during navigation so there is no flash of the stale page.
            goToLogin();
        });

        // Safety: if the dialog is dismissed by any other means, still redirect.
        void prevFilter; void prevTransition;
    }

    // True when a response indicates an expired session (401 from our auth handler).
    function isSessionExpired(status) {
        return status === 401;
    }

    // ── Wrap fetch ───────────────────────────────────────────────────────────
    var originalFetch = window.fetch;
    if (typeof originalFetch === 'function') {
        window.fetch = function (input, init) {
            init = init || {};
            // Tag same-origin requests as AJAX so the server returns 401 JSON (not a login redirect).
            var url = (typeof input === 'string') ? input : (input && input.url) || '';
            if (sameOrigin(url)) {
                var headers = new Headers(init.headers || (typeof input !== 'string' && input.headers) || {});
                if (!headers.has('X-Requested-With')) {
                    headers.set('X-Requested-With', 'XMLHttpRequest');
                }
                init.headers = headers;
            }

            return originalFetch.call(this, input, init).then(function (response) {
                if (response && isSessionExpired(response.status) && sameOrigin(url)) {
                    handleSessionExpired();
                }
                return response;
            });
        };
    }

    // ── Wrap XMLHttpRequest ────────────────────────────────────────────────────
    var OriginalXHR = window.XMLHttpRequest;
    if (OriginalXHR) {
        var open = OriginalXHR.prototype.open;
        var send = OriginalXHR.prototype.send;

        OriginalXHR.prototype.open = function (method, url) {
            this.__seUrl = url;
            this.__seSameOrigin = sameOrigin(url);
            return open.apply(this, arguments);
        };

        OriginalXHR.prototype.send = function () {
            try {
                if (this.__seSameOrigin) {
                    this.setRequestHeader('X-Requested-With', 'XMLHttpRequest');
                }
            } catch (e) { /* header may already be set by the caller; ignore */ }

            this.addEventListener('load', function () {
                if (this.__seSameOrigin && isSessionExpired(this.status)) {
                    handleSessionExpired();
                }
            });
            return send.apply(this, arguments);
        };
    }
})();
