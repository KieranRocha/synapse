﻿// <auto-generated />
using System;
using System.Collections.Generic;
using CADCompanion.Server.Data;
using CADCompanion.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CADCompanion.Server.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20250620171040_AddMachinesTable")]
    partial class AddMachinesTable
    {
        /// <inheritdoc />
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.6")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("CADCompanion.Server.Models.BomVersion", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<string>("AssemblyFilePath")
                        .IsRequired()
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<DateTime>("ExtractedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ExtractedBy")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<List<BomItemDto>>("Items")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<string>("MachineId")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<int?>("MachineId1")
                        .HasColumnType("integer");

                    b.Property<string>("ProjectId")
                        .IsRequired()
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<int?>("ProjectId1")
                        .HasColumnType("integer");

                    b.Property<int>("VersionNumber")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("ExtractedAt");

                    b.HasIndex("MachineId");

                    b.HasIndex("MachineId1");

                    b.HasIndex("ProjectId");

                    b.HasIndex("ProjectId1");

                    b.HasIndex("ProjectId", "MachineId", "VersionNumber")
                        .IsUnique();

                    b.ToTable("BomVersions");
                });

            modelBuilder.Entity("CADCompanion.Server.Models.Machine", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("FolderPath")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<DateTime?>("LastBomExtraction")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("MainAssemblyPath")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("OperationNumber")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<int>("ProjectId")
                        .HasColumnType("integer");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("TotalBomVersions")
                        .HasColumnType("integer");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("OperationNumber");

                    b.HasIndex("ProjectId");

                    b.ToTable("Machines");
                });

            modelBuilder.Entity("CADCompanion.Server.Models.Project", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<decimal?>("ActualCost")
                        .HasPrecision(18, 2)
                        .HasColumnType("numeric(18,2)");

                    b.Property<int>("ActualHours")
                        .HasColumnType("integer");

                    b.Property<decimal?>("BudgetValue")
                        .HasPrecision(18, 2)
                        .HasColumnType("numeric(18,2)");

                    b.Property<string>("Client")
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<string>("ContractNumber")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<DateTime>("CreatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Description")
                        .HasMaxLength(200)
                        .HasColumnType("character varying(200)");

                    b.Property<DateTime?>("EndDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("EstimatedHours")
                        .HasColumnType("integer");

                    b.Property<string>("FolderPath")
                        .HasMaxLength(500)
                        .HasColumnType("character varying(500)");

                    b.Property<DateTime?>("LastActivity")
                        .HasColumnType("timestamp with time zone");

                    b.Property<int>("MachineCount")
                        .HasColumnType("integer");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(100)
                        .HasColumnType("character varying(100)");

                    b.Property<decimal>("ProgressPercentage")
                        .HasPrecision(5, 2)
                        .HasColumnType("numeric(5,2)");

                    b.Property<string>("ResponsibleEngineer")
                        .HasMaxLength(50)
                        .HasColumnType("character varying(50)");

                    b.Property<DateTime?>("StartDate")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("Status")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("TotalBomVersions")
                        .HasColumnType("integer");

                    b.Property<DateTime>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.HasKey("Id");

                    b.HasIndex("ContractNumber");

                    b.HasIndex("CreatedAt");

                    b.HasIndex("Name");

                    b.HasIndex("Status");

                    b.ToTable("Projects");
                });

            modelBuilder.Entity("CADCompanion.Server.Models.BomVersion", b =>
                {
                    b.HasOne("CADCompanion.Server.Models.Machine", null)
                        .WithMany("BomVersions")
                        .HasForeignKey("MachineId1");

                    b.HasOne("CADCompanion.Server.Models.Project", null)
                        .WithMany("BomVersions")
                        .HasForeignKey("ProjectId1");
                });

            modelBuilder.Entity("CADCompanion.Server.Models.Machine", b =>
                {
                    b.HasOne("CADCompanion.Server.Models.Project", "Project")
                        .WithMany("Machines")
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Project");
                });

            modelBuilder.Entity("CADCompanion.Server.Models.Machine", b =>
                {
                    b.Navigation("BomVersions");
                });

            modelBuilder.Entity("CADCompanion.Server.Models.Project", b =>
                {
                    b.Navigation("BomVersions");

                    b.Navigation("Machines");
                });
#pragma warning restore 612, 618
        }
    }
}
