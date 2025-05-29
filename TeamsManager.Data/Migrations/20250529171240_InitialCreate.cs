using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TeamsManager.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationSettings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    IsRequired = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    DefaultValue = table.Column<string>(type: "TEXT", nullable: true),
                    ValidationPattern = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    ValidationMessage = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    DisplayOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Departments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ParentDepartmentId = table.Column<string>(type: "TEXT", nullable: true),
                    DepartmentCode = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Email = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Departments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Departments_Departments_ParentDepartmentId",
                        column: x => x.ParentDepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OperationHistories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    TargetEntityType = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetEntityId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    TargetEntityName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OperationDetails = table.Column<string>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    ErrorStackTrace = table.Column<string>(type: "TEXT", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    UserIpAddress = table.Column<string>(type: "TEXT", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SessionId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ParentOperationId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    SequenceNumber = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalItems = table.Column<int>(type: "INTEGER", nullable: true),
                    ProcessedItems = table.Column<int>(type: "INTEGER", nullable: true),
                    FailedItems = table.Column<int>(type: "INTEGER", nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OperationHistories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ShortName = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    FullName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ColorCode = table.Column<string>(type: "TEXT", maxLength: 7, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolTypes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SchoolYears",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsCurrent = table.Column<bool>(type: "INTEGER", nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    FirstSemesterStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    FirstSemesterEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SecondSemesterStart = table.Column<DateTime>(type: "TEXT", nullable: true),
                    SecondSemesterEnd = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SchoolYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    FirstName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    LastName = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    UPN = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    DepartmentId = table.Column<string>(type: "TEXT", nullable: false),
                    Phone = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    AlternateEmail = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    BirthDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EmploymentDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Position = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsSystemAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastLoginDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Users_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Subjects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Hours = table.Column<int>(type: "INTEGER", nullable: true),
                    DefaultSchoolTypeId = table.Column<string>(type: "TEXT", nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subjects_SchoolTypes_DefaultSchoolTypeId",
                        column: x => x.DefaultSchoolTypeId,
                        principalTable: "SchoolTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TeamTemplates",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Template = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsUniversal = table.Column<bool>(type: "INTEGER", nullable: false),
                    SchoolTypeId = table.Column<string>(type: "TEXT", nullable: true),
                    ExampleOutput = table.Column<string>(type: "TEXT", maxLength: 300, nullable: true),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    MaxLength = table.Column<int>(type: "INTEGER", nullable: true),
                    RemovePolishChars = table.Column<bool>(type: "INTEGER", nullable: false),
                    Prefix = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Suffix = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Separator = table.Column<string>(type: "TEXT", maxLength: 10, nullable: false),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    UsageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    LastUsedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamTemplates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamTemplates_SchoolTypes_SchoolTypeId",
                        column: x => x.SchoolTypeId,
                        principalTable: "SchoolTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "UserSchoolTypes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    SchoolTypeId = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsCurrentlyActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    WorkloadPercentage = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSchoolTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSchoolTypes_SchoolTypes_SchoolTypeId",
                        column: x => x.SchoolTypeId,
                        principalTable: "SchoolTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSchoolTypes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSchoolTypeSupervision",
                columns: table => new
                {
                    SupervisedSchoolTypesId = table.Column<string>(type: "TEXT", nullable: false),
                    SupervisingViceDirectorsId = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSchoolTypeSupervision", x => new { x.SupervisedSchoolTypesId, x.SupervisingViceDirectorsId });
                    table.ForeignKey(
                        name: "FK_UserSchoolTypeSupervision_SchoolTypes_SupervisedSchoolTypesId",
                        column: x => x.SupervisedSchoolTypesId,
                        principalTable: "SchoolTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSchoolTypeSupervision_Users_SupervisingViceDirectorsId",
                        column: x => x.SupervisingViceDirectorsId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserSubjects",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    SubjectId = table.Column<string>(type: "TEXT", nullable: false),
                    AssignedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSubjects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserSubjects_Subjects_SubjectId",
                        column: x => x.SubjectId,
                        principalTable: "Subjects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserSubjects_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Teams",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Owner = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusChangeDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StatusChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StatusChangeReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    TemplateId = table.Column<string>(type: "TEXT", nullable: true),
                    SchoolTypeId = table.Column<string>(type: "TEXT", nullable: true),
                    SchoolYearId = table.Column<string>(type: "TEXT", nullable: true),
                    AcademicYear = table.Column<string>(type: "TEXT", maxLength: 20, nullable: true),
                    Semester = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    StartDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    EndDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MaxMembers = table.Column<int>(type: "INTEGER", nullable: true),
                    ExternalId = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CourseCode = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TotalHours = table.Column<int>(type: "INTEGER", nullable: true),
                    Level = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", nullable: true),
                    IsVisible = table.Column<bool>(type: "INTEGER", nullable: false),
                    RequiresApproval = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastActivityDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Teams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Teams_SchoolTypes_SchoolTypeId",
                        column: x => x.SchoolTypeId,
                        principalTable: "SchoolTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Teams_SchoolYears_SchoolYearId",
                        column: x => x.SchoolYearId,
                        principalTable: "SchoolYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Teams_TeamTemplates_TemplateId",
                        column: x => x.TemplateId,
                        principalTable: "TeamTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    ChannelType = table.Column<string>(type: "TEXT", maxLength: 20, nullable: false),
                    IsGeneral = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsPrivate = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsReadOnly = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    StatusChangeDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    StatusChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    StatusChangeReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    LastActivityDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastMessageDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MessageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    FilesSize = table.Column<long>(type: "INTEGER", nullable: false),
                    NotificationSettings = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    IsModerationEnabled = table.Column<bool>(type: "INTEGER", nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    Tags = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    ExternalUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "INTEGER", nullable: false),
                    TeamId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeamMembers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Role = table.Column<int>(type: "INTEGER", nullable: false),
                    AddedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    RemovedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RemovalReason = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    AddedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RemovedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    RoleChangedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    RoleChangedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    PreviousRole = table.Column<int>(type: "INTEGER", nullable: true),
                    IsApproved = table.Column<bool>(type: "INTEGER", nullable: false),
                    ApprovedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ApprovedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    CanPost = table.Column<bool>(type: "INTEGER", nullable: false),
                    CanModerate = table.Column<bool>(type: "INTEGER", nullable: false),
                    CustomPermissions = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: true),
                    LastActivityDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    MessagesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 50, nullable: true),
                    TeamId = table.Column<string>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ModifiedDate = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ModifiedBy = table.Column<string>(type: "TEXT", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeamMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeamMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_Category",
                table: "ApplicationSettings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_CreatedBy",
                table: "ApplicationSettings",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_CreatedDate",
                table: "ApplicationSettings",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_IsActive",
                table: "ApplicationSettings",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_IsRequired",
                table: "ApplicationSettings",
                column: "IsRequired");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_IsVisible",
                table: "ApplicationSettings",
                column: "IsVisible");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_Key",
                table: "ApplicationSettings",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationSettings_Type",
                table: "ApplicationSettings",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_CreatedBy",
                table: "Channels",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_CreatedDate",
                table: "Channels",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_DisplayName",
                table: "Channels",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_IsActive",
                table: "Channels",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_IsGeneral",
                table: "Channels",
                column: "IsGeneral");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_IsPrivate",
                table: "Channels",
                column: "IsPrivate");

            migrationBuilder.CreateIndex(
                name: "IX_Channels_TeamId_DisplayName",
                table: "Channels",
                columns: new[] { "TeamId", "DisplayName" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CreatedBy",
                table: "Departments",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_CreatedDate",
                table: "Departments",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DepartmentCode",
                table: "Departments",
                column: "DepartmentCode");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_IsActive",
                table: "Departments",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_Name",
                table: "Departments",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_ParentDepartmentId",
                table: "Departments",
                column: "ParentDepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_CreatedBy",
                table: "OperationHistories",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_CreatedDate",
                table: "OperationHistories",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_IsActive",
                table: "OperationHistories",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_ParentOperationId",
                table: "OperationHistories",
                column: "ParentOperationId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_StartedAt",
                table: "OperationHistories",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_Status",
                table: "OperationHistories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_TargetEntityId",
                table: "OperationHistories",
                column: "TargetEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_TargetEntityType",
                table: "OperationHistories",
                column: "TargetEntityType");

            migrationBuilder.CreateIndex(
                name: "IX_OperationHistories_Type",
                table: "OperationHistories",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTypes_CreatedBy",
                table: "SchoolTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTypes_CreatedDate",
                table: "SchoolTypes",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTypes_FullName",
                table: "SchoolTypes",
                column: "FullName");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTypes_IsActive",
                table: "SchoolTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolTypes_ShortName",
                table: "SchoolTypes",
                column: "ShortName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYears_CreatedBy",
                table: "SchoolYears",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYears_CreatedDate",
                table: "SchoolYears",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYears_IsActive",
                table: "SchoolYears",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYears_IsCurrent",
                table: "SchoolYears",
                column: "IsCurrent");

            migrationBuilder.CreateIndex(
                name: "IX_SchoolYears_Name",
                table: "SchoolYears",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Code",
                table: "Subjects",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_CreatedBy",
                table: "Subjects",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_CreatedDate",
                table: "Subjects",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_DefaultSchoolTypeId",
                table: "Subjects",
                column: "DefaultSchoolTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_IsActive",
                table: "Subjects",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Subjects_Name",
                table: "Subjects",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_CreatedBy",
                table: "TeamMembers",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_CreatedDate",
                table: "TeamMembers",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_IsActive",
                table: "TeamMembers",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_IsApproved",
                table: "TeamMembers",
                column: "IsApproved");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_Role",
                table: "TeamMembers",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_TeamId",
                table: "TeamMembers",
                column: "TeamId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamMembers_UserId_TeamId",
                table: "TeamMembers",
                columns: new[] { "UserId", "TeamId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CreatedBy",
                table: "Teams",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_CreatedDate",
                table: "Teams",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_DisplayName",
                table: "Teams",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_ExternalId",
                table: "Teams",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_IsActive",
                table: "Teams",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Owner",
                table: "Teams",
                column: "Owner");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SchoolTypeId",
                table: "Teams",
                column: "SchoolTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_SchoolYearId",
                table: "Teams",
                column: "SchoolYearId");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_Status",
                table: "Teams",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Teams_TemplateId",
                table: "Teams",
                column: "TemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_Category",
                table: "TeamTemplates",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_CreatedBy",
                table: "TeamTemplates",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_CreatedDate",
                table: "TeamTemplates",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_IsActive",
                table: "TeamTemplates",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_IsDefault",
                table: "TeamTemplates",
                column: "IsDefault");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_IsUniversal",
                table: "TeamTemplates",
                column: "IsUniversal");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_Name",
                table: "TeamTemplates",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_TeamTemplates_SchoolTypeId",
                table: "TeamTemplates",
                column: "SchoolTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedBy",
                table: "Users",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Users_CreatedDate",
                table: "Users",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_Users_DepartmentId",
                table: "Users",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                table: "Users",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive",
                table: "Users",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_Users_UPN",
                table: "Users",
                column: "UPN",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolTypes_CreatedBy",
                table: "UserSchoolTypes",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolTypes_CreatedDate",
                table: "UserSchoolTypes",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolTypes_IsActive",
                table: "UserSchoolTypes",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolTypes_IsCurrentlyActive",
                table: "UserSchoolTypes",
                column: "IsCurrentlyActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolTypes_SchoolTypeId",
                table: "UserSchoolTypes",
                column: "SchoolTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolTypes_UserId_SchoolTypeId",
                table: "UserSchoolTypes",
                columns: new[] { "UserId", "SchoolTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserSchoolTypeSupervision_SupervisingViceDirectorsId",
                table: "UserSchoolTypeSupervision",
                column: "SupervisingViceDirectorsId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubjects_CreatedBy",
                table: "UserSubjects",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubjects_CreatedDate",
                table: "UserSubjects",
                column: "CreatedDate");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubjects_IsActive",
                table: "UserSubjects",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubjects_SubjectId",
                table: "UserSubjects",
                column: "SubjectId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSubjects_UserId_SubjectId",
                table: "UserSubjects",
                columns: new[] { "UserId", "SubjectId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationSettings");

            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "OperationHistories");

            migrationBuilder.DropTable(
                name: "TeamMembers");

            migrationBuilder.DropTable(
                name: "UserSchoolTypes");

            migrationBuilder.DropTable(
                name: "UserSchoolTypeSupervision");

            migrationBuilder.DropTable(
                name: "UserSubjects");

            migrationBuilder.DropTable(
                name: "Teams");

            migrationBuilder.DropTable(
                name: "Subjects");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "SchoolYears");

            migrationBuilder.DropTable(
                name: "TeamTemplates");

            migrationBuilder.DropTable(
                name: "Departments");

            migrationBuilder.DropTable(
                name: "SchoolTypes");
        }
    }
}
