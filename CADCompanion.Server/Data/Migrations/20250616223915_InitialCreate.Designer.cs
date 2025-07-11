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
    [Migration("20250616223915_InitialCreate")]
    partial class InitialCreate
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
                        .HasColumnType("text");

                    b.Property<DateTime>("ExtractedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<string>("ExtractedBy")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<List<BomItemDto>>("Items")
                        .IsRequired()
                        .HasColumnType("jsonb");

                    b.Property<string>("MachineId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<string>("ProjectId")
                        .IsRequired()
                        .HasColumnType("text");

                    b.Property<int>("VersionNumber")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.ToTable("BomVersions");
                });
#pragma warning restore 612, 618
        }
    }
}
