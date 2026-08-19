/**
 * EdCo Admin Portal - Global Site Scripts
 * Handles theme toggling, anti-CSRF request interceptors, sidebar behavior, and MathJax setup.
 */

// MathJax Configuration
window.MathJax = {
    tex: {
        inlineMath: [['$', '$'], ['\\(', '\\)']],
        displayMath: [['$$', '$$'], ['\\[', '\\]']]
    },
    svg: {
        fontCache: 'global'
    }
};

// Theme UI Handler
function updateThemeUI(theme) {
    var icon = document.getElementById('themeToggleIcon');
    var label = document.querySelector('.theme-toggle-label');
    if (theme === 'light') {
        if (icon) icon.className = 'fa-solid fa-sun text-warning';
        if (label) label.textContent = 'Light';
    } else {
        if (icon) icon.className = 'fa-solid fa-moon text-info';
        if (label) label.textContent = 'Dark';
    }
}

// Global Event Listeners & Request Interceptors
document.addEventListener('DOMContentLoaded', function () {
    // 1. Theme Management
    var currentTheme = document.documentElement.getAttribute('data-bs-theme') || 'dark';
    updateThemeUI(currentTheme);

    var themeBtn = document.getElementById('themeToggleBtn');
    if (themeBtn) {
        themeBtn.addEventListener('click', function (e) {
            e.preventDefault();
            var active = document.documentElement.getAttribute('data-bs-theme') || 'dark';
            var next = active === 'dark' ? 'light' : 'dark';
            document.documentElement.setAttribute('data-bs-theme', next);
            localStorage.setItem('edco_theme', next);
            updateThemeUI(next);
        });
    }

    // 2. Sidebar Collapsible Handler
    var sidebar = document.getElementById('sidebar');
    var mainContent = document.getElementById('main-content');
    var toggleBtn = document.getElementById('sidebarToggle');

    if (sidebar && mainContent) {
        var isCollapsed = localStorage.getItem('sidebarCollapsed') === 'true';
        if (isCollapsed) {
            sidebar.classList.add('collapsed');
            mainContent.classList.add('expanded');
        }

        if (toggleBtn) {
            toggleBtn.addEventListener('click', function (e) {
                e.preventDefault();
                sidebar.classList.toggle('collapsed');
                mainContent.classList.toggle('expanded');
                localStorage.setItem('sidebarCollapsed', sidebar.classList.contains('collapsed'));
            });
        }
    }
});

// 3. Setup global jQuery Anti-CSRF token header (if jQuery is present)
if (typeof $ !== 'undefined') {
    $.ajaxSetup({
        headers: {
            'RequestVerificationToken': $('meta[name="request-verification-token"]').attr('content')
        }
    });
}

// 4. Setup global fetch Anti-CSRF token header interceptor
(function () {
    const originalFetch = window.fetch;
    window.fetch = async function (resource, config) {
        config = config || {};
        const method = (config.method || 'GET').toUpperCase();
        if (method !== 'GET' && method !== 'HEAD') {
            config.headers = config.headers || {};
            const token = document.querySelector('meta[name="request-verification-token"]')?.getAttribute('content');
            if (token) {
                if (typeof Headers !== 'undefined' && config.headers instanceof Headers) {
                    if (!config.headers.has('RequestVerificationToken')) {
                        config.headers.append('RequestVerificationToken', token);
                    }
                } else if (Array.isArray(config.headers)) {
                    config.headers.push(['RequestVerificationToken', token]);
                } else if (typeof config.headers === 'object') {
                    if (!config.headers['RequestVerificationToken']) {
                        config.headers['RequestVerificationToken'] = token;
                    }
                }
            }
        }
        return originalFetch(resource, config);
    };
})();
