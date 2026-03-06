(function () {
    var saved = localStorage.getItem('explorer-theme') || 'dark';
    document.documentElement.setAttribute('data-theme', saved);
})();

function toggleTheme() {
    var html = document.documentElement;
    var current = html.getAttribute('data-theme') || 'dark';
    var next = current === 'dark' ? 'light' : 'dark';
    html.setAttribute('data-theme', next);
    localStorage.setItem('explorer-theme', next);
    var icon = document.getElementById('themeIcon');
    if (icon) {
        icon.className = next === 'dark' ? 'bi bi-sun-fill' : 'bi bi-moon-fill';
    }
}

window.getDocumentTheme = function() {
    return document.documentElement.getAttribute('data-theme') || 'dark';
};

window.downloadCsv = function (filename, content) {
    var blob = new Blob([content], { type: 'text/csv;charset=utf-8;' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
