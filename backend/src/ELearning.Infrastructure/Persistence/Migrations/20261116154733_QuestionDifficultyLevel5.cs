using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionDifficultyLevel5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamPoolRules_Difficulty",
                table: "ExamPoolRules");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions",
                sql: "\"Difficulty\" IN ('LEVEL1','LEVEL2','LEVEL3','LEVEL4','LEVEL5')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamPoolRules_Difficulty",
                table: "ExamPoolRules",
                sql: "\"Difficulty\" IN ('LEVEL1','LEVEL2','LEVEL3','LEVEL4','LEVEL5')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ExamPoolRules_Difficulty",
                table: "ExamPoolRules");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions",
                sql: "\"Difficulty\" IN ('LEVEL1','LEVEL2','LEVEL3','LEVEL4')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamPoolRules_Difficulty",
                table: "ExamPoolRules",
                sql: "\"Difficulty\" IN ('LEVEL1','LEVEL2','LEVEL3','LEVEL4')");
        }
    }
}
