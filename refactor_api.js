const fs = require('fs');
const path = require('path');

const inputFile = path.join(__dirname, 'frontend/admin/src/lib/api.ts');
const outputDir = path.join(__dirname, 'frontend/admin/src/lib/api');

let content = fs.readFileSync(inputFile, 'utf-8');
let remainingContent = content.substring(content.indexOf('// Auth endpoints'));
remainingContent = remainingContent.replace(/export interface PagedResult<T> {[\s\S]*?}/m, '');

// Extract interfaces to types.ts
const typeRegex = /export interface [A-Za-z0-9_]+ (?:extends [A-Za-z0-9_]+ )?{[\s\S]*?\n}/gm;
let typesContent = '';
let match;
while ((match = typeRegex.exec(remainingContent)) !== null) {
    typesContent += match[0] + '\n\n';
}
fs.writeFileSync(path.join(outputDir, 'types.ts'), typesContent);

// Get list of extracted interfaces
const interfaceNames = [];
const nameRegex = /export interface ([A-Za-z0-9_]+)/g;
let m;
while ((m = nameRegex.exec(typesContent)) !== null) {
    interfaceNames.push(m[1]);
}
const importTypesStr = interfaceNames.length > 0 ? `import { ${interfaceNames.join(', ')} } from './types';\n` : '';
const baseImports = `import { api, cachedApi, invalidateCache, invalidateCacheByPrefix, API_URL, PagedResult } from './core';\n${importTypesStr}\n`;

// Split by APIs
let strippedContent = remainingContent.replace(typeRegex, '');
const blocks = strippedContent.split('export const ');

for (let i = 1; i < blocks.length; i++) {
    const block = blocks[i];
    const apiNameMatch = block.match(/^([A-Za-z0-9_]+)/);
    if (!apiNameMatch) continue;
    
    const apiName = apiNameMatch[1];
    let fileName = apiName.replace('Api', '') + 's.ts';
    if (apiName === 'authApi') fileName = 'auth.ts';
    if (apiName === 'uploadApi') fileName = 'upload.ts';
    if (apiName === 'tenantApi') fileName = 'tenant.ts';
    
    fs.writeFileSync(path.join(outputDir, fileName), baseImports + 'export const ' + block);
}

// Generate index.ts
let indexContent = "export * from './core';\nexport * from './types';\n";
const files = fs.readdirSync(outputDir);
files.forEach(f => {
    if (f !== 'core.ts' && f !== 'types.ts' && f !== 'index.ts' && f.endsWith('.ts')) {
        indexContent += `export * from './${f.replace('.ts', '')}';\n`;
    }
});
fs.writeFileSync(path.join(outputDir, 'index.ts'), indexContent);

// Replace main api.ts
fs.writeFileSync(inputFile, "export * from './api/index';\n");

console.log('Successfully split api.ts!');
