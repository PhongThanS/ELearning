using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHomeroomTeacherToClassroom : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "HomeroomTeacherId",
                table: "Classrooms",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Classrooms_HomeroomTeacherId",
                table: "Classrooms",
                column: "HomeroomTeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Classrooms_HomeroomTeacher",
                table: "Classrooms",
                column: "HomeroomTeacherId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Classrooms_HomeroomTeacher",
                table: "Classrooms");

            migrationBuilder.DropIndex(
                name: "IX_Classrooms_HomeroomTeacherId",
                table: "Classrooms");

            migrationBuilder.DropColumn(
                name: "HomeroomTeacherId",
                table: "Classrooms");
        }
    }
}
