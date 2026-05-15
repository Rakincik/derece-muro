using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MURO.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantQuotas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "MaxBbbParticipants",
                table: "Tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxCourses",
                table: "Tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxSessionsPerDay",
                table: "Tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxStudents",
                table: "Tenants",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "StorageLimitGb",
                table: "Tenants",
                type: "numeric",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxBbbParticipants",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MaxCourses",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MaxSessionsPerDay",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "MaxStudents",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "StorageLimitGb",
                table: "Tenants");
        }
    }
}
