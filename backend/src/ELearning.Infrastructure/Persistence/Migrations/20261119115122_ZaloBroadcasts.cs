using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ELearning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ZaloBroadcasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ZaloBroadcasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassroomId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: false),
                    SessionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Content = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZaloBroadcasts", x => x.Id);
                    table.CheckConstraint("CK_ZaloBroadcasts_Kind", "\"Kind\" IN ('DAILY_REPORT','ANNOUNCEMENT')");
                    table.ForeignKey(
                        name: "FK_ZaloBroadcasts_Classroom",
                        column: x => x.ClassroomId,
                        principalTable: "Classrooms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZaloBroadcasts_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ZaloMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BroadcastId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(15)", unicode: false, maxLength: 15, nullable: false),
                    TemplateId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: false),
                    TemplateData = table.Column<string>(type: "text", nullable: false),
                    TrackingId = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", unicode: false, maxLength: 40, nullable: false),
                    Attempts = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SendingAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ProviderMessageId = table.Column<string>(type: "character varying(100)", unicode: false, maxLength: 100, nullable: true),
                    ErrorCode = table.Column<string>(type: "character varying(50)", unicode: false, maxLength: 50, nullable: true),
                    ErrorMessage = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZaloMessages", x => x.Id);
                    table.CheckConstraint("CK_ZaloMessages_Status", "\"Status\" IN ('PENDING','SENDING','SENT','FAILED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_ZaloMessages_Broadcast",
                        column: x => x.BroadcastId,
                        principalTable: "ZaloBroadcasts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ZaloMessages_Student",
                        column: x => x.StudentId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ZaloBroadcasts_Classroom_CreatedAt",
                table: "ZaloBroadcasts",
                columns: new[] { "ClassroomId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ZaloBroadcasts_CreatedBy",
                table: "ZaloBroadcasts",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_ZaloMessages_Broadcast",
                table: "ZaloMessages",
                column: "BroadcastId");

            migrationBuilder.CreateIndex(
                name: "IX_ZaloMessages_Queue",
                table: "ZaloMessages",
                columns: new[] { "Status", "CreatedAt" },
                filter: "\"Status\" IN ('PENDING','SENDING')");

            migrationBuilder.CreateIndex(
                name: "IX_ZaloMessages_StudentId",
                table: "ZaloMessages",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "UQ_ZaloMessages_TrackingId",
                table: "ZaloMessages",
                column: "TrackingId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ZaloMessages");

            migrationBuilder.DropTable(
                name: "ZaloBroadcasts");
        }
    }
}
