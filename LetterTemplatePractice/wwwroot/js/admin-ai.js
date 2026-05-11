/**
 * admin-ai.js — AI Dashboard polling & chart rendering
 * Uses jQuery (already loaded in _Layout). No external chart library needed.
 */
(function ($) {
    'use strict';

    var POLL_INTERVAL = 8000; // 8 seconds
    var pollTimer = null;

    // ── Overview polling ───────────────────────────────────────────────────

    function fetchOverview() {
        $.getJSON('/admin/api/ai/overview')
            .done(function (data) {
                $('#tPending').text(data.pending);
                $('#tInProgress').text(data.inProgress);
                $('#tSucceeded').text(data.succeeded24h);
                $('#tFailed').text(data.failed24h);
                $('#sAvgAttempts').text(data.avgAttempts);
                $('#sAvgLatency').text(formatMs(data.avgLatencyMs));
                renderVolumeChart(data.hourly);
            })
            .fail(function () {
                // Silently ignore — will retry next tick
            });
    }

    function formatMs(ms) {
        if (ms < 1000) return Math.round(ms) + 'ms';
        return (ms / 1000).toFixed(1) + 's';
    }

    // ── Simple bar chart (Canvas 2D) ──────────────────────────────────────

    function renderVolumeChart(hourly) {
        var canvas = document.getElementById('volumeCanvas');
        if (!canvas || !hourly || hourly.length === 0) return;

        var ctx = canvas.getContext('2d');
        var dpr = window.devicePixelRatio || 1;
        var rect = canvas.parentElement.getBoundingClientRect();
        canvas.width = rect.width * dpr;
        canvas.height = 160 * dpr;
        canvas.style.width = rect.width + 'px';
        canvas.style.height = '160px';
        ctx.scale(dpr, dpr);

        var W = rect.width;
        var H = 160;
        var pad = { top: 10, right: 10, bottom: 30, left: 40 };
        var chartW = W - pad.left - pad.right;
        var chartH = H - pad.top - pad.bottom;

        ctx.clearRect(0, 0, W, H);

        var maxCount = 1;
        hourly.forEach(function (h) { if (h.count > maxCount) maxCount = h.count; });

        var barW = Math.max(4, (chartW / hourly.length) - 2);
        var gap = (chartW - barW * hourly.length) / (hourly.length + 1);

        // Grid lines
        ctx.strokeStyle = '#e5e7eb';
        ctx.lineWidth = 1;
        for (var g = 0; g <= 4; g++) {
            var y = pad.top + chartH - (chartH * g / 4);
            ctx.beginPath();
            ctx.moveTo(pad.left, y);
            ctx.lineTo(W - pad.right, y);
            ctx.stroke();

            ctx.fillStyle = '#9ca3af';
            ctx.font = '10px sans-serif';
            ctx.textAlign = 'right';
            ctx.fillText(Math.round(maxCount * g / 4), pad.left - 6, y + 3);
        }

        // Bars
        ctx.fillStyle = '#2563eb';
        hourly.forEach(function (h, i) {
            var barH = (h.count / maxCount) * chartH;
            var x = pad.left + gap + i * (barW + gap);
            var y = pad.top + chartH - barH;
            ctx.fillRect(x, y, barW, barH);

            // Label
            ctx.fillStyle = '#9ca3af';
            ctx.font = '9px sans-serif';
            ctx.textAlign = 'center';
            var label = String(h.hour).padStart(2, '0') + ':00';
            ctx.fillText(label, x + barW / 2, H - 8);
            ctx.fillStyle = '#2563eb';
        });
    }

    // ── Health check ──────────────────────────────────────────────────────

    function runHealthCheck() {
        var $body = $('#healthBody');
        $body.html('<p class="ai-health-pending"><i class="fas fa-spinner fa-spin"></i> Checking…</p>');

        $.ajax({ url: '/health', type: 'GET', timeout: 10000 })
            .done(function (data, status, xhr) {
                var statusText = xhr.status === 200 ? 'Healthy' : 'Degraded';
                var dotClass = xhr.status === 200 ? 'ok' : 'warn';
                $body.html(
                    '<div class="ai-health-item">' +
                    '<span class="ai-health-dot ' + dotClass + '"></span>' +
                    '<span><strong>Health endpoint:</strong> ' + statusText + ' (HTTP ' + xhr.status + ')</span>' +
                    '</div>'
                );
            })
            .fail(function (xhr) {
                $body.html(
                    '<div class="ai-health-item">' +
                    '<span class="ai-health-dot fail"></span>' +
                    '<span><strong>Health endpoint:</strong> Unreachable (HTTP ' + (xhr.status || '?') + ')</span>' +
                    '</div>'
                );
            });
    }

    // ── Init ──────────────────────────────────────────────────────────────

    $(function () {
        fetchOverview();
        pollTimer = setInterval(fetchOverview, POLL_INTERVAL);

        $('#btnHealthCheck').on('click', runHealthCheck);

        // Stop polling when leaving the page
        $(window).on('beforeunload', function () {
            if (pollTimer) clearInterval(pollTimer);
        });
    });

})(jQuery);
