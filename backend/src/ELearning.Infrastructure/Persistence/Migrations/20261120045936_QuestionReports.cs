using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestionReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExamQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestionOrder = table.Column<int>(type: "integer", nullable: false),
                    ReportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: false),
                    Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolutionNote = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestionReports", x => x.Id);
                    table.CheckConstraint("CK_QuestionReports_Reason", "\"Reason\" IN ('WRONG_ANSWER','WRONG_CONTENT','TYPO','MISSING_MEDIA','OTHER')");
                    table.CheckConstraint("CK_QuestionReports_Status", "\"Status\" IN ('OPEN','RESOLVED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_QuestionReports_Attempt",
                        column: x => x.AttemptId,
                        principalTable: "ExamAttempts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionReports_Exam",
                        column: x => x.ExamId,
                        principalTable: "Exams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionReports_ExamQuestion",
                        column: x => x.ExamQuestionId,
                        principalTable: "ExamQuestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionReports_ReportedBy",
                        column: x => x.ReportedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_QuestionReports_ResolvedBy",
                        column: x => x.ResolvedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReports_ExamId",
                table: "QuestionReports",
                column: "ExamId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReports_ExamQuestion",
                table: "QuestionReports",
                column: "ExamQuestionId");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReports_ReportedBy",
                table: "QuestionReports",
                column: "ReportedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReports_ResolvedBy",
                table: "QuestionReports",
                column: "ResolvedBy");

            migrationBuilder.CreateIndex(
                name: "IX_QuestionReports_Status_Question",
                table: "QuestionReports",
                columns: new[] { "Status", "ExamQuestionId" });

            migrationBuilder.CreateIndex(
                name: "UQ_QuestionReports_Attempt_Question",
                table: "QuestionReports",
                columns: new[] { "AttemptId", "ExamQuestionId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestionReports");
        }
    }
}
