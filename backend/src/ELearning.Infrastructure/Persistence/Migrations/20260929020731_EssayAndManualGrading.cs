using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EssayAndManualGrading : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_Type",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamQuestions_Type",
                table: "ExamQuestions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamPoolRules_Type",
                table: "ExamPoolRules");

            migrationBuilder.AddColumn<bool>(
                name: "PartialScoring",
                table: "Questions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "PendingManualCount",
                table: "ExamResults",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<bool>(
                name: "PartialScoring",
                table: "ExamQuestions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AlterColumn<string>(
                name: "AnswerText",
                table: "AttemptAnswers",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ManualComment",
                table: "AttemptAnswers",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ManualGradedAt",
                table: "AttemptAnswers",
                type: "datetime2(3)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ManualGradedBy",
                table: "AttemptAnswers",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ManualScore",
                table: "AttemptAnswers",
                type: "decimal(10,2)",
                precision: 10,
                scale: 2,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Type",
                table: "Questions",
                sql: "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN','ESSAY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamQuestions_Type",
                table: "ExamQuestions",
                sql: "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN','ESSAY')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamPoolRules_Type",
                table: "ExamPoolRules",
                sql: "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN','ESSAY')");

            migrationBuilder.CreateIndex(
                name: "IX_AttemptAnswers_ManualGradedBy",
                table: "AttemptAnswers",
                column: "ManualGradedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_AttemptAnswers_ManualGradedBy",
                table: "AttemptAnswers",
                column: "ManualGradedBy",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AttemptAnswers_ManualGradedBy",
                table: "AttemptAnswers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_Type",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamQuestions_Type",
                table: "ExamQuestions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamPoolRules_Type",
                table: "ExamPoolRules");

            migrationBuilder.DropIndex(
                name: "IX_AttemptAnswers_ManualGradedBy",
                table: "AttemptAnswers");

            migrationBuilder.DropColumn(
                name: "PartialScoring",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "PendingManualCount",
                table: "ExamResults");

            migrationBuilder.DropColumn(
                name: "PartialScoring",
                table: "ExamQuestions");

            migrationBuilder.DropColumn(
                name: "ManualComment",
                table: "AttemptAnswers");

            migrationBuilder.DropColumn(
                name: "ManualGradedAt",
                table: "AttemptAnswers");

            migrationBuilder.DropColumn(
                name: "ManualGradedBy",
                table: "AttemptAnswers");

            migrationBuilder.DropColumn(
                name: "ManualScore",
                table: "AttemptAnswers");

            migrationBuilder.AlterColumn<string>(
                name: "AnswerText",
                table: "AttemptAnswers",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Type",
                table: "Questions",
                sql: "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamQuestions_Type",
                table: "ExamQuestions",
                sql: "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamPoolRules_Type",
                table: "ExamPoolRules",
                sql: "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN')");
        }
    }
}
