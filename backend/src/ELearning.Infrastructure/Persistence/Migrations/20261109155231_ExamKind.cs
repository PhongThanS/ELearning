using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExamKind : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Kind",
                table: "Exams",
                type: "character varying(40)",
                unicode: false,
                maxLength: 40,
                nullable: false,
                defaultValue: "GRADED");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Exams_Kind",
                table: "Exams",
                sql: "\"Kind\" IN ('GRADED','PRACTICE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Exams_Kind",
                table: "Exams");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "Exams");
        }
    }
}
