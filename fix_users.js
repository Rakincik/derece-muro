const fs = require('fs');
const file = 'frontend/admin/src/lib/api/users.ts';
let content = fs.readFileSync(file, 'utf8');

const target = `    exportExcel: async (token: string, tenantId: string, role?: string) => {
        const params = role ? \`?role=\${role}\` : '';
        const res = await fetch(\`\${API_URL}/users/export-excel\${params}\`, {`;

const replacement = `    exportExcel: async (token: string, tenantId: string, userIds: string[]) => {
        const res = await fetch(\`\${API_URL}/users/export-excel\`, {
            method: 'POST',
            headers: { Authorization: \`Bearer \${token}\`, 'X-Tenant-Id': tenantId, 'Content-Type': 'application/json' },
            body: JSON.stringify({ userIds })`;

// Normalizing line endings for the replace
const normalizedTarget = target.replace(/\r\n/g, '\n');
content = content.replace(/\r\n/g, '\n');

if (content.includes(normalizedTarget)) {
    content = content.replace(normalizedTarget, replacement);
    fs.writeFileSync(file, content);
    console.log("Success");
} else {
    console.log("Target not found!");
}
