/**
 * Mobile Navigation — Drawer, account dropdown, and keyboard interactions.
 * Handles:
 *   - Hamburger → open off-canvas drawer
 *   - Backdrop tap → close drawer
 *   - Nav link click inside drawer → close drawer
 *   - Escape key → close drawer and account dropdown
 *   - Avatar click → toggle mobile account dropdown
 *   - Outside click → close account dropdown
 */
(function () {
    var appShell = document.getElementById('appShell');
    if (!appShell) return;

    // --- Drawer interactions ---

    var hamburger = document.querySelector('.mobile-topbar__hamburger');
    var backdrop = document.querySelector('.mobile-backdrop');
    var sidebar = document.querySelector('.sidebar');

    function openDrawer() {
        appShell.classList.add('drawer-open');
    }

    function closeDrawer() {
        appShell.classList.remove('drawer-open');
    }

    if (hamburger) {
        hamburger.addEventListener('click', openDrawer);
    }

    if (backdrop) {
        backdrop.addEventListener('click', closeDrawer);
    }

    // Close drawer when clicking nav links inside sidebar
    if (sidebar) {
        sidebar.addEventListener('click', function (e) {
            if (e.target.closest('.nav-item') || e.target.closest('.nav-sub-item')) {
                closeDrawer();
            }
        });
    }

    // Escape key closes drawer and account dropdown
    document.addEventListener('keydown', function (e) {
        if (e.key === 'Escape') {
            closeDrawer();
            closeMobileAccount();
        }
    });

    // --- Mobile account dropdown ---

    var avatar = document.querySelector('.mobile-topbar__avatar');
    var mobileAccountDropdown = document.getElementById('mobileAccountDropdown');

    function closeMobileAccount() {
        if (mobileAccountDropdown) {
            mobileAccountDropdown.style.display = 'none';
        }
    }

    if (avatar && mobileAccountDropdown) {
        avatar.addEventListener('click', function () {
            var isVisible = mobileAccountDropdown.style.display === 'block';
            mobileAccountDropdown.style.display = isVisible ? 'none' : 'block';
        });
    }

    // Close mobile account dropdown when clicking outside
    document.addEventListener('click', function (e) {
        if (mobileAccountDropdown && avatar) {
            if (!avatar.contains(e.target) && !mobileAccountDropdown.contains(e.target)) {
                closeMobileAccount();
            }
        }
    });
})();
