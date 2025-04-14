﻿// <auto-generated />
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Tingle.Dependabot.Models;

#nullable disable

namespace Tingle.Dependabot.Migrations
{
    [DbContext(typeof(MainDbContext))]
    partial class MainDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder.HasAnnotation("ProductVersion", "9.0.4");

            modelBuilder.Entity("Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.DataProtectionKey", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("INTEGER");

                    b.Property<string>("FriendlyName")
                        .HasColumnType("TEXT");

                    b.Property<string>("Xml")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.ToTable("Microsoft.AspNetCore.DataProtection.EntityFrameworkCore.IDataProtectionKeyContext.DataProtectionKeys");
                });

            modelBuilder.Entity("Tingle.Dependabot.Models.Management.Project", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<long>("Created")
                        .HasColumnType("INTEGER");

                    b.Property<bool>("Debug")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Description")
                        .HasColumnType("TEXT");

                    b.Property<string>("Experiments")
                        .HasColumnType("TEXT");

                    b.Property<string>("GithubToken")
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<bool>("Private")
                        .HasColumnType("INTEGER");

                    b.Property<string>("ProviderId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Secrets")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Slug")
                        .HasColumnType("TEXT");

                    b.Property<long?>("Synchronized")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<int>("Type")
                        .HasColumnType("INTEGER");

                    b.Property<long>("Updated")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Url")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("UserId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Created")
                        .IsDescending();

                    b.HasIndex("Password")
                        .IsUnique();

                    b.HasIndex("ProviderId")
                        .IsUnique();

                    b.ToTable("Projects");
                });

            modelBuilder.Entity("Tingle.Dependabot.Models.Management.Repository", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<string>("ConfigFileContents")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<long>("Created")
                        .HasColumnType("INTEGER");

                    b.Property<string>("LatestCommit")
                        .HasMaxLength(200)
                        .HasColumnType("TEXT");

                    b.Property<string>("Name")
                        .HasColumnType("TEXT");

                    b.Property<string>("ProjectId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ProviderId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Registries")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Slug")
                        .HasColumnType("TEXT");

                    b.Property<string>("SyncException")
                        .HasColumnType("TEXT");

                    b.Property<long>("Updated")
                        .HasColumnType("INTEGER");

                    b.Property<string>("Updates")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("Created")
                        .IsDescending();

                    b.HasIndex("ProjectId");

                    b.HasIndex("ProviderId")
                        .IsUnique();

                    b.ToTable("Repositories");
                });

            modelBuilder.Entity("Tingle.Dependabot.Models.Management.UpdateJob", b =>
                {
                    b.Property<string>("Id")
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<string>("AuthKey")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("Commit")
                        .HasMaxLength(50)
                        .HasColumnType("TEXT");

                    b.Property<long>("Created")
                        .HasColumnType("INTEGER");

                    b.PrimitiveCollection<string>("Directories")
                        .HasColumnType("TEXT");

                    b.Property<string>("Directory")
                        .HasColumnType("TEXT");

                    b.Property<long?>("Duration")
                        .HasColumnType("INTEGER");

                    b.Property<long?>("End")
                        .HasColumnType("INTEGER");

                    b.Property<string>("EventBusId")
                        .HasColumnType("TEXT");

                    b.Property<string>("FlameGraphPath")
                        .HasColumnType("TEXT");

                    b.Property<string>("LogsPath")
                        .HasColumnType("TEXT");

                    b.Property<string>("PackageEcosystem")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ProjectId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("ProxyImage")
                        .HasColumnType("TEXT");

                    b.Property<string>("RepositoryId")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<string>("RepositorySlug")
                        .IsRequired()
                        .HasColumnType("TEXT");

                    b.Property<long?>("Start")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Status")
                        .HasColumnType("INTEGER");

                    b.Property<int>("Trigger")
                        .HasColumnType("INTEGER");

                    b.Property<string>("UpdaterImage")
                        .HasColumnType("TEXT");

                    b.HasKey("Id");

                    b.HasIndex("AuthKey")
                        .IsUnique();

                    b.HasIndex("Created")
                        .IsDescending();

                    b.HasIndex("ProjectId");

                    b.HasIndex("RepositoryId");

                    b.HasIndex("PackageEcosystem", "Directory", "Directories");

                    b.HasIndex("PackageEcosystem", "Directory", "Directories", "EventBusId")
                        .IsUnique();

                    b.ToTable("UpdateJobs");
                });

            modelBuilder.Entity("Tingle.Dependabot.Models.Management.Project", b =>
                {
                    b.OwnsOne("Tingle.Dependabot.Models.Management.ProjectAutoApprove", "AutoApprove", b1 =>
                        {
                            b1.Property<string>("ProjectId")
                                .HasColumnType("TEXT");

                            b1.Property<bool>("Enabled")
                                .HasColumnType("INTEGER");

                            b1.HasKey("ProjectId");

                            b1.ToTable("Projects");

                            b1.WithOwner()
                                .HasForeignKey("ProjectId");
                        });

                    b.OwnsOne("Tingle.Dependabot.Models.Management.ProjectAutoComplete", "AutoComplete", b1 =>
                        {
                            b1.Property<string>("ProjectId")
                                .HasColumnType("TEXT");

                            b1.Property<bool>("Enabled")
                                .HasColumnType("INTEGER");

                            b1.PrimitiveCollection<string>("IgnoreConfigs")
                                .HasColumnType("TEXT");

                            b1.Property<int?>("MergeStrategy")
                                .HasColumnType("INTEGER");

                            b1.HasKey("ProjectId");

                            b1.ToTable("Projects");

                            b1.WithOwner()
                                .HasForeignKey("ProjectId");
                        });

                    b.Navigation("AutoApprove")
                        .IsRequired();

                    b.Navigation("AutoComplete")
                        .IsRequired();
                });

            modelBuilder.Entity("Tingle.Dependabot.Models.Management.Repository", b =>
                {
                    b.HasOne("Tingle.Dependabot.Models.Management.Project", null)
                        .WithMany()
                        .HasForeignKey("ProjectId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Tingle.Dependabot.Models.Management.UpdateJob", b =>
                {
                    b.OwnsOne("Tingle.Dependabot.Models.Management.UpdateJobError", "Error", b1 =>
                        {
                            b1.Property<string>("UpdateJobId")
                                .HasColumnType("TEXT");

                            b1.Property<string>("Detail")
                                .HasColumnType("TEXT");

                            b1.Property<string>("Type")
                                .IsRequired()
                                .HasColumnType("TEXT");

                            b1.HasKey("UpdateJobId");

                            b1.HasIndex("Type");

                            b1.ToTable("UpdateJobs");

                            b1.WithOwner()
                                .HasForeignKey("UpdateJobId");
                        });

                    b.OwnsOne("Tingle.Dependabot.Models.Management.UpdateJobResources", "Resources", b1 =>
                        {
                            b1.Property<string>("UpdateJobId")
                                .HasColumnType("TEXT");

                            b1.Property<double>("Cpu")
                                .HasColumnType("REAL");

                            b1.Property<double>("Memory")
                                .HasColumnType("REAL");

                            b1.HasKey("UpdateJobId");

                            b1.ToTable("UpdateJobs");

                            b1.WithOwner()
                                .HasForeignKey("UpdateJobId");
                        });

                    b.Navigation("Error");

                    b.Navigation("Resources")
                        .IsRequired();
                });
#pragma warning restore 612, 618
        }
    }
}
