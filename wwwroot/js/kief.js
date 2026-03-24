/* ═══════════════════════════════════════════════════════
   KIEF PRODUCTIONS — Main JS
═══════════════════════════════════════════════════════ */

// ── Confirm Delete ────────────────────────────────────
document.addEventListener('DOMContentLoaded', function () {

    // Delete confirmation
    document.querySelectorAll('[data-confirm]').forEach(el => {
        el.addEventListener('click', function (e) {
            const msg = this.dataset.confirm || 'Are you sure you want to delete this?';
            if (!confirm(msg)) e.preventDefault();
        });
    });

    // Auto-dismiss alerts
    document.querySelectorAll('.alert[data-auto-dismiss]').forEach(el => {
        setTimeout(() => {
            el.style.opacity = '0';
            el.style.transition = 'opacity 0.4s';
            setTimeout(() => el.remove(), 400);
        }, 3500);
    });

    // Active nav item
    const currentPath = window.location.pathname.toLowerCase();
    document.querySelectorAll('.nav-item').forEach(link => {
        const href = link.getAttribute('href')?.toLowerCase();
        if (href && href !== '/' && currentPath.startsWith(href)) {
            link.classList.add('active');
        } else if (href === '/' && currentPath === '/') {
            link.classList.add('active');
        }
    });

    // Mobile sidebar toggle
    const sidebarToggle = document.getElementById('sidebarToggle');
    const sidebar = document.querySelector('.sidebar');
    if (sidebarToggle && sidebar) {
        sidebarToggle.addEventListener('click', () => {
            sidebar.classList.toggle('open');
        });
    }

    // Modal handling
    document.querySelectorAll('[data-modal-open]').forEach(btn => {
        btn.addEventListener('click', () => {
            const modal = document.getElementById(btn.dataset.modalOpen);
            if (modal) modal.classList.add('open');
        });
    });

    document.querySelectorAll('[data-modal-close]').forEach(btn => {
        btn.addEventListener('click', () => {
            const modal = btn.closest('.modal-backdrop');
            if (modal) modal.classList.remove('open');
        });
    });

    document.querySelectorAll('.modal-backdrop').forEach(backdrop => {
        backdrop.addEventListener('click', (e) => {
            if (e.target === backdrop) backdrop.classList.remove('open');
        });
    });

    // Format currency inputs on blur
    document.querySelectorAll('.currency-input').forEach(input => {
        input.addEventListener('blur', function () {
            const val = parseFloat(this.value);
            if (!isNaN(val)) this.value = val.toFixed(2);
        });
    });
});

// ── Format Currency ───────────────────────────────────
function formatCurrency(amount) {
    return 'R ' + parseFloat(amount || 0).toLocaleString('en-ZA', {
        minimumFractionDigits: 2,
        maximumFractionDigits: 2
    });
}

// ── Toast Notifications ───────────────────────────────
function showToast(message, type = 'success') {
    const container = document.getElementById('toastContainer') || createToastContainer();
    const toast = document.createElement('div');
    toast.className = `toast toast-${type}`;
    toast.innerHTML = `
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            ${type === 'success'
                ? '<path d="M20 6L9 17l-5-5"/>'
                : '<circle cx="12" cy="12" r="10"/><line x1="15" y1="9" x2="9" y2="15"/><line x1="9" y1="9" x2="15" y2="15"/>'}
        </svg>
        <span>${message}</span>
    `;

    const style = document.createElement('style');
    if (!document.getElementById('toast-styles')) {
        style.id = 'toast-styles';
        style.textContent = `
            #toastContainer { position:fixed; bottom:24px; right:24px; z-index:9999; display:flex; flex-direction:column; gap:8px; }
            .toast { display:flex; align-items:center; gap:10px; padding:12px 16px; border-radius:10px; font-size:13.5px; font-weight:500; min-width:240px; box-shadow:0 4px 20px rgba(0,0,0,0.4); animation:slideIn 0.3s ease; }
            .toast-success { background:#1a2e1a; color:#22c55e; border:1px solid rgba(34,197,94,0.2); }
            .toast-error   { background:#2e1a1a; color:#ef4444; border:1px solid rgba(239,68,68,0.2); }
            .toast-info    { background:#1a1f2e; color:#3b82f6; border:1px solid rgba(59,130,246,0.2); }
            @keyframes slideIn { from{opacity:0;transform:translateX(20px)} to{opacity:1;transform:translateX(0)} }
        `;
        document.head.appendChild(style);
    }

    container.appendChild(toast);
    setTimeout(() => {
        toast.style.opacity = '0';
        toast.style.transition = 'opacity 0.3s';
        setTimeout(() => toast.remove(), 300);
    }, 3000);
}

function createToastContainer() {
    const el = document.createElement('div');
    el.id = 'toastContainer';
    document.body.appendChild(el);
    return el;
}
