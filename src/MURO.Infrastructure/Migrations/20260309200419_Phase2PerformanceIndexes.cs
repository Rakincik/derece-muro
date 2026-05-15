using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MURO.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase2PerformanceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoNotes_UserId",
                table: "VideoNotes");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TenantId",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_TenantMemberships_TenantId",
                table: "TenantMemberships");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_TenantId",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_CourseId",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_TenantId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_TenantId",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_GroupMembers_GroupId",
                table: "GroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_Exams_TenantId",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_ExamResults_ExamId",
                table: "ExamResults");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_TenantId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentSubmissions_AssignmentId",
                table: "AssignmentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_TenantId",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_GroupId",
                table: "Announcements");

            migrationBuilder.CreateIndex(
                name: "IX_VideoNotes_UserId_MediaAssetId",
                table: "VideoNotes",
                columns: new[] { "UserId", "MediaAssetId" });

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId_TransactionDate",
                table: "Transactions",
                columns: new[] { "TenantId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId_Status",
                table: "TenantMemberships",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId_Status",
                table: "SupportTickets",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CourseId_ScheduledStart",
                table: "Sessions",
                columns: new[] { "CourseId", "ScheduledStart" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionAttendances_TenantId_UserId",
                table: "SessionAttendances",
                columns: new[] { "TenantId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_SecurityEvents_IpAddress_EventType_CreatedAt",
                table: "SecurityEvents",
                columns: new[] { "IpAddress", "EventType", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TenantId_InstructorId",
                table: "Questions",
                columns: new[] { "TenantId", "InstructorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TenantId_Status",
                table: "Questions",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId_CreatedAt",
                table: "Notifications",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_TenantId_IsRead",
                table: "Notifications",
                columns: new[] { "UserId", "TenantId", "IsRead" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_TenantId_CourseId",
                table: "MediaAssets",
                columns: new[] { "TenantId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_TenantId_Status",
                table: "MediaAssets",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_GroupId_Status",
                table: "GroupMembers",
                columns: new[] { "GroupId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_UserId",
                table: "GroupMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_TenantId_CreatedAt",
                table: "Exams",
                columns: new[] { "TenantId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Exams_TenantId_Status",
                table: "Exams",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_ExamId_UserId",
                table: "ExamResults",
                columns: new[] { "ExamId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_TenantId_GroupId",
                table: "CalendarEvents",
                columns: new[] { "TenantId", "GroupId" });

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_TenantId_StartDate_EndDate",
                table: "CalendarEvents",
                columns: new[] { "TenantId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_TenantId_Action",
                table: "AuditLogs",
                columns: new[] { "TenantId", "Action" });

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_AssignmentId_UserId",
                table: "AssignmentSubmissions",
                columns: new[] { "AssignmentId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_CourseId",
                table: "Assignments",
                columns: new[] { "TenantId", "CourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId_DueDate",
                table: "Assignments",
                columns: new[] { "TenantId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_GroupId_CreatedAt",
                table: "Announcements",
                columns: new[] { "GroupId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_VideoNotes_UserId_MediaAssetId",
                table: "VideoNotes");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_TenantId_TransactionDate",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_TenantMemberships_TenantId_Status",
                table: "TenantMemberships");

            migrationBuilder.DropIndex(
                name: "IX_SupportTickets_TenantId_Status",
                table: "SupportTickets");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_CourseId_ScheduledStart",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_SessionAttendances_TenantId_UserId",
                table: "SessionAttendances");

            migrationBuilder.DropIndex(
                name: "IX_SecurityEvents_IpAddress_EventType_CreatedAt",
                table: "SecurityEvents");

            migrationBuilder.DropIndex(
                name: "IX_Questions_TenantId_InstructorId",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Questions_TenantId_Status",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_TenantId_CreatedAt",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_TenantId_IsRead",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_TenantId_CourseId",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_TenantId_Status",
                table: "MediaAssets");

            migrationBuilder.DropIndex(
                name: "IX_GroupMembers_GroupId_Status",
                table: "GroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_GroupMembers_UserId",
                table: "GroupMembers");

            migrationBuilder.DropIndex(
                name: "IX_Exams_TenantId_CreatedAt",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_TenantId_Status",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_ExamResults_ExamId_UserId",
                table: "ExamResults");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_TenantId_GroupId",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_CalendarEvents_TenantId_StartDate_EndDate",
                table: "CalendarEvents");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_TenantId_Action",
                table: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_AssignmentSubmissions_AssignmentId_UserId",
                table: "AssignmentSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_TenantId_CourseId",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Assignments_TenantId_DueDate",
                table: "Assignments");

            migrationBuilder.DropIndex(
                name: "IX_Announcements_GroupId_CreatedAt",
                table: "Announcements");

            migrationBuilder.CreateIndex(
                name: "IX_VideoNotes_UserId",
                table: "VideoNotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_TenantId",
                table: "Transactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantMemberships_TenantId",
                table: "TenantMemberships",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TenantId",
                table: "SupportTickets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_CourseId",
                table: "Sessions",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_Questions_TenantId",
                table: "Questions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_TenantId",
                table: "MediaAssets",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GroupMembers_GroupId",
                table: "GroupMembers",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Exams_TenantId",
                table: "Exams",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ExamResults_ExamId",
                table: "ExamResults",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_CalendarEvents_TenantId",
                table: "CalendarEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AssignmentSubmissions_AssignmentId",
                table: "AssignmentSubmissions",
                column: "AssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Assignments_TenantId",
                table: "Assignments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Announcements_GroupId",
                table: "Announcements",
                column: "GroupId");
        }
    }
}
