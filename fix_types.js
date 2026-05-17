const fs = require('fs');
const file = 'c:/Users/Rüstem/.gemini/antigravity/scratch/muro-v2/frontend/admin/src/lib/api/types.ts';
let code = fs.readFileSync(file, 'utf8');

code = code.replace(
    /        totalEnrolled: number;\r?\n        attendanceRate: number;\r?\n    }\[\];\r?\n\}/,
    `        totalEnrolled: number;
        attendanceRate: number;
    }[];
    enrolledStudents?: {
        userId: string;
        fullName: string;
    }[];
}`
);

fs.writeFileSync(file, code, 'utf8');
