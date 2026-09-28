using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExamVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamAssignments_Exam",
                table: "ExamAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestions_Version",
                table: "ExamQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamVersions_Exam",
                table: "ExamVersions");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAssignments_Exam",
                table: "ExamAssignments",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestions_Version",
                table: "ExamQuestions",
                column: "ExamVersionId",
                principalTable: "ExamVersions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamVersions_Exam",
                table: "ExamVersions",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamAssignments_Exam",
                table: "ExamAssignments");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestions_Version",
                table: "ExamQuestions");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamVersions_Exam",
                table: "ExamVersions");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamAssignments_Exam",
                table: "ExamAssignments",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestions_Version",
                table: "ExamQuestions",
                column: "ExamVersionId",
                principalTable: "ExamVersions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamVersions_Exam",
                table: "ExamVersions",
                column: "ExamId",
                principalTable: "Exams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
