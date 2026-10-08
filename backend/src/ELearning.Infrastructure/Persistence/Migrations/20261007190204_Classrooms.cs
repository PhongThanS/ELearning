using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Classrooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAssignments_Target",
                table: "ExamAssignments");

            migrationBuilder.AddColumn<Guid>(
                name: "ClassroomId",
                table: "ExamAssignments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Classrooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "varchar(50)", unicode: false, maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SchoolYear = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false),
                    UpdatedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Classrooms", x => x.Id);
                    table.CheckConstraint("CK_Classrooms_Dates", "[EndDate] IS NULL OR [StartDate] IS NULL OR [EndDate] >= [StartDate]");
                    table.ForeignKey(
                        name: "FK_Classrooms_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Classrooms_UpdatedBy",
                        column: x => x.UpdatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClassroomStudents",
                columns: table => new
                {
                    ClassroomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "datetime2(3)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassroomStudents", x => new { x.ClassroomId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ClassroomStudents_Classroom",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassroomStudents_User",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExamAssignments_ClassroomId",
                table: "ExamAssignments",
                column: "ClassroomId",
                filter: "[ClassroomId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "UX_ExamAssignments_Classroom",
                table: "ExamAssignments",
                columns: new[] { "ExamId", "ClassroomId" },
                unique: true,
                filter: "[ClassroomId] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAssignments_Target",
                table: "ExamAssignments",
                sql: "(CASE WHEN [GroupId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [UserId] IS NULL THEN 0 ELSE 1 END + CASE WHEN [ClassroomId] IS NULL THEN 0 ELSE 1 END) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_CreatedBy",
                table: "Classrooms",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_UpdatedBy",
                table: "Classrooms",
                column: "UpdatedBy");

            migrationBuilder.CreateIndex(
                name: "UQ_Classrooms_Code",
                table: "Classrooms",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassroomStudents_User",
                table: "ClassroomStudents",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAssignments_Classroom",
                table: "ExamAssignments",
                column: "ClassroomId",
                principalTable: "Classrooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamAssignments_Classroom",
                table: "ExamAssignments");

            migrationBuilder.DropTable(
                name: "ClassroomStudents");

            migrationBuilder.DropTable(
                name: "Classrooms");

            migrationBuilder.DropIndex(
                name: "IX_ExamAssignments_ClassroomId",
                table: "ExamAssignments");

            migrationBuilder.DropIndex(
                name: "UX_ExamAssignments_Classroom",
                table: "ExamAssignments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamAssignments_Target",
                table: "ExamAssignments");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                table: "ExamAssignments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamAssignments_Target",
                table: "ExamAssignments",
                sql: "([GroupId] IS NULL AND [UserId] IS NOT NULL) OR ([GroupId] IS NOT NULL AND [UserId] IS NULL)");
        }
    }
}
