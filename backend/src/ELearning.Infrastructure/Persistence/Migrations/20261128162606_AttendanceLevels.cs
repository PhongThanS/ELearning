using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LevelComment1",
                table: "AttendanceSessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelComment2",
                table: "AttendanceSessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelComment3",
                table: "AttendanceSessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelComment4",
                table: "AttendanceSessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LevelComment5",
                table: "AttendanceSessions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "AttendanceRecords",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceRecords_Level",
                table: "AttendanceRecords",
                sql: "\"Level\" IS NULL OR \"Level\" BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceRecords_Level",
                table: "AttendanceRecords");

            migrationBuilder.DropColumn(
                name: "LevelComment1",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "LevelComment2",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "LevelComment3",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "LevelComment4",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "LevelComment5",
                table: "AttendanceSessions");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "AttendanceRecords");
        }
    }
}
