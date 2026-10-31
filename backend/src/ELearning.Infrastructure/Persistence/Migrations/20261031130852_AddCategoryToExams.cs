using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryToExams : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CategoryId",
                table: "Exams",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Exams_CategoryId",
                table: "Exams",
                column: "CategoryId");

            migrationBuilder.AddForeignKey(
                name: "FK_Exams_Category",
                table: "Exams",
                column: "CategoryId",
                principalTable: "QuestionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Exams_Category",
                table: "Exams");

            migrationBuilder.DropIndex(
                name: "IX_Exams_CategoryId",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "CategoryId",
                table: "Exams");
        }
    }
}
