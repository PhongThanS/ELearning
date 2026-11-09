using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionDifficultyFourLevels : Migration
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

            // Độ khó 3 mức → 4 mức: Dễ = Mức 1, Trung bình = Mức 2, Khó = Mức 3 (Mức 4 dùng cho câu mới)
            foreach (var table in new[] { "Questions", "ExamPoolRules" })
            {
                migrationBuilder.Sql(
                    $"""
                    UPDATE "{table}" SET "Difficulty" = CASE "Difficulty"
                        WHEN 'EASY' THEN 'LEVEL1' WHEN 'MEDIUM' THEN 'LEVEL2' WHEN 'HARD' THEN 'LEVEL3' ELSE "Difficulty" END
                    WHERE "Difficulty" IN ('EASY', 'MEDIUM', 'HARD');
                    """);
            }

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions",
                sql: "\"Difficulty\" IN ('LEVEL1','LEVEL2','LEVEL3','LEVEL4')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamPoolRules_Difficulty",
                table: "ExamPoolRules",
                sql: "\"Difficulty\" IN ('LEVEL1','LEVEL2','LEVEL3','LEVEL4')");
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

            // Quay lại 3 mức: Mức 4 gộp vào Khó
            foreach (var table in new[] { "Questions", "ExamPoolRules" })
            {
                migrationBuilder.Sql(
                    $"""
                    UPDATE "{table}" SET "Difficulty" = CASE "Difficulty"
                        WHEN 'LEVEL1' THEN 'EASY' WHEN 'LEVEL2' THEN 'MEDIUM' ELSE 'HARD' END
                    WHERE "Difficulty" IN ('LEVEL1', 'LEVEL2', 'LEVEL3', 'LEVEL4');
                    """);
            }

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions",
                sql: "\"Difficulty\" IN ('EASY','MEDIUM','HARD')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ExamPoolRules_Difficulty",
                table: "ExamPoolRules",
                sql: "\"Difficulty\" IN ('EASY','MEDIUM','HARD')");
        }
    }
}
