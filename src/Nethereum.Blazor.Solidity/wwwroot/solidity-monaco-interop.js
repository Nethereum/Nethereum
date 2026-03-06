const MONACO_CDN = 'https://cdn.jsdelivr.net/npm/monaco-editor@0.52.2/min';
let monacoLoaded = false;
let monacoLoadPromise = null;
const editors = {};
const decorations = {};
const models = {};

function loadMonaco() {
    if (monacoLoaded) return Promise.resolve();
    if (monacoLoadPromise) return monacoLoadPromise;

    monacoLoadPromise = new Promise((resolve, reject) => {
        const script = document.createElement('script');
        script.src = `${MONACO_CDN}/vs/loader.js`;
        script.onload = () => {
            require.config({ paths: { vs: `${MONACO_CDN}/vs` } });
            require(['vs/editor/editor.main'], async () => {
                await registerSolidityLanguage();
                defineThemes();
                monacoLoaded = true;
                resolve();
            });
        };
        script.onerror = () => reject(new Error('Failed to load Monaco Editor'));
        document.head.appendChild(script);
    });

    return monacoLoadPromise;
}

async function registerSolidityLanguage() {
    const module = await import('./solidity-monarch.js');
    monaco.languages.register({ id: 'solidity' });
    monaco.languages.setMonarchTokensProvider('solidity', module.solidityLanguage);
}

function defineThemes() {
    monaco.editor.defineTheme('nethereum-dark', {
        base: 'vs-dark',
        inherit: true,
        rules: [
            { token: 'keyword', foreground: 'c792ea' },
            { token: 'keyword.modifier', foreground: 'c792ea', fontStyle: 'italic' },
            { token: 'type', foreground: 'ffcb6b' },
            { token: 'predefined', foreground: '82aaff' },
            { token: 'variable.predefined', foreground: 'f78c6c' },
            { token: 'constant', foreground: 'f78c6c' },
            { token: 'string', foreground: 'c3e88d' },
            { token: 'string.escape', foreground: '89ddff' },
            { token: 'number', foreground: 'f78c6c' },
            { token: 'number.hex', foreground: 'f78c6c' },
            { token: 'number.float', foreground: 'f78c6c' },
            { token: 'comment', foreground: '546e7a', fontStyle: 'italic' },
            { token: 'comment.doc', foreground: '7a8a99', fontStyle: 'italic' },
            { token: 'comment.doc.tag', foreground: '64ffda', fontStyle: 'italic bold' },
            { token: 'operator', foreground: '89ddff' },
            { token: 'delimiter', foreground: '89ddff' },
            { token: 'identifier', foreground: 'eeffff' },
            { token: '', foreground: 'eeffff' }
        ],
        colors: {
            'editor.background': '#0a192f',
            'editor.foreground': '#eeffff',
            'editor.lineHighlightBackground': '#0d2137',
            'editor.selectionBackground': '#1b3a5c',
            'editorCursor.foreground': '#64ffda',
            'editorLineNumber.foreground': '#4a6a8a',
            'editorLineNumber.activeForeground': '#64ffda',
            'editorGutter.background': '#091527',
            'editor.inactiveSelectionBackground': '#142840'
        }
    });

    monaco.editor.defineTheme('nethereum-light', {
        base: 'vs',
        inherit: true,
        rules: [
            { token: 'keyword', foreground: '7c3aed' },
            { token: 'keyword.modifier', foreground: '7c3aed', fontStyle: 'italic' },
            { token: 'type', foreground: 'b45309' },
            { token: 'predefined', foreground: '2563eb' },
            { token: 'variable.predefined', foreground: 'dc2626' },
            { token: 'constant', foreground: 'dc2626' },
            { token: 'string', foreground: '16a34a' },
            { token: 'number', foreground: 'ea580c' },
            { token: 'comment', foreground: '9ca3af', fontStyle: 'italic' },
            { token: 'comment.doc', foreground: '6b7280', fontStyle: 'italic' },
            { token: 'comment.doc.tag', foreground: '0d9488', fontStyle: 'italic bold' },
            { token: 'operator', foreground: '475569' }
        ],
        colors: {
            'editor.background': '#ffffff',
            'editor.foreground': '#1e293b',
            'editor.lineHighlightBackground': '#f1f5f9',
            'editorLineNumber.foreground': '#94a3b8'
        }
    });
}

export async function initMonaco(elementId, content, language, theme) {
    await loadMonaco();

    const container = document.getElementById(elementId);
    if (!container) return;

    if (editors[elementId]) {
        editors[elementId].dispose();
    }

    const editor = monaco.editor.create(container, {
        value: content || '',
        language: language || 'solidity',
        theme: theme || 'nethereum-dark',
        readOnly: true,
        minimap: { enabled: false },
        glyphMargin: true,
        fontSize: 13,
        fontFamily: "'Cascadia Code', 'Fira Code', Consolas, monospace",
        lineNumbers: 'on',
        scrollBeyondLastLine: false,
        automaticLayout: true,
        wordWrap: 'off',
        folding: true,
        renderLineHighlight: 'line',
        scrollbar: {
            verticalScrollbarSize: 8,
            horizontalScrollbarSize: 8
        }
    });

    editors[elementId] = editor;
    decorations[elementId] = [];
    models[elementId] = {};
}

// Register a file as a named model so switching is instant and preserves view state
export function registerFile(elementId, filePath, content) {
    if (!monacoLoaded) return;
    const uri = monaco.Uri.parse('file:///' + filePath.replace(/\\/g, '/'));
    let model = monaco.editor.getModel(uri);
    if (!model) {
        model = monaco.editor.createModel(content || '', 'solidity', uri);
    }
    if (!models[elementId]) models[elementId] = {};
    models[elementId][filePath] = model;
}

// Switch the editor to show a previously registered file model (instant, preserves per-file view state)
export function switchToFile(elementId, filePath) {
    const editor = editors[elementId];
    if (!editor) return;
    const fileModels = models[elementId];
    if (!fileModels || !fileModels[filePath]) return;

    // Save current view state before switching
    const currentModel = editor.getModel();
    if (currentModel && currentModel._viewState === undefined) {
        // Store view state keyed by model URI
        const key = currentModel.uri.toString();
        if (!editor._viewStates) editor._viewStates = {};
        editor._viewStates[key] = editor.saveViewState();
    }

    const targetModel = fileModels[filePath];
    editor.setModel(targetModel);

    // Restore view state for this file if we had one
    if (editor._viewStates) {
        const savedState = editor._viewStates[targetModel.uri.toString()];
        if (savedState) {
            editor.restoreViewState(savedState);
        }
    }

    // Clear decorations when switching files
    decorations[elementId] = editor.deltaDecorations(
        decorations[elementId] || [],
        []
    );
}

export function setContent(elementId, content) {
    const editor = editors[elementId];
    if (editor) {
        editor.setValue(content || '');
    }
}

export function highlightLine(elementId, lineNumber) {
    const editor = editors[elementId];
    if (!editor || lineNumber < 1) return;

    const newDecorations = [
        {
            range: new monaco.Range(lineNumber, 1, lineNumber, 1),
            options: {
                isWholeLine: true,
                className: 'solidity-debug-current-line',
                glyphMarginClassName: 'solidity-debug-glyph-arrow'
            }
        }
    ];

    decorations[elementId] = editor.deltaDecorations(
        decorations[elementId] || [],
        newDecorations
    );
}

export function clearHighlights(elementId) {
    const editor = editors[elementId];
    if (editor) {
        decorations[elementId] = editor.deltaDecorations(
            decorations[elementId] || [],
            []
        );
    }
}

export function revealLine(elementId, lineNumber) {
    const editor = editors[elementId];
    if (editor && lineNumber > 0) {
        editor.revealLineInCenter(lineNumber);
    }
}

export function setTheme(theme) {
    if (monacoLoaded) {
        monaco.editor.setTheme(theme);
    }
}

let themeObserver = null;
export function observeTheme() {
    if (themeObserver) return;
    themeObserver = new MutationObserver(() => {
        if (!monacoLoaded) return;
        const t = document.documentElement.getAttribute('data-theme') || 'dark';
        monaco.editor.setTheme(t === 'light' ? 'nethereum-light' : 'nethereum-dark');
    });
    themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ['data-theme'] });
}

export function scrollOpcodeList(listId, stepIndex) {
    const container = document.getElementById(listId);
    if (!container) return;
    const row = container.querySelector(`[data-step="${stepIndex}"]`);
    if (row) {
        const containerHeight = container.clientHeight;
        const rowTop = row.offsetTop - container.offsetTop;
        container.scrollTop = rowTop - (containerHeight / 2) + 12;
    }
}

export function dispose(elementId) {
    const editor = editors[elementId];
    if (editor) {
        editor.dispose();
        delete editors[elementId];
        delete decorations[elementId];
    }
    // Dispose all models for this editor
    const fileModels = models[elementId];
    if (fileModels) {
        for (const key of Object.keys(fileModels)) {
            fileModels[key].dispose();
        }
        delete models[elementId];
    }
}
