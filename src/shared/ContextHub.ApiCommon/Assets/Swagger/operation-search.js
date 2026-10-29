// Adapted from the Asset Management Swagger search; native controls keep the shared UI small.
(() => {
    'use strict';

    const methods = new Set(['get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace']);
    const normalize = value => String(value ?? '').trim().toLowerCase();
    let documentState;
    let operationsByTag = new Map();
    let controls;
    let frame;

    // Read the already loaded document; collapsed controllers have no operation DOM to index.
    function readDocument() {
        const state = globalThis.ui?.specSelectors?.specJson();
        if (!state || state === documentState) return;
        documentState = state;
        const specification = state.toJS();
        const operations = Object.entries(specification.paths ?? {}).flatMap(([path, item]) =>
            Object.entries(item).filter(([method]) => methods.has(method)).flatMap(([method, operation]) =>
                (operation.tags?.length ? operation.tags : ['default']).map(tag => ({
                    tag,
                    key: `${method} ${path}`,
                    text: normalize(`${method} ${path} ${operation.summary ?? ''} ${tag}`)
                }))));
        operationsByTag = Map.groupBy(operations, operation => operation.tag);

        const selected = controls.scope.value;
        const tags = [...operationsByTag.keys()].sort();
        controls.scope.replaceChildren(new Option('All controllers', ''));
        tags.forEach(tag => controls.scope.add(new Option(tag, tag)));
        controls.scope.value = tags.includes(selected) ? selected : '';
    }

    // Keep the original Swagger filter empty so it never removes operations before our filter runs.
    function createControls() {
        const native = document.querySelector('.swagger-ui .filter input');
        if (!native || controls?.input.isConnected) return;
        native.hidden = true;
        native.setAttribute('aria-hidden', 'true');
        native.tabIndex = -1;

        const container = document.createElement('div');
        container.className = 'api-operation-search';
        container.style.cssText = 'display:flex;gap:12px;flex-wrap:wrap;width:100%';
        const scope = document.createElement('select');
        scope.setAttribute('aria-label', 'Controller');
        scope.style.cssText = 'min-width:180px;max-width:100%';
        const input = document.createElement('input');
        input.type = 'search';
        input.placeholder = 'Search API by path, method, summary, tag';
        input.setAttribute('aria-label', 'Search API');
        input.style.cssText = 'flex:1;min-width:180px';
        container.append(scope, input);
        native.after(container);
        controls = { scope, input };
        documentState = undefined;
        scope.addEventListener('change', schedule);
        input.addEventListener('input', schedule);
    }

    // Filter within the cached controller group and use key lookups for mounted operation rows.
    function applyFilter() {
        frame = undefined;
        createControls();
        if (!controls?.input.isConnected) return;
        readDocument();
        const query = normalize(controls.input.value);
        const scope = controls.scope.value;
        document.querySelectorAll('.swagger-ui .opblock-tag-section').forEach(section => {
            const header = section.querySelector('.opblock-tag');
            const tag = header?.dataset.tag ?? header?.querySelector('a')?.textContent?.trim() ?? '';
            const inScope = !scope || scope === tag;
            const matches = inScope
                ? (operationsByTag.get(tag) ?? []).filter(operation => !query || operation.text.includes(query))
                : [];
            const visibleKeys = new Set(matches.map(operation => operation.key));
            section.style.display = matches.length === 0 ? 'none' : '';
            section.querySelectorAll('.opblock').forEach(block => {
                const method = normalize(block.querySelector('.opblock-summary-method')?.textContent);
                const pathElement = block.querySelector('.opblock-summary-path');
                const path = pathElement?.dataset.path ?? pathElement?.textContent?.trim();
                block.hidden = !visibleKeys.has(`${method} ${path}`);
            });
        });
    }

    // Coalesce document rendering and user input without observing our own visibility changes.
    function schedule() {
        if (frame === undefined) frame = requestAnimationFrame(applyFilter);
    }

    function start() {
        new MutationObserver(schedule).observe(document.body, { childList: true, subtree: true });
        schedule();
    }

    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', start, { once: true });
    else start();
})();
