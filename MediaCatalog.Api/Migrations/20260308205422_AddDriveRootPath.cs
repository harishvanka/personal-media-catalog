using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MediaCatalog.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddDriveRootPath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Drives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    RootPath = table.Column<string>(type: "TEXT", nullable: false),
                    Serial = table.Column<string>(type: "TEXT", nullable: true),
                    LastScannedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drives", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MediaFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    DriveId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", nullable: false),
                    SizeBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    Extension = table.Column<string>(type: "TEXT", nullable: false),
                    ContentHash = table.Column<string>(type: "TEXT", nullable: false),
                    Category = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtFs = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedAtFs = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFiles_Drives_DriveId",
                        column: x => x.DriveId,
                        principalTable: "Drives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Drives_Label",
                table: "Drives",
                column: "Label",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_Category",
                table: "MediaFiles",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_ContentHash",
                table: "MediaFiles",
                column: "ContentHash");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_DriveId",
                table: "MediaFiles",
                column: "DriveId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFiles_RelativePath",
                table: "MediaFiles",
                column: "RelativePath");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaFiles");

            migrationBuilder.DropTable(
                name: "Drives");
        }
    }
}
