using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MURO.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseInstructorsManyToMany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CourseInstructors",
                columns: table => new
                {
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseInstructors", x => new { x.CourseId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CourseInstructors_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseInstructors_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseInstructors_UserId",
                table: "CourseInstructors",
                column: "UserId");

            migrationBuilder.Sql("INSERT INTO \"CourseInstructors\" (\"CourseId\", \"UserId\") SELECT \"Id\", \"InstructorId\" FROM \"Courses\" WHERE \"InstructorId\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CourseInstructors");
        }
    }
}
