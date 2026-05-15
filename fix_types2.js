const fs = require('fs');
const path = require('path');

const typesFile = path.join(__dirname, 'frontend/admin/src/lib/api/types.ts');
let content = fs.readFileSync(typesFile, 'utf8');

// Fix UserDto
content = content.replace(/export interface UserDto {[\s\S]*?\n}/, `export interface UserDto {
    id: string;
    firstName: string;
    lastName: string;
    email: string;
    phone: string | null;
    role: string;
    studentType: string | null;
    demoExpiresAt?: string | null;
    isActive: boolean;
    createdAt: string;
    tenants?: UserTenantDto[];
    lastLoginAt?: string | null;
    groupNames?: string[];
}`);

// Fix AssignmentListDto
content = content.replace(/export interface AssignmentListDto {[\s\S]*?\n}/, `export interface AssignmentListDto {
    id: string;
    title: string;
    description?: string | null;
    courseId: string;
    courseName?: string;
    dueDate: string;
    maxScore: number;
    fileUrl?: string | null;
    submissionCount: number;
    gradedCount: number;
    averageScore: number | null;
    createdAt?: string;
}`);

// Fix AssignmentDetailDto
content = content.replace(/export interface AssignmentDetailDto extends AssignmentListDto {[\s\S]*?\n}/, `export interface AssignmentDetailDto extends AssignmentListDto {
    submissions: SubmissionDto[];
}`);

fs.writeFileSync(typesFile, content);
console.log('Fixed types.ts manually!');
