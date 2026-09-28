using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionBank : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionAcceptedAnswers_Question",
                table: "ExamQuestionAcceptedAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionOptions_Question",
                table: "ExamQuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionAcceptedAnswers_Question",
                table: "QuestionAcceptedAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionOptions_Question",
                table: "QuestionOptions");

            migrationBuilder.CreateSequence(
                name: "QuestionCodeSequence");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionAcceptedAnswers_Question",
                table: "ExamQuestionAcceptedAnswers",
                column: "ExamQuestionId",
                principalTable: "ExamQuestions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionOptions_Question",
                table: "ExamQuestionOptions",
                column: "ExamQuestionId",
                principalTable: "ExamQuestions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionAcceptedAnswers_Question",
                table: "QuestionAcceptedAnswers",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_Question",
                table: "QuestionOptions",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionAcceptedAnswers_Question",
                table: "ExamQuestionAcceptedAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestionOptions_Question",
                table: "ExamQuestionOptions");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionAcceptedAnswers_Question",
                table: "QuestionAcceptedAnswers");

            migrationBuilder.DropForeignKey(
                name: "FK_QuestionOptions_Question",
                table: "QuestionOptions");

            migrationBuilder.DropSequence(
                name: "QuestionCodeSequence");

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionAcceptedAnswers_Question",
                table: "ExamQuestionAcceptedAnswers",
                column: "ExamQuestionId",
                principalTable: "ExamQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestionOptions_Question",
                table: "ExamQuestionOptions",
                column: "ExamQuestionId",
                principalTable: "ExamQuestions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionAcceptedAnswers_Question",
                table: "QuestionAcceptedAnswers",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionOptions_Question",
                table: "QuestionOptions",
                column: "QuestionId",
                principalTable: "Questions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
