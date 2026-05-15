const fs = require('fs');
const file = 'c:/Users/Rüstem/.gemini/antigravity/scratch/muro-v2/frontend/admin/src/app/dashboard/groups/page.tsx';
let content = fs.readFileSync(file, 'utf8');

// The file was read as ANSI and written as UTF8. We can reverse this by reading the file as UTF8,
// converting it to a Buffer (latin1 string), and then decoding as UTF8.
let buffer = Buffer.from(content, 'latin1');
let fixed = buffer.toString('utf8');

// There might be a second layer of corruption based on the output 'ÃƒÅ“' which is Ãœ encoded again!
// Let's check if it needs double decoding.
try {
   let doubleBuffer = Buffer.from(fixed, 'latin1');
   let doubleFixed = doubleBuffer.toString('utf8');
   if (doubleFixed.includes('Ü') || doubleFixed.includes('ü')) fixed = doubleFixed;
} catch(e) {}

fs.writeFileSync(file, fixed, 'utf8');
