(() => {
    const key = 'contexthub_theme_pref';
    const media = window.matchMedia('(prefers-color-scheme: dark)');
    let preference = 'system';
    try { preference = localStorage.getItem(key) || 'system'; } catch (error) {
        if (!(error instanceof DOMException) || !['SecurityError', 'QuotaExceededError'].includes(error.name)) throw error;
        preference = 'system';
        document.documentElement.dataset.themePersistence = 'unavailable';
    }
    if (!['system', 'light', 'dark'].includes(preference)) preference = 'system';
    const apply = () => {
        document.documentElement.dataset.theme = preference === 'system'
            ? (media.matches ? 'dark' : 'light') : preference;
    };
    apply();
    media.addEventListener('change', apply);
    document.addEventListener('DOMContentLoaded', () => {
        const select = document.getElementById('sso-theme');
        if (!select) return;
        select.value = preference;
        select.addEventListener('change', () => {
            preference = select.value;
            apply();
            try { localStorage.setItem(key, preference); } catch (error) {
                if (!(error instanceof DOMException) || !['SecurityError', 'QuotaExceededError'].includes(error.name)) throw error;
                document.documentElement.dataset.themePersistence = 'unavailable';
            }
        });
    });
})();
