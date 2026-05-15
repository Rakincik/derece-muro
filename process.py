# -*- coding: utf-8 -*-
import os

def process_file(filename, class_name, interface_name, keep_ranges):
    with open(filename, 'r', encoding='utf-8') as f:
        lines = f.readlines()
    
    # Replace class declaration
    for i, line in enumerate(lines):
        if 'public class AdminService : IAdminService' in line:
            lines[i] = line.replace('AdminService : IAdminService', f'{class_name} : {interface_name}')
            break
            
    for i, line in enumerate(lines):
        if 'public AdminService(' in line:
            lines[i] = line.replace('AdminService(', f'{class_name}(')
            lines[i] = line.replace('ILogger<AdminService>', f'ILogger<{class_name}>')
            break
            
    new_lines = []
    # Always keep header (1 to 30)
    for i in range(1, 31):
        # Also replace ILogger<AdminService> in the private readonly field
        if 'ILogger<AdminService>' in lines[i-1]:
            new_lines.append(lines[i-1].replace('ILogger<AdminService>', f'ILogger<{class_name}>'))
        else:
            new_lines.append(lines[i-1])
        
    for start, end in keep_ranges:
        for i in range(start, end + 1):
            if i - 1 < len(lines):
                new_lines.append(lines[i-1])
                
    new_lines.append("}\n")
    
    with open(filename, 'w', encoding='utf-8') as f:
        f.writelines(new_lines)

process_file(r'src\MURO.Infrastructure\Services\AdminTenantService.cs', 'AdminTenantService', 'IAdminTenantService', [(31, 420), (1012, 1094)])
process_file(r'src\MURO.Infrastructure\Services\AdminSessionService.cs', 'AdminSessionService', 'IAdminSessionService', [(421, 598), (764, 833)])
process_file(r'src\MURO.Infrastructure\Services\AdminAnalyticsService.cs', 'AdminAnalyticsService', 'IAdminAnalyticsService', [(599, 692)])
process_file(r'src\MURO.Infrastructure\Services\AdminUserService.cs', 'AdminUserService', 'IAdminUserService', [(693, 763), (1417, 1490)])
process_file(r'src\MURO.Infrastructure\Services\AdminSystemService.cs', 'AdminSystemService', 'IAdminSystemService', [(834, 891), (1095, 1416), (1593, 2025)])
process_file(r'src\MURO.Infrastructure\Services\AdminJobsService.cs', 'AdminJobsService', 'IAdminJobsService', [(892, 1011)])
process_file(r'src\MURO.Infrastructure\Services\AdminSecurityService.cs', 'AdminSecurityService', 'IAdminSecurityService', [(1491, 1592)])
print('Files processed successfully')
