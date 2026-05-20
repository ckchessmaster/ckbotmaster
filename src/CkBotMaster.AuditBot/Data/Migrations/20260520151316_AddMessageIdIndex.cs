using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CkBotMaster.AuditBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessageIdIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_MessageId",
                schema: "ckbotmaster",
                table: "audit_entries",
                column: "MessageId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_audit_entries_MessageId",
                schema: "ckbotmaster",
                table: "audit_entries");
        }
    }
}
