// Turns src/<name>.ts into dist/<name>.js by blanking the types out (Node's own type stripping), so every line and
// column of the JavaScript is the TypeScript's and a script error from the game server points into the .ts file.
// "npm run build" type checks first (tsc); the server project embeds dist/*.js (its BuildGameScripts target).
import { mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { stripTypeScriptTypes } from 'node:module';
import { basename, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = fileURLToPath(new URL('.', import.meta.url));
const src = join(root, 'src');
const dist = join(root, 'dist');

// StartScript's limit for the source (openbf2-server-ext docs/admin-channel-draft.md)
const maxBytes = 60000;

// What the module's engine does not have. A script that names one of these would only fail on the game server.
const missing = ['Date', 'RegExp', 'eval', 'Function', 'Proxy', 'WeakRef', 'FinalizationRegistry', 'BigInt', 'Atomics',
    'console', 'setTimeout', 'setInterval', 'queueMicrotask', 'require', 'import', 'export'];

/** The code with comments, strings and template literals blanked, for the checks below. */
function codeOnly(code) {
    let out = '';
    for (let i = 0; i < code.length;) {
        const two = code.slice(i, i + 2);
        if (two === '//') {
            while (i < code.length && code[i] !== '\n') i++;
        } else if (two === '/*') {
            const end = code.indexOf('*/', i + 2);
            i = end < 0 ? code.length : end + 2;
        } else if (code[i] === '"' || code[i] === "'" || code[i] === '`') {
            const quote = code[i++];
            while (i < code.length && code[i] !== quote) i += code[i] === '\\' ? 2 : 1;
            i++;
            out += '""';
        } else {
            out += code[i++];
        }
    }
    return out;
}

function fail(file, message) {
    console.error(`${file}: ${message}`);
    process.exitCode = 1;
}

rmSync(dist, { recursive: true, force: true });
mkdirSync(dist, { recursive: true });

const files = readdirSync(src).filter(f => f.endsWith('.ts') && !f.endsWith('.d.ts')).sort();
for (const file of files) {
    const name = basename(file, '.ts');
    if (!/^[a-z][a-z0-9-]{0,63}$/.test(name)) {
        fail(file, 'a script name is lower-case letters, digits and "-", at most 64 characters');
        continue;
    }

    const typescript = readFileSync(join(src, file), 'utf8');
    const javascript = stripTypeScriptTypes(typescript, { mode: 'strip' });
    if (javascript.length !== typescript.length)
        fail(file, 'removing the types changed the length; lines and columns would be off');

    const words = new Set(codeOnly(javascript).match(/[A-Za-z_$][\w$]*/g) ?? []);
    for (const word of missing.filter(w => words.has(w)))
        fail(file, `uses "${word}", which the game server's script engine does not have (docs/scripts.md)`);

    const bytes = Buffer.byteLength(javascript, 'utf8');
    if (bytes > maxBytes)
        fail(file, `is ${bytes} bytes; a script is at most ${maxBytes}`);

    writeFileSync(join(dist, `${name}.js`), javascript);
    console.log(`${name}.js ${bytes} bytes`);
}

if (files.length === 0)
    fail('src', 'no scripts');
