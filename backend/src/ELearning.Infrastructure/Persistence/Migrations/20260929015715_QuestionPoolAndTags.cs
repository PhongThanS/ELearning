using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionPoolAndTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Difficulty",
                table: "Questions",
                type: "varchar(40)",
                unicode: false,
                maxLength: 40,
                nullable: true,
                collation: "Latin1_General_100_BIN2");

            migrationBuilder.AddColumn<Guid>(
                name: "PoolRuleId",
                table: "ExamQuestions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ExamPoolRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExamVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RuleOrder = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Difficulty = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true, collation: "Latin1_General_100_BIN2"),
                    Tag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    QuestionType = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: true, collation: "Latin1_General_100_BIN2"),
                    DrawCount = table.Column<int>(type: "int", nullable: false),
                    ScorePerQuestion = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExamPoolRules", x => x.Id);
                    table.CheckConstraint("CK_ExamPoolRules_Difficulty", "[Difficulty] IN ('EASY','MEDIUM','HARD')");
                    table.CheckConstraint("CK_ExamPoolRules_DrawCount", "[DrawCount] BETWEEN 1 AND 500");
                    table.CheckConstraint("CK_ExamPoolRules_Score", "[ScorePerQuestion] > 0");
                    table.CheckConstraint("CK_ExamPoolRules_Type", "[QuestionType] IN ('SINGLE_CHOICE','MULTIPLE_CHOICE','TRUE_FALSE','FILL_IN')");
                    table.ForeignKey(
                        name: "FK_ExamPoolRules_Category",
                        column: x => x.CategoryId,
                        principalTable: "QuestionCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ExamPoolRules_Version",
                        column: x => x.ExamVersionId,
                        principalTable: "ExamVersions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "QuestionTags",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    QuestionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tag = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuestionTags_Question",
                        column: x => x.QuestionId,
                        principalTable: "Questions",
                        principalColumn: "Id");
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions",
                sql: "[Difficulty] IN ('EASY','MEDIUM','HARD')");

            migrationBuilder.CreateIndex(
                name: "IX_ExamQuestions_PoolRule",
                table: "ExamQuestions",
                column: "PoolRuleId",
                filter: "[PoolRuleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ExamPoolRules_CategoryId",
                table: "ExamPoolRules",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "UQ_ExamPoolRules_Order",
                table: "ExamPoolRules",
                columns: new[] { "ExamVersionId", "RuleOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionTags_Tag",
                table: "QuestionTags",
                column: "Tag");

            migrationBuilder.CreateIndex(
                name: "UQ_QuestionTags_Tag",
                table: "QuestionTags",
                columns: new[] { "QuestionId", "Tag" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ExamQuestions_PoolRule",
                table: "ExamQuestions",
                column: "PoolRuleId",
                principalTable: "ExamPoolRules",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExamQuestions_PoolRule",
                table: "ExamQuestions");

            migrationBuilder.DropTable(
                name: "ExamPoolRules");

            migrationBuilder.DropTable(
                name: "QuestionTags");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Questions_Difficulty",
                table: "Questions");

            migrationBuilder.DropIndex(
                name: "IX_ExamQuestions_PoolRule",
                table: "ExamQuestions");

            migrationBuilder.DropColumn(
                name: "Difficulty",
                table: "Questions");

            migrationBuilder.DropColumn(
                name: "PoolRuleId",
                table: "ExamQuestions");
        }
    }
}
