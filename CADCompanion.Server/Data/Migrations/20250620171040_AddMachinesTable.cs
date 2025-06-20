using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CADCompanion.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMachinesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ProjectId",
                table: "BomVersions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "MachineId",
                table: "BomVersions",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedBy",
                table: "BomVersions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AssemblyFilePath",
                table: "BomVersions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "MachineId1",
                table: "BomVersions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Machines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OperationNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FolderPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MainAssemblyPath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ProjectId = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastBomExtraction = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TotalBomVersions = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Machines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Machines_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BomVersions_ExtractedAt",
                table: "BomVersions",
                column: "ExtractedAt");

            migrationBuilder.CreateIndex(
                name: "IX_BomVersions_MachineId",
                table: "BomVersions",
                column: "MachineId");

            migrationBuilder.CreateIndex(
                name: "IX_BomVersions_MachineId1",
                table: "BomVersions",
                column: "MachineId1");

            migrationBuilder.CreateIndex(
                name: "IX_BomVersions_ProjectId",
                table: "BomVersions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_BomVersions_ProjectId_MachineId_VersionNumber",
                table: "BomVersions",
                columns: new[] { "ProjectId", "MachineId", "VersionNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Machines_CreatedAt",
                table: "Machines",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_OperationNumber",
                table: "Machines",
                column: "OperationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Machines_ProjectId",
                table: "Machines",
                column: "ProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_BomVersions_Machines_MachineId1",
                table: "BomVersions",
                column: "MachineId1",
                principalTable: "Machines",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BomVersions_Machines_MachineId1",
                table: "BomVersions");

            migrationBuilder.DropTable(
                name: "Machines");

            migrationBuilder.DropIndex(
                name: "IX_BomVersions_ExtractedAt",
                table: "BomVersions");

            migrationBuilder.DropIndex(
                name: "IX_BomVersions_MachineId",
                table: "BomVersions");

            migrationBuilder.DropIndex(
                name: "IX_BomVersions_MachineId1",
                table: "BomVersions");

            migrationBuilder.DropIndex(
                name: "IX_BomVersions_ProjectId",
                table: "BomVersions");

            migrationBuilder.DropIndex(
                name: "IX_BomVersions_ProjectId_MachineId_VersionNumber",
                table: "BomVersions");

            migrationBuilder.DropColumn(
                name: "MachineId1",
                table: "BomVersions");

            migrationBuilder.AlterColumn<string>(
                name: "ProjectId",
                table: "BomVersions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "MachineId",
                table: "BomVersions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "ExtractedBy",
                table: "BomVersions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "AssemblyFilePath",
                table: "BomVersions",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);
        }
    }
}
