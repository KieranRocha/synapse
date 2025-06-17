using System;
using System.Collections.Generic;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CADCompanion.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BomVersions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProjectId = table.Column<string>(type: "text", nullable: false),
                    MachineId = table.Column<string>(type: "text", nullable: false),
                    AssemblyFilePath = table.Column<string>(type: "text", nullable: false),
                    ExtractedBy = table.Column<string>(type: "text", nullable: false),
                    ExtractedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VersionNumber = table.Column<int>(type: "integer", nullable: false),
                    Items = table.Column<List<BomItemDto>>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomVersions", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BomVersions");
        }
    }
}
