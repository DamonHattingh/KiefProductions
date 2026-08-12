function makeDynamicTable(options) {
    const {
        containerId,   // e.g. 'invoiceResults'
        searchInputId, // e.g. 'searchBox'
        url,           // e.g. '/Invoices/Index'
        debounceMs = 300
    } = options;

    const results = document.getElementById(containerId);
    const searchBox = document.getElementById(searchInputId);
    let debounceTimer;

    function loadResults(search, page) {
        const params = new URLSearchParams({ search: search || '', page: page || 1 });
        fetch(`${url}?${params.toString()}`, {
            headers: { 'X-Requested-With': 'XMLHttpRequest' }
        })
            .then(res => res.text())
            .then(html => {
                results.innerHTML = html;
                attachPageLinks();
            });
    }

    function attachPageLinks() {
        results.querySelectorAll('.page-btn').forEach(link => {
            link.addEventListener('click', function (e) {
                e.preventDefault();
                if (this.classList.contains('disabled')) return;
                loadResults(searchBox ? searchBox.value : '', this.getAttribute('data-page'));
            });
        });
    }

    if (searchBox) {
        searchBox.addEventListener('input', function () {
            clearTimeout(debounceTimer);
            debounceTimer = setTimeout(() => loadResults(this.value, 1), debounceMs);
        });
    }

    attachPageLinks();
}