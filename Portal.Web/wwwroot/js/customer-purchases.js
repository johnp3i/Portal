/* Customer Purchases dashboard. Vanilla JS, no jQuery. All data is written with textContent. */
(function () {
    'use strict';

    var MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
    var PALETTE = ['#0D5EA6', '#57B8E8', '#129867', '#C8912E', '#8a9bab'];
    var OTHER_COLOUR = '#D5DEE6';
    var KPI_COOKIE = 'cp_kpi_collapsed';
    var SWAL_BLUE = '#0D5EA6';

    var root = document.getElementById('cpRoot');
    var island = document.getElementById('purchasesData');
    if (!root || !island) { return; }

    var state = {
        customerId: parseInt(root.getAttribute('data-customer-id'), 10),
        applied: { period: 'last12m', from: null, to: null, currency: null },
        data: null,
        lineCache: new Map(),
        currencyChosenExplicitly: false,
        mixPage: 1,
        mixFilter: ''
    };

    var tipEl = null;

    // ------------------------------------------------------------------ helpers

    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) { node.className = className; }
        if (text !== undefined && text !== null) { node.textContent = text; }
        return node;
    }

    function clear(node) {
        while (node.firstChild) { node.removeChild(node.firstChild); }
    }

    function esc(value) {
        return String(value).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function isNum(v) { return typeof v === 'number' && isFinite(v); }

    function plural(n, one, many) { return n + ' ' + (n === 1 ? one : many); }

    function fmtNumber(value, decimals) {
        return Number(value).toLocaleString('en-US', { minimumFractionDigits: decimals, maximumFractionDigits: decimals });
    }

    function fmtMoney(value, code, decimals) {
        var d = decimals === undefined ? 0 : decimals;
        try {
            return new Intl.NumberFormat('en-US', {
                style: 'currency', currency: code, minimumFractionDigits: d, maximumFractionDigits: d
            }).format(value);
        } catch (ex) {
            return (code ? code + ' ' : '') + fmtNumber(value, d);
        }
    }

    function money(value, decimals) {
        return fmtMoney(value, state.data ? state.data.selectedCurrency : '', decimals);
    }

    function parseDate(s) {
        if (typeof s !== 'string') { return null; }
        var m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s);
        return m ? { y: +m[1], m: +m[2], d: +m[3] } : null;
    }

    function fmtDate(s) {
        var p = parseDate(s);
        if (!p) { return '-'; }
        return (p.d < 10 ? '0' : '') + p.d + ' ' + MONTHS[p.m - 1] + ' ' + p.y;
    }

    function fmtShortDate(s) {
        var p = parseDate(s);
        if (!p) { return '-'; }
        return (p.d < 10 ? '0' : '') + p.d + ' ' + MONTHS[p.m - 1];
    }

    function getAntiForgeryToken() {
        var input = document.querySelector('input[name="__RequestVerificationToken"]');
        return input ? input.value : '';
    }

    function swal(icon, title, text) {
        return Swal.fire({ icon: icon, title: title, text: text, confirmButtonColor: SWAL_BLUE });
    }

    // ------------------------------------------------------------------ request wrapper

    async function request(url, options, message, asBlob) {
        BlockUI.show(message);
        var controller = new AbortController();
        var timer = setTimeout(function () { controller.abort(); }, 30000);
        try {
            var response = await fetch(url, Object.assign({ signal: controller.signal }, options || {}));
            if (response.status === 404) {
                // The shared NotFound() has an empty body: never parse JSON here.
                BlockUI.hide();
                swal('warning', 'Not found', 'This customer could not be found.');
                return { ok: false, status: 404, handled: true, data: { kind: 'notFound' } };
            }
            if (asBlob && response.ok) {
                var blob = await response.blob();
                BlockUI.hide();
                return { ok: true, status: response.status, blob: blob, response: response };
            }
            var data = await response.json();
            BlockUI.hide();
            return { ok: response.ok && data && data.success === true, status: response.status, data: data };
        } catch (ex) {
            BlockUI.hide();
            return { ok: false, status: 0, data: { kind: 'error', message: 'Something went wrong. Please try again.' } };
        } finally {
            clearTimeout(timer);
        }
    }

    function showFailure(result) {
        if (!result || result.handled) { return; }
        var kind = result.data && result.data.kind;
        var text = (result.data && result.data.message) || 'Something went wrong. Please try again.';
        if (kind === 'validation') {
            swal('warning', 'Please check your selection', text);
        } else {
            swal('error', 'Error', text);
        }
    }

    // ------------------------------------------------------------------ safe rendering

    function safeRender(name, containerId, fn, message) {
        var container = document.getElementById(containerId);
        if (!container) { return; }
        try {
            fn(container);
        } catch (ex) {
            console.error('Render failed: ' + name, ex);
            clear(container);
            container.appendChild(el('div', 'cp-inline-msg cp-error', message || 'This section could not be displayed.'));
        }
    }

    function renderAll(data, skipKpis) {
        if (!skipKpis) { renderKpisSafely(data); }
        safeRender('chart', 'cpChart', function (c) { renderChart(c, data); }, 'The chart could not be displayed.');
        safeRender('mix', 'cpMix', function (c) { renderMix(c, data); });
        safeRender('donut', 'cpDonut', function (c) { renderDonut(c, data); });
        safeRender('behaviour', 'cpBehaviour', function (c) { renderBehaviour(c, data); });
        safeRender('pending', 'cpPending', function (c) { renderPending(c, data); });
        safeRender('currency', 'cpCurrencySeg', function (c) { renderCurrencySwitch(c, data); });
    }

    // KPI cards are server-rendered with their collapse toggles, so a render failure must never remove them.
    function renderKpisSafely(data) {
        var grid = document.getElementById('kpiGrid');
        if (!grid) { return; }
        try {
            renderKpis(grid, data);
        } catch (ex) {
            console.error('Render failed: kpis', ex);
            grid.querySelectorAll('.kpi').forEach(function (card) {
                card.querySelector('[data-role="v"]').textContent = '-';
                card.querySelector('[data-role="d"]').textContent = 'This figure could not be displayed.';
            });
        }
    }

    // ------------------------------------------------------------------ KPIs

    function kpiCard(container, id) {
        return container.querySelector('.kpi[data-kpi="' + id + '"]');
    }

    function setKpi(container, id, valueText, buildDetail) {
        var card = kpiCard(container, id);
        if (!card) { return; }
        card.querySelector('[data-role="v"]').textContent = valueText;
        var d = card.querySelector('[data-role="d"]');
        clear(d);
        if (buildDetail) { buildDetail(d); }
    }

    function renderKpis(container, data) {
        var k = data.kpis;
        var cur = data.selectedCurrency;

        setKpi(container, 'rev12', fmtMoney(k.rev12.value, cur, 0), function (d) {
            if (k.rev12.hasPrior && isNum(k.rev12.changePercent)) {
                var arrow = k.rev12.direction === 'up' ? '\u2191 ' : k.rev12.direction === 'down' ? '\u2193 ' : '\u2192 ';
                var pct = Math.abs(k.rev12.changePercent).toFixed(1) + '%';
                d.appendChild(el('span', k.rev12.direction === 'up' ? 'up' : k.rev12.direction === 'down' ? 'down' : '', arrow + pct));
                d.appendChild(document.createTextNode(' vs previous 12 months (' + fmtMoney(k.rev12.comparison, cur, 0) + ')'));
            } else {
                d.textContent = 'No prior period';
            }
        });

        setKpi(container, 'lifetime', fmtMoney(k.lifetime.value, cur, 0), function (d) {
            var text = plural(k.lifetime.invoiceCount, 'invoice', 'invoices');
            var first = parseDate(k.lifetime.firstInvoiceDate);
            if (first) { text += ' since ' + MONTHS[first.m - 1] + ' ' + first.y; }
            d.textContent = text + ', after credit notes';
        });

        setKpi(container, 'avginv', isNum(k.avgInvoice.value) ? fmtMoney(k.avgInvoice.value, cur, 0) : 'No invoices', function (d) {
            d.textContent = plural(k.avgInvoice.invoiceCount, 'invoice', 'invoices') + ' in the period';
        });

        var last = k.last;
        if (last.date && isNum(last.daysAgo)) {
            setKpi(container, 'last', last.daysAgo === 0 ? 'Today' : plural(last.daysAgo, 'day', 'days') + ' ago', function (d) {
                var text = fmtDate(last.date);
                if (isNum(last.frequencyDays)) { text += ' \u00B7 buys about every ' + plural(last.frequencyDays, 'day', 'days'); }
                d.textContent = text;
            });
        } else {
            setKpi(container, 'last', 'No purchases yet', null);
        }

        setKpi(container, 'pending', k.pending.count + ' \u00B7 ' + fmtMoney(k.pending.amount, cur, 0), function (d) {
            d.textContent = 'Outstanding after payments and credit notes';
        });

        setKpi(container, 'overdue', fmtMoney(k.overdue.amount, cur, 0), function (d) {
            if (k.overdue.count > 0) {
                var text = plural(k.overdue.count, 'invoice', 'invoices');
                if (isNum(k.overdue.daysLate)) { text += ', ' + plural(k.overdue.daysLate, 'day', 'days') + ' late'; }
                d.appendChild(el('span', 'down', text));
            } else {
                d.textContent = 'Nothing overdue';
            }
        });
    }

    // KPI collapse: the cookie is only written by explicit user actions, never by a refresh.
    function collapsedIdsFromDom() {
        var ids = [];
        document.querySelectorAll('#kpiGrid .kpi.collapsed').forEach(function (card) { ids.push(card.getAttribute('data-kpi')); });
        return ids;
    }

    function writeKpiCookie() {
        try {
            document.cookie = KPI_COOKIE + '=' + encodeURIComponent(collapsedIdsFromDom().join(',')) + '; max-age=31536000; path=/; SameSite=Lax';
        } catch (ex) {
            console.error('Could not save KPI preference', ex);
        }
    }

    function setCardCollapsed(card, collapsed) {
        card.classList.toggle('collapsed', collapsed);
        var btn = card.querySelector('.tg');
        if (btn) {
            btn.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
            btn.setAttribute('aria-label', collapsed ? 'Expand card' : 'Collapse card');
        }
    }

    function bindKpiControls() {
        var grid = document.getElementById('kpiGrid');
        if (grid) {
            grid.addEventListener('click', function (e) {
                var btn = e.target.closest('.tg');
                if (!btn) { return; }
                var card = btn.closest('.kpi');
                if (!card) { return; }
                setCardCollapsed(card, !card.classList.contains('collapsed'));
                writeKpiCookie();
            });
        }
        function all(collapsed) {
            document.querySelectorAll('#kpiGrid .kpi').forEach(function (card) { setCardCollapsed(card, collapsed); });
            writeKpiCookie();
        }
        var collapseAll = document.getElementById('kpiCollapseAll');
        var expandAll = document.getElementById('kpiExpandAll');
        if (collapseAll) { collapseAll.addEventListener('click', function () { all(true); }); }
        if (expandAll) { expandAll.addEventListener('click', function () { all(false); }); }
    }

    // ------------------------------------------------------------------ trend chart

    function monthLabel(t, i) {
        var s = MONTHS[t.month - 1];
        if (i === 0 || t.month === 1) { s += ' ' + String(t.year).slice(-2); }
        if (t.isCurrent) { s += '*'; }
        return s;
    }

    function niceScale(maxV) {
        if (!(maxV > 0)) { return { step: 1000, n: 1 }; }
        var raw = maxV / 5;
        var mag = Math.pow(10, Math.floor(Math.log(raw) / Math.LN10));
        var steps = [1, 2, 2.5, 5, 10];
        var step = mag * 10;
        for (var i = 0; i < steps.length; i++) {
            if (steps[i] * mag >= raw) { step = steps[i] * mag; break; }
        }
        return { step: step, n: Math.max(1, Math.ceil(maxV / step - 1e-9)) };
    }

    function compact(v) {
        if (v >= 1000000) { return Number((v / 1000000).toFixed(1)) + 'M'; }
        if (v >= 1000) { return Number((v / 1000).toFixed(1)) + 'k'; }
        return String(Number(v.toFixed(1)));
    }

    function validateTrend(trend) {
        if (!Array.isArray(trend) || trend.length !== 12) { throw new Error('Trend must have 12 items'); }
        trend.forEach(function (t) {
            if (!isNum(t.revenue) || !isNum(t.previousYearRevenue) || !isNum(t.invoiceCount) || !isNum(t.month) || !isNum(t.year) || t.month < 1 || t.month > 12) {
                throw new Error('Trend contains invalid values');
            }
        });
    }

    function hideTip() {
        if (tipEl && tipEl.parentNode) { tipEl.parentNode.removeChild(tipEl); }
        tipEl = null;
    }

    function showTip(target, text) {
        hideTip();
        tipEl = el('div', 'cp-tip', text);
        tipEl.setAttribute('role', 'tooltip');
        document.body.appendChild(tipEl);
        var r = target.getBoundingClientRect();
        var w = tipEl.offsetWidth;
        var left = Math.min(Math.max(8, r.left + r.width / 2 - w / 2), window.innerWidth - w - 8);
        tipEl.style.left = left + 'px';
        tipEl.style.top = Math.max(8, r.top - tipEl.offsetHeight - 8) + 'px';
    }

    function renderChart(container, data) {
        var trend = data.trend;
        validateTrend(trend);
        var cur = data.selectedCurrency;
        var compareBox = document.getElementById('cpCompare');
        var hasPrev = trend.some(function (t) { return t.previousYearRevenue !== 0; });
        if (compareBox) { compareBox.disabled = !hasPrev; }
        var showPrev = !!(compareBox && compareBox.checked) && hasPrev;

        var W = 900, H = 270, L = 62, R = 886, B = 222, PH = 180, bw = 34;
        var slot = (R - L) / 12;
        var maxV = 0;
        trend.forEach(function (t) {
            maxV = Math.max(maxV, t.revenue, showPrev ? t.previousYearRevenue : 0);
        });
        var scale = niceScale(maxV);
        var top = scale.step * scale.n;
        function y(v) { return B - (v / top) * PH; }

        var s = '<svg viewBox="0 0 ' + W + ' ' + H + '" width="100%" role="img" aria-label="Monthly revenue trend, last 12 months. A data table follows.">';
        for (var g = 0; g <= scale.n; g++) {
            var gv = g * scale.step, gy = y(gv);
            s += '<line x1="' + L + '" x2="' + R + '" y1="' + gy + '" y2="' + gy + '" stroke="rgba(13,94,166,.08)"/>' +
                '<text x="' + (L - 8) + '" y="' + (gy + 4) + '" text-anchor="end" font-size="10" fill="#73889A">' + esc(cur + ' ' + compact(gv)) + '</text>';
        }
        trend.forEach(function (t, i) {
            var x = L + i * slot + (slot - bw) / 2;
            var h = (t.revenue / top) * PH;
            var label = monthLabel(t, i);
            var tip = label.replace('*', '') + ': ' + fmtMoney(t.revenue, cur, 0) + ' (' + plural(t.invoiceCount, 'invoice', 'invoices') + ')';
            s += '<rect class="cp-bar" tabindex="0" data-tip="' + esc(tip) + '" aria-label="' + esc(tip) + '" x="' + x + '" y="' + (B - h) + '" width="' + bw + '" height="' + h + '" rx="6" fill="' + (t.isCurrent ? '#57B8E8' : '#0D5EA6') + '"/>' +
                (t.revenue !== 0 ? '<text x="' + (x + bw / 2) + '" y="' + (B - h - 6) + '" text-anchor="middle" font-size="10" font-weight="700" fill="#0B1B28">' + esc(fmtMoney(t.revenue, cur, 0)) + '</text>' : '') +
                '<text x="' + (x + bw / 2) + '" y="' + (B + 16) + '" text-anchor="middle" font-size="11" fill="#5E7385">' + esc(label) + '</text>' +
                '<text x="' + (x + bw / 2) + '" y="' + (B + 30) + '" text-anchor="middle" font-size="9" fill="#73889A">' + t.invoiceCount + ' inv</text>';
        });
        if (showPrev) {
            var pts = trend.map(function (t, j) { return (L + j * slot + slot / 2) + ',' + y(t.previousYearRevenue); });
            s += '<polyline points="' + pts.join(' ') + '" fill="none" stroke="#C8912E" stroke-width="2.5" stroke-dasharray="6 4"/>';
            trend.forEach(function (t, k) {
                var p = pts[k].split(',');
                var tip2 = 'Same month last year: ' + fmtMoney(t.previousYearRevenue, cur, 0);
                s += '<circle cx="' + p[0] + '" cy="' + p[1] + '" r="3.5" fill="#fff" stroke="#C8912E" stroke-width="2"><title>' + esc(tip2) + '</title></circle>';
            });
            s += '<g transform="translate(540,6)"><rect width="12" height="12" rx="3" fill="#0D5EA6"/><text x="18" y="10" font-size="11" fill="#5E7385">Last 12 months</text>' +
                '<line x1="110" x2="132" y1="6" y2="6" stroke="#C8912E" stroke-width="2.5" stroke-dasharray="6 4"/><text x="138" y="10" font-size="11" fill="#5E7385">Same month last year</text></g>';
        } else {
            s += '<g transform="translate(740,6)"><rect width="12" height="12" rx="3" fill="#0D5EA6"/><text x="18" y="10" font-size="11" fill="#5E7385">Last 12 months</text></g>';
        }
        s += '</svg>';

        // Build the sr-only table first so a failure here never leaves partial output.
        var table = el('table', 'sr-only');
        table.appendChild(el('caption', null, 'Monthly revenue, last 12 months'));
        var head = el('tr');
        ['Month', 'Revenue', 'Invoices', 'Same month last year'].forEach(function (h) { head.appendChild(el('th', null, h)); });
        var thead = el('thead'); thead.appendChild(head); table.appendChild(thead);
        var tbody = el('tbody');
        trend.forEach(function (t, i) {
            var tr = el('tr');
            tr.appendChild(el('td', null, monthLabel(t, i).replace('*', '') + (t.isCurrent ? ' (to date)' : '')));
            tr.appendChild(el('td', null, fmtMoney(t.revenue, cur, 0)));
            tr.appendChild(el('td', null, String(t.invoiceCount)));
            tr.appendChild(el('td', null, fmtMoney(t.previousYearRevenue, cur, 0)));
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);

        hideTip();
        container.innerHTML = s;
        container.appendChild(table);

        container.querySelectorAll('.cp-bar').forEach(function (bar) {
            var text = bar.getAttribute('data-tip');
            bar.addEventListener('mouseenter', function () { showTip(bar, text); });
            bar.addEventListener('focus', function () { showTip(bar, text); });
            bar.addEventListener('mouseleave', hideTip);
            bar.addEventListener('blur', hideTip);
        });
    }

    // ------------------------------------------------------------------ product mix and drill-down

    function qsForApplied(withCurrency) {
        var p = [];
        p.push('period=' + encodeURIComponent(state.applied.period));
        if (state.applied.period === 'custom') {
            p.push('from=' + encodeURIComponent(state.applied.from || ''));
            p.push('to=' + encodeURIComponent(state.applied.to || ''));
        }
        if (withCurrency && state.data && state.data.selectedCurrency) {
            p.push('currency=' + encodeURIComponent(state.data.selectedCurrency));
        }
        return p.join('&');
    }

    function basePath() { return '/Customer/Purchases/' + state.customerId; }

    function th(text, num) { return el('th', num ? 'num' : null, text); }

    var MIX_PAGE_SIZE = 15;

    function filteredMixRows(rows) {
        var q = (state.mixFilter || '').trim().toLowerCase();
        if (!q) { return rows; }
        return rows.filter(function (r) {
            return (r.displayName || '').toLowerCase().indexOf(q) !== -1 || (r.code || '').toLowerCase().indexOf(q) !== -1;
        });
    }

    // Page numbers to show: all when 7 or fewer, otherwise first, last and the current page's neighbours (null = gap).
    function pagesToShow(count, current) {
        var i, out = [];
        if (count <= 7) { for (i = 1; i <= count; i++) { out.push(i); } return out; }
        var set = [1, count, current - 1, current, current + 1].filter(function (p) { return p >= 1 && p <= count; });
        set = set.filter(function (p, idx) { return set.indexOf(p) === idx; }).sort(function (a, b) { return a - b; });
        set.forEach(function (p, idx) { if (idx > 0 && p - set[idx - 1] > 1) { out.push(null); } out.push(p); });
        return out;
    }

    function renderMix(container, data) {
        clear(container);
        var mix = data.productMix;
        var cur = data.selectedCurrency;
        if (!mix.rows || mix.rows.length === 0) {
            container.appendChild(el('div', 'cp-inline-msg', 'No purchases in this period.'));
            return;
        }

        var scroll = el('div', 'scroll');
        var table = el('table');
        var thead = el('thead');
        var hr = el('tr');
        hr.appendChild(th('Product')); hr.appendChild(th('Qty', true)); hr.appendChild(th('Revenue', true));
        hr.appendChild(th('Share')); hr.appendChild(th('Avg price vs list', true)); hr.appendChild(th('Last bought')); hr.appendChild(th('Margin', true));
        thead.appendChild(hr);
        table.appendChild(thead);

        var tbody = el('tbody');
        var filtered = filteredMixRows(mix.rows);
        if (filtered.length === 0) {
            container.appendChild(el('div', 'cp-inline-msg', 'No products match your search.'));
            return;
        }
        var pageCount = Math.max(1, Math.ceil(filtered.length / MIX_PAGE_SIZE));
        if (state.mixPage > pageCount) { state.mixPage = pageCount; }
        if (state.mixPage < 1) { state.mixPage = 1; }
        var startIdx = (state.mixPage - 1) * MIX_PAGE_SIZE;
        var pageRows = filtered.slice(startIdx, startIdx + MIX_PAGE_SIZE);

        pageRows.forEach(function (row) {
            var i = mix.rows.indexOf(row);
            var tr = el('tr');
            var nameCell = el('td', 'cp-name');
            var btn = el('button', 'drill-toggle');
            btn.type = 'button';
            btn.setAttribute('aria-expanded', 'false');
            btn.setAttribute('aria-controls', 'cpDetail' + i);
            btn.setAttribute('data-index', String(i));
            var label = el('span');
            label.appendChild(el('span', null, row.displayName));
            label.appendChild(el('span', 'code', row.isCustomItem ? 'no product code, grouped by description' : (row.code || '')));
            btn.appendChild(label);
            nameCell.appendChild(btn);
            tr.appendChild(nameCell);

            tr.appendChild(el('td', 'num', fmtNumber(row.quantity, 2).replace(/\.00$/, '')));
            var rev = el('td', 'num'); rev.appendChild(el('b', null, fmtMoney(row.revenue, cur, 0))); tr.appendChild(rev);

            var share = el('td');
            var bar = el('span', 'bar');
            bar.style.width = Math.max(0, Math.min(100, row.sharePercent)) * 2 + 'px';
            share.appendChild(bar);
            share.appendChild(document.createTextNode(Number(row.sharePercent).toFixed(1) + '%'));
            tr.appendChild(share);

            var avg = el('td', 'num');
            if (isNum(row.averagePricePaid)) {
                avg.appendChild(document.createTextNode(fmtMoney(row.averagePricePaid, cur, 2) + ' vs ' + (isNum(row.listPrice) ? fmtMoney(row.listPrice, cur, 2) : '-')));
                if (isNum(row.priceDifferencePercent)) {
                    var diff = row.priceDifferencePercent;
                    avg.appendChild(document.createTextNode(' '));
                    avg.appendChild(el('span', diff < 0 ? 'down' : diff > 0 ? 'up' : '', '(' + (diff > 0 ? '+' : '') + diff.toFixed(1) + '%)'));
                }
            } else {
                avg.textContent = '-';
            }
            tr.appendChild(avg);

            tr.appendChild(el('td', null, fmtDate(row.lastBought)));

            var margin = el('td', 'num');
            if (isNum(row.marginPercent)) {
                margin.appendChild(document.createTextNode(Number(row.marginPercent).toFixed(1) + '%'));
                if (row.isMarginPartial) {
                    margin.appendChild(document.createTextNode(' '));
                    margin.appendChild(el('span', 'pill p-gry', 'partial'));
                }
            } else {
                margin.textContent = '-';
            }
            tr.appendChild(margin);
            tbody.appendChild(tr);

            var detail = el('tr', 'detail');
            detail.id = 'cpDetail' + i;
            detail.hidden = true;
            var dtd = el('td'); dtd.colSpan = 7;
            detail.appendChild(dtd);
            tbody.appendChild(detail);
        });
        table.appendChild(tbody);
        scroll.appendChild(table);
        container.appendChild(scroll);

        var pager = el('div', 'cp-pager');
        pager.appendChild(el('div', 'cp-pager-info', 'Showing ' + (startIdx + 1) + '-' + (startIdx + pageRows.length) + ' of ' + plural(filtered.length, 'product', 'products')));
        if (pageCount > 1) {
            var controls = el('div', 'cp-pager-controls');
            var pageBtn = function (label, page, disabled, active) {
                var b = el('button', 'cp-page-btn' + (active ? ' on' : ''), label);
                b.type = 'button';
                if (disabled) { b.disabled = true; }
                if (active) { b.setAttribute('aria-current', 'page'); }
                b.addEventListener('click', function () {
                    state.mixPage = page;
                    safeRender('mix', 'cpMix', function (c) { renderMix(c, data); });
                });
                return b;
            };
            controls.appendChild(pageBtn('Previous', state.mixPage - 1, state.mixPage === 1, false));
            pagesToShow(pageCount, state.mixPage).forEach(function (p) {
                if (p === null) { controls.appendChild(el('span', 'cp-page-gap', '\u2026')); }
                else { controls.appendChild(pageBtn(String(p), p, false, p === state.mixPage)); }
            });
            controls.appendChild(pageBtn('Next', state.mixPage + 1, state.mixPage === pageCount, false));
            pager.appendChild(controls);
        }
        container.appendChild(pager);

        var note = el('div', 'note');
        var coverage = isNum(mix.marginCoveragePercent)
            ? 'Margin uses cost price where it was recorded (' + Number(mix.marginCoveragePercent).toFixed(1) + '% of line value here). '
            : 'Margin coverage: - (no cost price recorded). ';
        note.appendChild(el('b', null, 'Reading this table. '));
        note.appendChild(document.createTextNode('Products are grouped by product code. Lines without a code are grouped by description and shown as custom items. ' + coverage +
            'Adjustment lines are left out of this table, so product revenue can differ from the net revenue on the KPI cards. '));
        note.appendChild(el('b', null, 'Unpaid amounts are shown per invoice below'));
        note.appendChild(document.createTextNode(', because payments are recorded against invoices, not products.'));
        container.appendChild(note);

        tbody.addEventListener('click', function (e) {
            var btn = e.target.closest('.drill-toggle');
            if (btn) { toggleDrill(btn, mix.rows[parseInt(btn.getAttribute('data-index'), 10)]); }
        });
    }

    function statusClass(status) {
        switch (status) {
            case 'Paid': return 'p-grn';
            case 'Partially paid': return 'p-amb';
            case 'Overdue': return 'p-red';
            default: return 'p-gry'; // Unpaid, Written off
        }
    }

    function renderLines(cell, dto) {
        clear(cell);
        var cur = state.data.selectedCurrency;
        if (!dto.lines || dto.lines.length === 0) {
            cell.appendChild(el('span', 'cp-small cp-muted', 'No purchase lines found for this product.'));
            return;
        }
        var scroll = el('div', 'scroll');
        var table = el('table');
        var thead = el('thead'); var hr = el('tr');
        hr.appendChild(th('Date')); hr.appendChild(th('Invoice')); hr.appendChild(th('Qty', true)); hr.appendChild(th('Line total', true)); hr.appendChild(th('Status'));
        thead.appendChild(hr); table.appendChild(thead);
        var tbody = el('tbody');
        dto.lines.forEach(function (line) {
            var tr = el('tr');
            tr.appendChild(el('td', null, fmtDate(line.date)));
            var inv = el('td');
            var a = el('a', 'cp-inv-link', line.invoiceNumber);
            a.href = '/Invoice/Detail/' + encodeURIComponent(line.invoiceId);
            inv.appendChild(a);
            tr.appendChild(inv);
            tr.appendChild(el('td', 'num', fmtNumber(line.quantity, 2).replace(/\.00$/, '')));
            tr.appendChild(el('td', 'num', fmtMoney(line.lineTotal, cur, 2)));
            var st = el('td'); st.appendChild(el('span', 'pill ' + statusClass(line.status), line.status)); tr.appendChild(st);
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);
        scroll.appendChild(table);
        cell.appendChild(scroll);
        if (dto.totalLineCount > dto.lines.length) {
            cell.appendChild(el('div', 'cp-small cp-muted', 'Showing the latest ' + dto.lines.length + ' of ' + dto.totalLineCount + ' lines.'));
        }
    }

    async function toggleDrill(btn, row) {
        var detail = document.getElementById(btn.getAttribute('aria-controls'));
        if (!detail || !row) { return; }
        if (btn.getAttribute('aria-expanded') === 'true') {
            btn.setAttribute('aria-expanded', 'false');
            detail.hidden = true;
            return;
        }
        var dto = state.lineCache.get(row.groupKey);
        if (!dto) {
            var url = basePath() + '/AxGetProductPurchaseLines?' + qsForApplied(true) + '&productKey=' + encodeURIComponent(row.groupKey);
            var res = await request(url, { method: 'GET' }, 'Loading purchase history...');
            if (!res.ok) { showFailure(res); return; }
            dto = res.data.data;
            state.lineCache.set(row.groupKey, dto);
        }
        try {
            renderLines(detail.firstChild, dto);
        } catch (ex) {
            console.error('Render failed: drill-down', ex);
            clear(detail.firstChild);
            detail.firstChild.appendChild(el('span', 'cp-small cp-error', 'This section could not be displayed.'));
        }
        btn.setAttribute('aria-expanded', 'true');
        detail.hidden = false;
    }

    // ------------------------------------------------------------------ donut

    function renderDonut(container, data) {
        clear(container);
        var slices = data.donut || [];
        var cur = data.selectedCurrency;
        var sum = 0;
        slices.forEach(function (sl) { if (!isNum(sl.sharePercent)) { throw new Error('Invalid slice'); } sum += sl.sharePercent; });
        if (slices.length === 0 || sum <= 0) {
            container.appendChild(el('div', 'cp-inline-msg', 'No product revenue in this period.'));
            return;
        }

        var C = 339.29, offset = 0, circles = '';
        var colours = [];
        var idx = 0;
        slices.forEach(function (sl) {
            var colour = sl.isOther ? OTHER_COLOUR : PALETTE[idx++ % PALETTE.length];
            colours.push(colour);
            var dash = Math.max(0, sl.sharePercent) / 100 * C;
            circles += '<circle cx="70" cy="70" r="54" stroke="' + colour + '" stroke-dasharray="' + dash.toFixed(2) + ' ' + C + '" stroke-dashoffset="' + (-offset).toFixed(2) + '"/>';
            offset += dash;
        });

        var wrap = el('div', 'donut');
        var holder = el('div');
        holder.innerHTML = '<svg width="190" height="190" viewBox="0 0 140 140" role="img" aria-label="Share of spend by product. The legend lists the values.">' +
            '<g transform="rotate(-90 70 70)" fill="none" stroke-width="22">' + circles + '</g></svg>';
        var svg = holder.firstChild;
        var total = fmtMoney(data.productMix.totalRevenue, cur, 0);
        var ns = 'http://www.w3.org/2000/svg';
        var t1 = document.createElementNS(ns, 'text');
        t1.setAttribute('x', '70'); t1.setAttribute('y', '66'); t1.setAttribute('text-anchor', 'middle');
        t1.setAttribute('font-weight', '800'); t1.setAttribute('font-size', total.length > 10 ? '11' : '15'); t1.setAttribute('fill', '#0B1B28');
        t1.textContent = total;
        var t2 = document.createElementNS(ns, 'text');
        t2.setAttribute('x', '70'); t2.setAttribute('y', '82'); t2.setAttribute('text-anchor', 'middle');
        t2.setAttribute('font-size', '8'); t2.setAttribute('fill', '#73889A');
        t2.textContent = 'selected period';
        svg.appendChild(t1); svg.appendChild(t2);
        wrap.appendChild(svg);

        var leg = el('div', 'leg');
        slices.forEach(function (sl, i) {
            var line = el('div');
            var left = el('span', 'nm');
            var sw = el('span', 'sw'); sw.style.background = colours[i];
            left.appendChild(sw);
            left.appendChild(document.createTextNode(sl.isOther ? 'Other (' + plural(Math.max(0, data.productMix.rows.length - (slices.length - 1)), 'product', 'products') + ')' : sl.label));
            line.appendChild(left);
            line.appendChild(el('b', null, Number(sl.sharePercent).toFixed(1) + '%'));
            leg.appendChild(line);
        });
        wrap.appendChild(leg);
        container.appendChild(wrap);
    }

    // ------------------------------------------------------------------ behaviour

    function stat(label, valueText, valueClass) {
        var row = el('div', 'stat');
        row.appendChild(el('span', null, label));
        row.appendChild(el('b', valueClass || null, valueText));
        return row;
    }

    function renderBehaviour(container, data) {
        clear(container);
        var b = data.behaviour;
        var cur = data.selectedCurrency;

        // Rhythm
        var rhythmCard = el('div', 'ins');
        var h = el('h4', null, 'Buying rhythm ');
        h.appendChild(el('span', 'cp-small cp-muted', 'typical gap between purchases'));
        rhythmCard.appendChild(h);
        if (!b.rhythm || b.rhythm.length === 0) {
            rhythmCard.appendChild(el('div', 'cp-small cp-muted', 'Not enough repeat purchases to show a rhythm yet.'));
        } else {
            var list = el('div', 'rhythm');
            b.rhythm.forEach(function (item) {
                var row = el('div');
                var left = el('span', 'rn');
                left.appendChild(el('span', null, item.name));
                left.appendChild(document.createElement('br'));
                left.appendChild(el('span', 'cp-small cp-muted',
                    'every ~' + plural(item.typicalGapDays, 'day', 'days') + ', last ' + (item.daysSinceLast === 0 ? 'today' : plural(item.daysSinceLast, 'day', 'days') + ' ago')));
                row.appendChild(left);
                var chip = item.chip === 'reorderAround'
                    ? el('span', 'pill p-amb', 'Reorder around ' + fmtShortDate(item.expectedDate))
                    : item.chip === 'reorderSoon'
                        ? el('span', 'pill p-amb', 'Reorder soon')
                        : el('span', 'pill p-grn', 'On track');
                row.appendChild(chip);
                list.appendChild(row);
            });
            rhythmCard.appendChild(list);
        }
        container.appendChild(rhythmCard);

        // Payment behaviour
        var p = b.payment;
        var payCard = el('div', 'ins');
        payCard.appendChild(el('h4', null, 'Payment behaviour'));
        if (p.hasSettled) {
            var days = isNum(p.avgDaysToPay) ? plural(p.avgDaysToPay, 'day', 'days') + (isNum(p.avgTermsDays) ? ' (terms ' + p.avgTermsDays + ')' : '') : '-';
            payCard.appendChild(stat('Average days to pay', days));
            payCard.appendChild(stat('Settled on time', p.onTimeCount + ' of ' + plural(p.settledCount, 'invoice', 'invoices') + (isNum(p.onTimePercent) ? ' (' + Math.round(p.onTimePercent) + '%)' : '')));
        } else {
            payCard.appendChild(el('div', 'cp-small cp-muted', 'No settled invoices yet'));
        }
        payCard.appendChild(stat('Currently overdue', fmtMoney(p.overdueAmount, cur, 0), p.overdueAmount > 0 ? 'down' : ''));
        payCard.appendChild(stat('Credit notes in the period', p.creditNoteCount + ' \u00B7 ' + fmtMoney(p.creditNoteTotal, cur, 0)));
        payCard.appendChild(el('div', 'note', 'Overdue means a balance above zero after payments and credit notes, with a due date in the past.'));
        container.appendChild(payCard);
    }

    // ------------------------------------------------------------------ pending invoices

    function renderPending(container, data) {
        clear(container);
        var cur = data.selectedCurrency;
        var rows = data.pendingInvoices || [];
        if (rows.length === 0) {
            container.appendChild(el('div', 'cp-inline-msg', 'No pending invoices'));
            return;
        }
        var scroll = el('div', 'scroll');
        var table = el('table');
        var thead = el('thead'); var hr = el('tr');
        hr.appendChild(th('Invoice')); hr.appendChild(th('Issued')); hr.appendChild(th('Due'));
        hr.appendChild(th('Total', true)); hr.appendChild(th('Paid', true)); hr.appendChild(th('Credit notes', true)); hr.appendChild(th('Outstanding', true)); hr.appendChild(th('Status'));
        thead.appendChild(hr); table.appendChild(thead);

        var tbody = el('tbody');
        rows.forEach(function (r) {
            var tr = el('tr');
            var inv = el('td');
            var a = el('a', 'cp-inv-link', r.invoiceNumber);
            a.href = '/Invoice/Detail/' + encodeURIComponent(r.invoiceId);
            inv.appendChild(a);
            tr.appendChild(inv);
            tr.appendChild(el('td', null, fmtDate(r.issueDate)));
            tr.appendChild(el('td', null, fmtDate(r.dueDate)));
            tr.appendChild(el('td', 'num', fmtMoney(r.total, cur, 2)));
            tr.appendChild(el('td', 'num', fmtMoney(r.paid, cur, 2)));
            tr.appendChild(el('td', 'num', fmtMoney(r.creditNotes, cur, 2)));
            var out = el('td', 'num'); out.appendChild(el('b', null, fmtMoney(r.outstanding, cur, 2))); tr.appendChild(out);
            var st = el('td');
            st.appendChild(el('span', 'pill ' + (r.statusKind === 'overdue' ? 'p-red' : r.statusKind === 'partial' ? 'p-amb' : 'p-gry'), r.statusText));
            tr.appendChild(st);
            tbody.appendChild(tr);
        });
        table.appendChild(tbody);

        var tfoot = el('tfoot'); var fr = el('tr');
        var label = el('td', 'num'); label.colSpan = 6; label.appendChild(el('b', null, 'Total outstanding'));
        var total = el('td', 'num'); total.appendChild(el('b', null, fmtMoney(data.pendingTotal, cur, 2)));
        fr.appendChild(label); fr.appendChild(total); fr.appendChild(el('td'));
        tfoot.appendChild(fr); table.appendChild(tfoot);

        scroll.appendChild(table);
        container.appendChild(scroll);
    }

    // ------------------------------------------------------------------ currency switch

    function renderCurrencySwitch(container, data) {
        clear(container);
        var field = document.getElementById('cpCurrencyField');
        var hint = document.getElementById('cpCurrencyHint');
        var show = !!data.showCurrencySwitch && Array.isArray(data.currencies) && data.currencies.length > 1;
        if (field) { field.hidden = !show; }
        if (hint) { hint.hidden = !show; }
        if (!show) { return; }
        data.currencies.forEach(function (code) {
            var b = el('button', code === data.selectedCurrency ? 'on' : '', code);
            b.type = 'button';
            b.setAttribute('aria-pressed', code === data.selectedCurrency ? 'true' : 'false');
            b.addEventListener('click', function () { onCurrencyClick(code); });
            container.appendChild(b);
        });
    }

    // ------------------------------------------------------------------ refresh, period and currency

    function periodControls() {
        return {
            select: document.getElementById('cpPeriod'),
            from: document.getElementById('cpFrom'),
            to: document.getElementById('cpTo'),
            fromWrap: document.getElementById('cpCustomFields'),
            toWrap: document.getElementById('cpCustomFieldsTo')
        };
    }

    function syncPeriodControls() {
        var c = periodControls();
        var custom = state.applied.period === 'custom';
        c.select.value = state.applied.period;
        c.from.value = custom ? (state.applied.from || '') : '';
        c.to.value = custom ? (state.applied.to || '') : '';
        c.fromWrap.style.display = custom ? '' : 'none';
        c.toWrap.style.display = custom ? '' : 'none';
    }

    function updateUrl() {
        try {
            var q = ['period=' + encodeURIComponent(state.applied.period)];
            if (state.applied.period === 'custom') {
                q.push('from=' + encodeURIComponent(state.applied.from || ''));
                q.push('to=' + encodeURIComponent(state.applied.to || ''));
            }
            if (state.currencyChosenExplicitly && state.applied.currency) {
                q.push('currency=' + encodeURIComponent(state.applied.currency));
            }
            history.replaceState(null, '', window.location.pathname + '?' + q.join('&'));
        } catch (ex) {
            console.error('Could not update the address bar', ex);
        }
    }

    function appliedFromData(data) {
        var custom = data.period.key === 'custom';
        return { period: data.period.key, from: custom ? data.period.from : null, to: custom ? data.period.to : null, currency: data.selectedCurrency };
    }

    // Returns true when the refresh succeeded. `applied` only changes after success.
    async function refresh(selection, explicitCurrency) {
        var q = ['period=' + encodeURIComponent(selection.period)];
        if (selection.period === 'custom') {
            q.push('from=' + encodeURIComponent(selection.from));
            q.push('to=' + encodeURIComponent(selection.to));
        }
        if (selection.currency) { q.push('currency=' + encodeURIComponent(selection.currency)); }
        var res = await request(basePath() + '/AxGetPurchasesData?' + q.join('&'), { method: 'GET' }, 'Loading purchases...');
        if (!res.ok) {
            showFailure(res);
            syncPeriodControls();
            return false;
        }
        state.data = res.data.data;
        state.applied = appliedFromData(state.data);
        state.currencyChosenExplicitly = explicitCurrency;
        state.lineCache.clear();
        state.mixPage = 1;
        renderAll(state.data);
        syncPeriodControls();
        updateUrl();
        return true;
    }

    function validDate(s) { return /^\d{4}-\d{2}-\d{2}$/.test(s) && !isNaN(Date.parse(s)); }

    async function onPeriodChange() {
        var c = periodControls();
        var key = c.select.value;
        if (key !== 'custom') {
            c.fromWrap.style.display = 'none';
            c.toWrap.style.display = 'none';
            await refresh({ period: key, from: null, to: null, currency: null }, false);
            return;
        }
        c.fromWrap.style.display = '';
        c.toWrap.style.display = '';
        await onCustomDatesChange();
    }

    async function onCustomDatesChange() {
        var c = periodControls();
        if (c.select.value !== 'custom') { return; }
        var from = c.from.value, to = c.to.value;
        if (!validDate(from) || !validDate(to)) { return; } // wait for both dates
        if (from > to) {
            swal('warning', 'Invalid date range', 'The start date must be on or before the end date.');
            return;
        }
        await refresh({ period: 'custom', from: from, to: to, currency: null }, false);
    }

    async function onCurrencyClick(code) {
        if (code === state.data.selectedCurrency) { return; }
        var sel = { period: state.applied.period, from: state.applied.from, to: state.applied.to, currency: code };
        await refresh(sel, true);
    }

    // ------------------------------------------------------------------ teasers and export

    async function onNotify(btn) {
        var key = btn.getAttribute('data-feature');
        var res = await request('/Customer/Purchases/AxPostRegisterFeatureInterest', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': getAntiForgeryToken() },
            body: JSON.stringify({ featureKey: key })
        }, 'Saving...');
        if (!res.ok) { showFailure(res); return; }
        btn.textContent = 'You will be notified';
        btn.disabled = true;
        swal('success', 'We will let you know', 'You will be notified when this insight becomes available.');
    }

    function fileNameFrom(response) {
        var fallback = 'customer-purchases.xlsx';
        try {
            var cd = response.headers.get('Content-Disposition') || '';
            var star = /filename\*=(?:UTF-8'')?([^;]+)/i.exec(cd);
            if (star) { return decodeURIComponent(star[1].trim().replace(/^"|"$/g, '')); }
            var plain = /filename="?([^";]+)"?/i.exec(cd);
            if (plain) { return plain[1].trim(); }
        } catch (ex) {
            console.error('Could not read the file name', ex);
        }
        return fallback;
    }

    async function onExport(e) {
        e.preventDefault();
        var url = basePath() + '/AxGetExportPurchases?' + qsForApplied(true);
        var res = await request(url, { method: 'GET' }, 'Preparing export...', true);
        if (!res.ok) { showFailure(res); return; }
        var objectUrl = URL.createObjectURL(res.blob);
        var a = document.createElement('a');
        a.href = objectUrl;
        a.download = fileNameFrom(res.response);
        document.body.appendChild(a);
        a.click();
        document.body.removeChild(a);
        setTimeout(function () { URL.revokeObjectURL(objectUrl); }, 1000);
    }

    // ------------------------------------------------------------------ init

    function init() {
        var data;
        try {
            data = JSON.parse(island.textContent);
        } catch (ex) {
            console.error('Could not read the purchases data', ex);
            var grid = document.getElementById('kpiGrid');
            if (grid) { clear(grid); grid.appendChild(el('div', 'cp-inline-msg cp-error', 'This page could not be displayed.')); }
            return;
        }
        state.data = data;
        state.applied = appliedFromData(data);
        try {
            var urlCurrency = new URLSearchParams(window.location.search).get('currency');
            state.currencyChosenExplicitly = !!urlCurrency && urlCurrency === data.selectedCurrency;
        } catch (ex) {
            state.currencyChosenExplicitly = false;
        }

        syncPeriodControls();
        bindKpiControls();

        var c = periodControls();
        c.select.addEventListener('change', onPeriodChange);
        c.from.addEventListener('change', onCustomDatesChange);
        c.to.addEventListener('change', onCustomDatesChange);

        var compare = document.getElementById('cpCompare');
        if (compare) {
            compare.addEventListener('change', function () {
                safeRender('chart', 'cpChart', function (container) { renderChart(container, state.data); }, 'The chart could not be displayed.');
            });
        }

        document.querySelectorAll('.notify').forEach(function (btn) {
            if (btn.disabled) { return; }
            btn.addEventListener('click', function () { onNotify(btn); });
        });

        var mixSearch = document.getElementById('cpMixSearch');
        if (mixSearch) {
            var searchTimer = null;
            mixSearch.addEventListener('input', function () {
                clearTimeout(searchTimer);
                searchTimer = setTimeout(function () {
                    state.mixFilter = mixSearch.value || '';
                    state.mixPage = 1;
                    safeRender('mix', 'cpMix', function (c) { renderMix(c, state.data); });
                }, 150);
            });
        }

        var exportBtn = document.getElementById('exportBtn');
        if (exportBtn) { exportBtn.addEventListener('click', onExport); }

        renderAll(data, true); // KPI cards are already filled by the server

        if (root.getAttribute('data-period-fallback') === 'true') {
            swal('warning', 'Period not recognised', 'The requested period was not valid, so the last 12 months are shown.');
        }
    }

    init();
})();
