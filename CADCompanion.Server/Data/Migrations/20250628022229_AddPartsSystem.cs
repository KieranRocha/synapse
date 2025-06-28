using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CADCompanion.Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartsSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.CreateTable(
                name: "PartNumberSequences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastNumber = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SequenceType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartNumberSequences", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Parts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PartNumber = table.Column<string>(type: "character(6)", fixedLength: true, maxLength: 6, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Material = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Weight = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: true),
                    Cost = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Supplier = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Manufacturer = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ManufacturerPartNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    DatasheetUrl = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    CustomProperties = table.Column<Dictionary<string, object>>(type: "jsonb", nullable: true),
                    IsStandardPart = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parts", x => x.Id);
                    table.UniqueConstraint("AK_Parts_PartNumber", x => x.PartNumber);
                });

            migrationBuilder.CreateTable(
                name: "BomPartUsages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BomVersionId = table.Column<int>(type: "integer", nullable: false),
                    PartNumber = table.Column<string>(type: "character(6)", maxLength: 6, nullable: false),
                    Quantity = table.Column<decimal>(type: "numeric(10,3)", precision: 10, scale: 3, nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    ParentPartNumber = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: true),
                    ReferenceDesignator = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsOptional = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BomPartUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BomPartUsages_BomVersions_BomVersionId",
                        column: x => x.BomVersionId,
                        principalTable: "BomVersions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BomPartUsages_Parts_PartNumber",
                        column: x => x.PartNumber,
                        principalTable: "Parts",
                        principalColumn: "PartNumber",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BomPartUsages_BomVersionId",
                table: "BomPartUsages",
                column: "BomVersionId");

            migrationBuilder.CreateIndex(
                name: "IX_BomPartUsages_BomVersionId_PartNumber",
                table: "BomPartUsages",
                columns: new[] { "BomVersionId", "PartNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BomPartUsages_ParentPartNumber",
                table: "BomPartUsages",
                column: "ParentPartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_BomPartUsages_PartNumber",
                table: "BomPartUsages",
                column: "PartNumber");

            migrationBuilder.CreateIndex(
                name: "IX_PartNumberSequences_SequenceType",
                table: "PartNumberSequences",
                column: "SequenceType",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parts_Category",
                table: "Parts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_CreatedAt",
                table: "Parts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_Description",
                table: "Parts",
                column: "Description");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_IsStandardPart",
                table: "Parts",
                column: "IsStandardPart");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_PartNumber",
                table: "Parts",
                column: "PartNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Parts_Status",
                table: "Parts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Parts_Supplier",
                table: "Parts",
                column: "Supplier");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BomPartUsages");

            migrationBuilder.DropTable(
                name: "PartNumberSequences");

            migrationBuilder.DropTable(
                name: "Parts");

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
        }
    }
}
