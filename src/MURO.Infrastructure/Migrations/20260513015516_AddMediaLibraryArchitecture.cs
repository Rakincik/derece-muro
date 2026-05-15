using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MURO.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMediaLibraryArchitecture : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FolderId",
                table: "MediaAssets",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CourseMedias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    MediaAssetId = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderIndex = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseMedias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CourseMedias_Courses_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Courses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CourseMedias_MediaAssets_MediaAssetId",
                        column: x => x.MediaAssetId,
                        principalTable: "MediaAssets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaFolders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ParentFolderId = table.Column<Guid>(type: "uuid", nullable: true),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFolders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFolders_MediaFolders_ParentFolderId",
                        column: x => x.ParentFolderId,
                        principalTable: "MediaFolders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MediaFolders_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaAssets_FolderId",
                table: "MediaAssets",
                column: "FolderId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseMedias_CourseId_MediaAssetId",
                table: "CourseMedias",
                columns: new[] { "CourseId", "MediaAssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseMedias_MediaAssetId",
                table: "CourseMedias",
                column: "MediaAssetId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseMedias_OrderIndex",
                table: "CourseMedias",
                column: "OrderIndex");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFolders_ParentFolderId",
                table: "MediaFolders",
                column: "ParentFolderId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFolders_TenantId",
                table: "MediaFolders",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_MediaAssets_MediaFolders_FolderId",
                table: "MediaAssets",
                column: "FolderId",
                principalTable: "MediaFolders",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MediaAssets_MediaFolders_FolderId",
                table: "MediaAssets");

            migrationBuilder.DropTable(
                name: "CourseMedias");

            migrationBuilder.DropTable(
                name: "MediaFolders");

            migrationBuilder.DropIndex(
                name: "IX_MediaAssets_FolderId",
                table: "MediaAssets");

            migrationBuilder.DropColumn(
                name: "FolderId",
                table: "MediaAssets");
        }
    }
}
