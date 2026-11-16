using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AttendanceStatusOnline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceRecords_Status",
                table: "AttendanceRecords");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceRecords_Status",
                table: "AttendanceRecords",
                sql: "\"Status\" IN ('PRESENT','LATE','EXCUSED','UNEXCUSED','ONLINE')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_AttendanceRecords_Status",
                table: "AttendanceRecords");

            migrationBuilder.AddCheckConstraint(
                name: "CK_AttendanceRecords_Status",
                table: "AttendanceRecords",
                sql: "\"Status\" IN ('PRESENT','LATE','EXCUSED','UNEXCUSED')");
        }
    }
}
