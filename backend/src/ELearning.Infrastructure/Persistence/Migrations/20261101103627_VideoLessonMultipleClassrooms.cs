using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class VideoLessonMultipleClassrooms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Tạo bảng nối trước, chép lớp hiện có của từng video sang, rồi mới bỏ cột cũ (không mất dữ liệu)
            migrationBuilder.CreateTable(
                name: "VideoLessonClassrooms",
                columns: table => new
                {
                    VideoLessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassroomId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoLessonClassrooms", x => new { x.VideoLessonId, x.ClassroomId });
                    table.ForeignKey(
                        name: "FK_VideoLessonClassrooms_Classroom",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VideoLessonClassrooms_VideoLesson",
                        column: x => x.VideoLessonId,
                        principalTable: "VideoLessons",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_VideoLessonClassrooms_Classroom",
                table: "VideoLessonClassrooms",
                column: "ClassroomId");

            migrationBuilder.Sql(
                """
                INSERT INTO "VideoLessonClassrooms" ("VideoLessonId", "ClassroomId")
                SELECT "Id", "ClassroomId" FROM "VideoLessons" WHERE "ClassroomId" IS NOT NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_VideoLessons_Classroom",
                table: "VideoLessons");

            migrationBuilder.DropIndex(
                name: "IX_VideoLessons_ClassroomId",
                table: "VideoLessons");

            migrationBuilder.DropColumn(
                name: "ClassroomId",
                table: "VideoLessons");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClassroomId",
                table: "VideoLessons",
                type: "uuid",
                nullable: true);

            // Cột cũ chỉ giữ được một lớp: lấy lớp có mã nhỏ nhất của mỗi video
            migrationBuilder.Sql(
                """
                UPDATE "VideoLessons" v SET "ClassroomId" = (
                    SELECT vc."ClassroomId" FROM "VideoLessonClassrooms" vc
                    JOIN "Classrooms" c ON c."Id" = vc."ClassroomId"
                    WHERE vc."VideoLessonId" = v."Id"
                    ORDER BY c."Code" LIMIT 1);
                """);

            migrationBuilder.DropTable(
                name: "VideoLessonClassrooms");

            migrationBuilder.CreateIndex(
                name: "IX_VideoLessons_ClassroomId",
                table: "VideoLessons",
                column: "ClassroomId");

            migrationBuilder.AddForeignKey(
                name: "FK_VideoLessons_Classroom",
                table: "VideoLessons",
                column: "ClassroomId",
                principalTable: "Classrooms",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
