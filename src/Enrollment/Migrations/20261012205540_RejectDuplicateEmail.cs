using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Enrollment.Migrations
{
    /// <inheritdoc />
    public partial class RejectDuplicateEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Enrollments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.Sql("""
                ;WITH ranked AS (
                    SELECT TrackingId,
                           ROW_NUMBER() OVER (PARTITION BY Email ORDER BY CreatedUtc, TrackingId) AS rn
                    FROM Enrollments
                    WHERE Status = 'completed'
                )
                UPDATE e
                SET Status = 'rejected',
                    Message = 'An enrollment for this email already exists.',
                    CompletedUtc = NULL
                FROM Enrollments e
                INNER JOIN ranked r ON r.TrackingId = e.TrackingId
                WHERE r.rn > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Enrollments_CompletedEmail",
                table: "Enrollments",
                column: "Email",
                unique: true,
                filter: "[Status] = 'completed'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Enrollments_CompletedEmail",
                table: "Enrollments");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Enrollments");
        }
    }
}
