/**
 * admin-user-analytics.js — User Analytics dashboard
 * Chart.js loaded from CDN in the view. jQuery + Bootstrap already loaded.
 */
(function ($) {
    'use strict';

    var state = {
        page: 1,
        pageSize: 10,
        search: '',
        sortBy: 'posts',
        sortDir: 'desc',
        selectedUserId: null,
        selectedUserName: '',
        dateRange: '30d',
        userCache: {}
    };

    var chartInstances = {};

    function destroyCharts() {
        ['chartViewsTime', 'chartClapsPost', 'chartTraffic'].forEach(function (id) {
            if (chartInstances[id]) { chartInstances[id].destroy(); delete chartInstances[id]; }
        });
    }

    function showSkeleton(container) {
        $(container).find('.ua-chart-skeleton, .ua-heatmap-skeleton').show();
        $(container).find('canvas, .ua-heatmap-grid').hide();
    }

    function hideSkeleton(container) {
        $(container).find('.ua-chart-skeleton, .ua-heatmap-skeleton').hide();
        $(container).find('canvas, .ua-heatmap-grid').show();
    }

    function formatReadTime(minutes) {
        if (!minutes || minutes === 0) return '--';
        var m = Math.floor(minutes);
        var s = Math.round((minutes - m) * 60);
        if (s === 60) { m++; s = 0; }
        if (m > 0) return m + 'm ' + s + 's';
        return s + 's';
    }

    function formatLargeNum(n) {
        if (n >= 1000000) return (n / 1000000).toFixed(1) + 'M';
        if (n >= 1000) return (n / 1000).toFixed(1) + 'K';
        return String(n);
    }

    function getInitials(name) {
        if (!name) return '?';
        return name.charAt(0).toUpperCase();
    }

    // ── User List ──────────────────────────────────────────────────────────

    function buildUserListUrl() {
        var params = new URLSearchParams();
        if (state.search) params.set('search', state.search);
        params.set('sortBy', state.sortBy);
        params.set('sortDir', state.sortDir);
        params.set('page', String(state.page));
        params.set('pageSize', String(state.pageSize));
        return '/admin/api/users?' + params.toString();
    }

    function loadUserList() {
        $('#uaTableBody').html(
            '<div class="ua-table-skeleton">' +
            new Array(10).fill('<div class="skeleton-row" style="height:56px;margin:4px 8px;"></div>').join('') +
            '</div>'
        );

        $.getJSON(buildUserListUrl())
            .done(function (data) {
                renderUserTable(data);
            })
            .fail(function () {
                $('#uaTableBody').html(
                    '<div style="padding:20px;text-align:center;color:#dc2626;font-size:0.85rem;">' +
                    'Failed to load users. Check console.</div>'
                );
            });
    }

    function renderUserTable(data) {
        var items = data.items || [];
        var total = data.total || 0;
        var totalPages = Math.max(1, Math.ceil(total / state.pageSize));

        var html = '';
        items.forEach(function (u) {
            var avatarHtml = u.avatarUrl
                ? '<img src="' + escapeHtml(u.avatarUrl) + '" class="ua-user-avatar" alt="" />'
                : '<div class="ua-user-avatar">' + getInitials(u.displayName || u.username) + '</div>';
            var name = u.displayName || u.username || 'Unknown';
            var isActive = u.id === state.selectedUserId ? ' active' : '';

            html += '<div class="ua-user-row' + isActive + '" data-user-id="' + u.id + '" data-user-name="' + escapeAttr(name) + '">' +
                '<div class="ua-user-cell">' +
                    avatarHtml +
                    '<div class="ua-user-info">' +
                        '<span class="ua-user-name">' + escapeHtml(name) + '</span>' +
                        '<span class="ua-user-email">' + escapeHtml(u.email || '') + '</span>' +
                    '</div>' +
                '</div>' +
                '<div class="ua-cell-num">' + u.postCount + '</div>' +
                '<div class="ua-cell-num">' + formatLargeNum(u.totalViews) + '</div>' +
                '<div class="ua-cell-num">' + formatLargeNum(u.totalClaps) + '</div>' +
                '<div class="ua-cell-num">' + formatReadTime(u.averageReadTimeMinutes) + '</div>' +
                '<div class="ua-cell-date">' + escapeHtml(u.dateJoined || '') + '</div>' +
            '</div>';
        });

        if (!items.length) {
            html = '<div style="padding:30px;text-align:center;color:var(--muted);font-size:0.85rem;">No users found.</div>';
        }

        $('#uaTableBody').html(html);
        renderPagination(total, totalPages);

        updateSortIndicators();
    }

    function renderPagination(total, totalPages) {
        if (totalPages <= 1) {
            $('#uaPagination').html(
                '<div style="padding:10px 14px;font-size:0.75rem;color:var(--muted);border-top:1px solid var(--line);">' +
                total + ' users</div>'
            );
            return;
        }

        var nav = '';
        nav += '<button class="ua-page-btn" ' + (state.page <= 1 ? 'disabled' : '') + ' data-page="1">' +
            '<i class="fas fa-angles-left" style="font-size:0.6rem;"></i></button>';
        nav += '<button class="ua-page-btn" ' + (state.page <= 1 ? 'disabled' : '') + ' data-page="' + (state.page - 1) + '">' +
            '<i class="fas fa-angle-left" style="font-size:0.6rem;"></i></button>';

        var start = Math.max(1, state.page - 2);
        var end = Math.min(totalPages, start + 4);
        start = Math.max(1, end - 4);

        for (var p = start; p <= end; p++) {
            nav += '<button class="ua-page-btn' + (p === state.page ? ' active' : '') + '" data-page="' + p + '">' + p + '</button>';
        }

        nav += '<button class="ua-page-btn" ' + (state.page >= totalPages ? 'disabled' : '') + ' data-page="' + (state.page + 1) + '">' +
            '<i class="fas fa-angle-right" style="font-size:0.6rem;"></i></button>';
        nav += '<button class="ua-page-btn" ' + (state.page >= totalPages ? 'disabled' : '') + ' data-page="' + totalPages + '">' +
            '<i class="fas fa-angles-right" style="font-size:0.6rem;"></i></button>';

        $('#uaPagination').html(
            '<div style="display:flex;align-items:center;justify-content:space-between;padding:10px 14px;border-top:1px solid var(--line);">' +
            '<span style="font-size:0.75rem;color:var(--muted);">' + total + ' users</span>' +
            '<div class="ua-pagination-nav">' + nav + '</div>' +
            '</div>'
        );
    }

    function updateSortIndicators() {
        $('.ua-th.sortable').removeClass('active').find('i').attr('class', 'fas fa-sort');
        $('.ua-th.sortable[data-sort="' + state.sortBy + '"]').addClass('active').find('i')
            .attr('class', state.sortDir === 'asc' ? 'fas fa-sort-up' : 'fas fa-sort-down');
    }

    function escapeHtml(str) {
        var d = document.createElement('div');
        d.textContent = str || '';
        return d.innerHTML;
    }

    function escapeAttr(str) {
        return (str || '').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    // ── Analytics Detail ──────────────────────────────────────────────────

    function selectUser(userId, userName) {
        state.selectedUserId = userId;
        state.selectedUserName = userName;

        $('.ua-user-row').removeClass('active');
        $('.ua-user-row[data-user-id="' + userId + '"]').addClass('active');

        $('#uaDetailEmpty').hide();
        $('#uaDetailContent').show();

        resetDetailShell();
        loadUserAnalytics(userId);
        loadUserPosts(userId);
    }

    function resetDetailShell() {
        $('#uaTotalViews').text('--');
        $('#uaTotalClaps').text('--');
        $('#uaAvgReadTime').text('--');
        $('#uaAvgScroll').text('--');
        $('#uaPostsBody').html('');
        showSkeleton('#uaSummaryRow');
        destroyCharts();
        showSkeleton($('#uaMain'));
    }

    function loadUserAnalytics(userId) {
        var range = state.dateRange;

        $.getJSON('/admin/api/users/' + userId + '/analytics?range=' + range)
            .done(function (data) {
                renderSummary(data.summary);
                renderViewsTimeChart(data.dailyViews);
                renderClapsPostChart(data.clapsPerPost);
                renderTrafficChart(data.trafficSources);
                renderHeatmap(data.heatmapData);
            })
            .fail(function () {
                console.error('Failed to load user analytics for #' + userId);
            });
    }

    function loadUserPosts(userId) {
        $('#uaPostsTableWrap').hide();
        $('.ua-posts-skeleton').show();

        $.getJSON('/admin/api/users/' + userId + '/posts')
            .done(function (data) {
                renderPostsTable(data);
            })
            .fail(function () {
                console.error('Failed to load posts for #' + userId);
            });
    }

    // ── Summary Cards ─────────────────────────────────────────────────────

    function renderSummary(summary) {
        hideSkeleton('#uaSummaryRow');
        $('#uaTotalViews').text(formatLargeNum(summary.totalViews));
        $('#uaTotalClaps').text(formatLargeNum(summary.totalClaps));
        $('#uaAvgReadTime').text(formatReadTime(summary.averageReadTimeMinutes));
        $('#uaAvgScroll').text(summary.averageScrollRate + '%');
    }

    // ── Chart 1: Views Over Time (Bar) ────────────────────────────────────

    function renderViewsTimeChart(dailyViews) {
        var $canvas = $('#chartViewsTime');
        var $card = $canvas.closest('.ua-chart-card');

        destroyCharts();
        hideSkeleton($card);
        $canvas.show();

        if (!dailyViews || !dailyViews.length) {
            $canvas.hide();
            $canvas.after('<div style="text-align:center;padding:40px;color:var(--muted);">No view data in this period.</div>');
            return;
        }

        var labels = dailyViews.map(function (d) {
            var dt = new Date(d.date + 'T00:00:00');
            return dt.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
        });
        var values = dailyViews.map(function (d) { return d.count; });

        var ctx = $canvas[0].getContext('2d');
        chartInstances.chartViewsTime = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Views',
                    data: values,
                    backgroundColor: '#2563eb',
                    borderRadius: 4,
                    barThickness: Math.max(6, Math.min(20, 600 / labels.length))
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { font: { size: 9 }, maxTicksLimit: 14 }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: '#f0f0f0' },
                        ticks: { font: { size: 10 } }
                    }
                }
            }
        });
    }

    // ── Chart 2: Claps Per Post (Horizontal Bar) ──────────────────────────

    function renderClapsPostChart(clapsPerPost) {
        var $canvas = $('#chartClapsPost');
        var $card = $canvas.closest('.ua-chart-card');

        hideSkeleton($card);
        $canvas.show();

        if (!clapsPerPost || !clapsPerPost.length) {
            $canvas.hide();
            $canvas.after('<div style="text-align:center;padding:40px;color:var(--muted);">No posts yet.</div>');
            return;
        }

        var top = clapsPerPost.slice(0, 15);
        var labels = top.map(function (p) {
            return (p.title || 'Untitled').length > 30
                ? (p.title || 'Untitled').substring(0, 28) + '…'
                : (p.title || 'Untitled');
        });
        var values = top.map(function (p) { return p.claps; });

        var ctx = $canvas[0].getContext('2d');
        chartInstances.chartClapsPost = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Claps',
                    data: values,
                    backgroundColor: '#f59e0b',
                    borderRadius: 4,
                    barThickness: Math.max(8, Math.min(16, 300 / labels.length))
                }]
            },
            options: {
                indexAxis: 'y',
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { display: false } },
                scales: {
                    x: {
                        beginAtZero: true,
                        grid: { color: '#f0f0f0' },
                        ticks: { font: { size: 10 }, precision: 0 }
                    },
                    y: {
                        grid: { display: false },
                        ticks: { font: { size: 10 } }
                    }
                }
            }
        });
    }

    // ── Chart 3: Read Time Trend (Line) ───────────────────────────────────

    function renderReadTrendChart(readTimeTrend) {
        var $canvas = $('#chartReadTrend');
        var $card = $canvas.closest('.ua-chart-card');

        hideSkeleton($card);
        $canvas.show();

        if (!readTimeTrend || !readTimeTrend.length) {
            $canvas.hide();
            $canvas.after('<div style="text-align:center;padding:40px;color:var(--muted);">No posts yet.</div>');
            return;
        }

        var labels = readTimeTrend.map(function (p, i) { return '#' + (i + 1); });
        var values = readTimeTrend.map(function (p) { return p.readTime; });

        var ctx = $canvas[0].getContext('2d');
        chartInstances.chartReadTrend = new Chart(ctx, {
            type: 'line',
            data: {
                labels: labels,
                datasets: [{
                    label: 'Read Time (min)',
                    data: values,
                    borderColor: '#7c3aed',
                    backgroundColor: 'rgba(124,58,237,0.08)',
                    fill: true,
                    tension: 0.3,
                    pointRadius: 3,
                    pointBackgroundColor: '#7c3aed'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        callbacks: {
                            title: function (ctx) {
                                var i = ctx[0].dataIndex;
                                return readTimeTrend[i] ? readTimeTrend[i].title : '';
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        grid: { display: false },
                        ticks: { font: { size: 9 } },
                        title: { display: true, text: 'Posts (chronological)', font: { size: 9 } }
                    },
                    y: {
                        beginAtZero: true,
                        grid: { color: '#f0f0f0' },
                        ticks: { font: { size: 10 } },
                        title: { display: true, text: 'Minutes', font: { size: 9 } }
                    }
                }
            }
        });
    }

    // ── Chart 4: Traffic Sources (Donut) ──────────────────────────────────

    function renderTrafficChart(trafficSources) {
        var $canvas = $('#chartTraffic');
        var $card = $canvas.closest('.ua-chart-card');

        hideSkeleton($card);
        $canvas.show();

        if (!trafficSources || !trafficSources.length) {
            $canvas.hide();
            $canvas.after('<div style="text-align:center;padding:40px;color:var(--muted);">No traffic data.</div>');
            return;
        }

        var labels = trafficSources.map(function (s) { return s.source; });
        var values = trafficSources.map(function (s) { return s.count; });
        var colors = {
            'Direct': '#2563eb',
            'Search': '#16a34a',
            'Social': '#f59e0b',
            'Referral': '#dc2626'
        };
        var bgColors = labels.map(function (l) { return colors[l] || '#9ca3af'; });

        var ctx = $canvas[0].getContext('2d');
        chartInstances.chartTraffic = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [{
                    data: values,
                    backgroundColor: bgColors,
                    borderWidth: 2,
                    borderColor: '#fff'
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { position: 'bottom', labels: { padding: 16, font: { size: 11 } } }
                }
            }
        });
    }

    // ── Activity Heatmap ──────────────────────────────────────────────────

    function renderHeatmap(heatmapData) {
        var $grid = $('#uaHeatmap');
        var $card = $grid.closest('.ua-chart-card');
        hideSkeleton($card);
        $grid.show().empty();

        if (!heatmapData || !heatmapData.length) {
            $grid.html('<div style="text-align:center;padding:20px;color:var(--muted);">No activity data.</div>');
            return;
        }

        var maxViews = 1;
        heatmapData.forEach(function (d) { if (d.views > maxViews) maxViews = d.views; });

        var dayLabels = ['Mon', '', 'Wed', '', 'Fri', '', 'Sun'];
        var weeks = 12;
        var daysPerWeek = 7;

        for (var row = 0; row < daysPerWeek; row++) {
            var weekHtml = '<div class="ua-heatmap-week-row"><div class="ua-heatmap-day-label">' + dayLabels[row] + '</div>';
            for (var col = 0; col < weeks; col++) {
                var di = col * daysPerWeek + row;
                var d = heatmapData[di];
                var views = d ? d.views : 0;
                var level = views === 0 ? 0 : (views <= maxViews * 0.25 ? 1 : (views <= maxViews * 0.5 ? 2 : (views <= maxViews * 0.75 ? 3 : 4)));
                var dateLabel = d ? formatHeatmapDate(d.date) : '';
                weekHtml += '<div class="ua-heatmap-cell level-' + level + '" title="' + dateLabel + ' — ' + views + ' views">' +
                    '<div class="ua-heatmap-tooltip">' + dateLabel + ' — ' + views + ' views</div>' +
                    '</div>';
            }
            weekHtml += '</div>';
            $grid.append(weekHtml);
        }

        // Legend
        $grid.append(
            '<div style="display:flex;align-items:center;gap:4px;margin-top:8px;font-size:0.65rem;color:var(--muted);">' +
            '<span style="margin-right:4px;">Less</span>' +
            '<span class="ua-heatmap-cell level-0" style="display:inline-block;cursor:default;"></span>' +
            '<span class="ua-heatmap-cell level-1" style="display:inline-block;cursor:default;"></span>' +
            '<span class="ua-heatmap-cell level-2" style="display:inline-block;cursor:default;"></span>' +
            '<span class="ua-heatmap-cell level-3" style="display:inline-block;cursor:default;"></span>' +
            '<span class="ua-heatmap-cell level-4" style="display:inline-block;cursor:default;"></span>' +
            '<span style="margin-left:4px;">More</span>' +
            '</div>'
        );
    }

    function formatHeatmapDate(dateStr) {
        var dt = new Date(dateStr + 'T00:00:00');
        return dt.toLocaleDateString('en-US', { weekday: 'short', month: 'short', day: 'numeric' });
    }

    // ── Posts Table ───────────────────────────────────────────────────────

    function renderPostsTable(posts) {
        $('.ua-posts-skeleton').hide();
        $('#uaPostsTableWrap').show();

        if (!posts || !posts.length) {
            $('#uaPostsBody').html(
                '<tr><td colspan="8" style="text-align:center;padding:20px;color:var(--muted);">No published posts.</td></tr>'
            );
            return;
        }

        var html = '';
        posts.forEach(function (p) {
            var title = p.title || 'Untitled';
            var truncated = title.length > 40 ? title.substring(0, 38) + '…' : title;
            var postUrl = '/Blog/Details/' + (p.slug || p.id);

            html += '<tr>' +
                '<td><div class="ua-post-title"><a href="#" target="_blank">' + escapeHtml(truncated) + '</a></div></td>' +
                '<td>' + escapeHtml(p.publishedAt || '') + '</td>' +
                '<td>' + formatLargeNum(p.totalViews) + '</td>' +
                '<td>' + formatLargeNum(p.uniqueVisitors) + '</td>' +
                '<td>' + formatLargeNum(p.totalClaps) + '</td>' +
                '<td>' + formatReadTime(p.avgReadTimeMinutes) + '</td>' +
                '<td>' + p.scrollCompletionPercent + '%</td>' +
                '<td><button class="btn btn-sm btn-secondary btn-heatmap-view" data-post-id="' + p.id + '" data-title="' + escapeAttr(title) + '">' +
                '<i class="fas fa-fire"></i> Heatmap</button></td>' +
                '</tr>';
        });

        $('#uaPostsBody').html(html);
    }

    // ── Scroll Heatmap Modal ──────────────────────────────────────────────

    function openHeatmapModal(postId, postTitle) {
        $('#uaModalTitle').text(postTitle || 'Scroll Heatmap');
        $('#uaModalBody').html('<div style="text-align:center;padding:30px;color:var(--muted);"><i class="fas fa-spinner fa-spin"></i> Loading…</div>');
        $('#uaHeatmapModal').fadeIn(150);

        $.getJSON('/admin/api/posts/' + postId + '/heatmap')
            .done(function (data) {
                renderScrollHeatmap(data);
            })
            .fail(function () {
                $('#uaModalBody').html('<div style="text-align:center;padding:30px;color:#dc2626;">Failed to load heatmap data.</div>');
            });
    }

    function renderScrollHeatmap(data) {
        var sections = data.sections || [];
        if (!sections.length) {
            $('#uaModalBody').html('<div style="text-align:center;padding:30px;color:var(--muted);">No scroll depth data for this post yet.</div>');
            return;
        }

        var html = '<div class="ua-scroll-heatmap">';
        sections.forEach(function (s) {
            var colorClass = 'ua-scroll-green';
            if (s.percentage < 30) colorClass = 'ua-scroll-red';
            else if (s.percentage < 60) colorClass = 'ua-scroll-yellow';

            html += '<div class="ua-scroll-section">' +
                '<span class="ua-scroll-label">' + escapeHtml(s.label) + '</span>' +
                '<div class="ua-scroll-bar ' + colorClass + '" style="width:' + Math.max(10, s.percentage) + '%">' +
                '<span>' + s.percentage + '%</span>' +
                '</div>' +
            '</div>';
        });
        html += '</div>';

        $('#uaModalBody').html(html);
    }

    function closeHeatmapModal() {
        $('#uaHeatmapModal').fadeOut(150);
    }

    // ── CSV Export ────────────────────────────────────────────────────────

    function exportCSV() {
        if (!state.selectedUserId) {
            alert('Please select a user first.');
            return;
        }

        $.getJSON('/admin/api/users/' + state.selectedUserId + '/posts')
            .done(function (posts) {
                if (!posts || !posts.length) {
                    alert('No posts to export.');
                    return;
                }

                var headers = ['Post Title', 'Published', 'Total Views', 'Unique Visitors', 'Total Claps', 'Avg Read Time (min)', 'Scroll Completion %'];
                var rows = [headers.join(',')];

                posts.forEach(function (p) {
                    rows.push([
                        '"' + (p.title || '').replace(/"/g, '""') + '"',
                        '"' + (p.publishedAt || '') + '"',
                        p.totalViews,
                        p.uniqueVisitors,
                        p.totalClaps,
                        p.avgReadTimeMinutes,
                        p.scrollCompletionPercent
                    ].join(','));
                });

                var csv = rows.join('\n');
                var blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
                var url = URL.createObjectURL(blob);
                var link = document.createElement('a');
                link.href = url;
                link.download = 'analytics-' + (state.selectedUserName || 'user') + '.csv';
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                URL.revokeObjectURL(url);
            })
            .fail(function () {
                alert('Failed to load data for export.');
            });
    }

    // ── Event Bindings ────────────────────────────────────────────────────

    // Search
    var searchTimer;
    $('#uaSearch').on('input', function () {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(function () {
            state.search = $('#uaSearch').val();
            state.page = 1;
            loadUserList();
        }, 300);
    });

    // Sort headers
    $('.ua-table-card').on('click', '.ua-th.sortable', function () {
        var sortBy = $(this).data('sort');
        if (state.sortBy === sortBy) {
            state.sortDir = state.sortDir === 'asc' ? 'desc' : 'asc';
        } else {
            state.sortBy = sortBy;
            state.sortDir = 'desc';
        }
        state.page = 1;
        loadUserList();
    });

    // User row click
    $('.ua-table-card').on('click', '.ua-user-row', function () {
        var userId = parseInt($(this).data('user-id'));
        var userName = $(this).data('user-name') || '';
        selectUser(userId, userName);
    });

    // Pagination
    $('#uaPagination').on('click', '.ua-page-btn:not(:disabled)', function () {
        var page = parseInt($(this).data('page'));
        if (page && page !== state.page) {
            state.page = page;
            loadUserList();
        }
    });

    // Date range buttons
    $('.ua-range-btn').on('click', function () {
        $('.ua-range-btn').removeClass('active');
        $(this).addClass('active');
        state.dateRange = $(this).data('range');
        if (state.selectedUserId) {
            loadUserAnalytics(state.selectedUserId);
        }
    });

    // User panel toggle
    $('#uaToggleUsers').on('click', toggleUserPanel);

    // Export CSV
    $('#btnExportCSV').on('click', exportCSV);

    // Heatmap modal
    $('#uaMain').on('click', '.btn-heatmap-view', function () {
        var postId = $(this).data('post-id');
        var postTitle = $(this).data('title') || 'Post';
        openHeatmapModal(postId, postTitle);
    });

    $('#uaModalClose').on('click', closeHeatmapModal);
    $('#uaHeatmapModal').on('click', function (e) {
        if (e.target === this) closeHeatmapModal();
    });

    $(document).on('keydown', function (e) {
        if (e.key === 'Escape' && $('#uaHeatmapModal').is(':visible')) {
            closeHeatmapModal();
        }
    });

    function toggleUserPanel() {
        var $content = $('#uaContent');
        var hidden = $content.hasClass('has-panel-hidden');
        if (hidden) {
            $content.removeClass('has-panel-hidden');
            sessionStorage.setItem('uaPanelHidden', '0');
        } else {
            $content.addClass('has-panel-hidden');
            sessionStorage.setItem('uaPanelHidden', '1');
        }
    }

    // ── Init ──────────────────────────────────────────────────────────────
    $(function () {
        if (sessionStorage.getItem('uaPanelHidden') === '1') {
            $('#uaContent').addClass('has-panel-hidden');
        }
        loadUserList();
    });

})(jQuery);
