const fs = require('fs');
const path = require('path');
const english = JSON.parse(fs.readFileSync(path.join(__dirname, 'locales/en.json'), 'utf8')).strings.ui;
const entries = Object.entries(english);
if (entries.some(([key, value]) => !/^[A-Za-z][A-Za-z0-9_]*$/.test(key) || typeof value !== 'string')) {
  throw Error('Invalid English language defaults');
}
// Plain managed constants: fallback text must work before Unity or disk IO is available.
const code = `// Generated from locales/en.json. Do not edit.\nusing System.Collections.Generic;\nnamespace SCDEKeyboardControl\n{\n    internal static class LanguageDefaults\n    {\n        internal static Dictionary<string, string> English()\n        {\n            return new Dictionary<string, string>\n            {\n${entries.map(([key, value]) => `                { ${JSON.stringify(key)}, ${JSON.stringify(value)} }`).join(',\n')}\n            };\n        }\n    }\n}\n`;
fs.writeFileSync(process.argv[2], code);
