using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class QuestionCategoryParent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentId",
                table: "QuestionCategories",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_QuestionCategories_ParentId",
                table: "QuestionCategories",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_QuestionCategories_Parent",
                table: "QuestionCategories",
                column: "ParentId",
                principalTable: "QuestionCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_QuestionCategories_Parent",
                table: "QuestionCategories");

            migrationBuilder.DropIndex(
                name: "IX_QuestionCategories_ParentId",
                table: "QuestionCategories");

            migrationBuilder.DropColumn(
                name: "ParentId",
                table: "QuestionCategories");
        }
    }
}
