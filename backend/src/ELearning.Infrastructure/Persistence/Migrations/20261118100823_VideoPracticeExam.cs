using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VideoPracticeExam : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PracticeExamId",
                table: "VideoLessons",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoLessons_PracticeExamId",
                table: "VideoLessons",
                column: "PracticeExamId");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoLessons_PracticeExam",
                table: "VideoLessons",
                column: "PracticeExamId",
                principalTable: "Exams",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_VideoLessons_PracticeExam",
                table: "VideoLessons");

            migrationBuilder.DropIndex(
                name: "IX_VideoLessons_PracticeExamId",
                table: "VideoLessons");

            migrationBuilder.DropColumn(
                name: "PracticeExamId",
                table: "VideoLessons");
        }
    }
}
