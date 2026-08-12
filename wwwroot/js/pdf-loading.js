async function generatePdfWithSpinner(button, url) {
    const originalContent = button.innerHTML;
    const originalWidth = button.offsetWidth;

    button.style.width = originalWidth + 'px';
    button.disabled = true;
    button.innerHTML = '<span class="btn-spinner"></span>';

    try {
        const response = await fetch(url);
        if (!response.ok) throw new Error('PDF generation failed');

        const blob = await response.blob();

        let fileName = 'document.pdf';
        const disposition = response.headers.get('Content-Disposition');
        if (disposition) {
            const match = disposition.match(/filename="?([^"]+)"?/);
            if (match) fileName = match[1];
        }

        const blobUrl = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = blobUrl;
        link.download = fileName;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        URL.revokeObjectURL(blobUrl);
    } catch (err) {
        alert('Failed to generate PDF. Please try again.');
    } finally {
        button.disabled = false;
        button.innerHTML = originalContent;
        button.style.width = '';
    }
}