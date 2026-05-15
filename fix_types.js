const fs = require('fs');
const path = require('path');

const dir = path.join(__dirname, 'frontend/admin/src/lib/api');

// Clean types.ts
let typesContent = fs.readFileSync(path.join(dir, 'types.ts'), 'utf8');

const interfaceMap = new Map();
const typeRegex = /export interface ([A-Za-z0-9_]+)(?: extends [A-Za-z0-9_]+)? {[\s\S]*?\n}/gm;

let match;
let newTypesContent = '';

while ((match = typeRegex.exec(typesContent)) !== null) {
    const name = match[1];
    if (!interfaceMap.has(name)) {
        interfaceMap.set(name, match[0]);
        newTypesContent += match[0] + '\n\n';
    } else {
        // Log duplicate
        console.log(`Removed duplicate interface: ${name}`);
    }
}

fs.writeFileSync(path.join(dir, 'types.ts'), newTypesContent);

// Fix imports in all files
const files = fs.readdirSync(dir);
files.forEach(f => {
    if (f === 'core.ts' || f === 'types.ts' || f === 'index.ts' || !f.endsWith('.ts')) return;

    let content = fs.readFileSync(path.join(dir, f), 'utf8');
    
    // Replace duplicate imports like `import { UserDto, UserDto } from './types'`
    const importRegex = /import \{([^}]+)\} from '\.\/types';/;
    content = content.replace(importRegex, (match, group1) => {
        const parts = group1.split(',').map(s => s.trim()).filter(s => s);
        const unique = [...new Set(parts)];
        return `import { ${unique.join(', ')} } from './types';`;
    });

    fs.writeFileSync(path.join(dir, f), content);
});

console.log('Fixed types.ts and import statements!');
