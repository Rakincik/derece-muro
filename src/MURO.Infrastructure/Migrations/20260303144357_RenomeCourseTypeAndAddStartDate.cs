using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MURO.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenomeCourseTypeAndAddStartDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StartDate",
                table: "Courses",
                type: "timestamp with time zone",
                nullable: true);

            // Rename existing CourseType string values to match new enum names
            migrationBuilder.Sql("UPDATE \"Courses\" SET \"CourseType\" = 'Online' WHERE \"CourseType\" = 'OnDemand'");
            migrationBuilder.Sql("UPDATE \"Courses\" SET \"CourseType\" = 'Offline' WHERE \"CourseType\" = 'Live'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StartDate",
                table: "Courses");
        }
    }
}
