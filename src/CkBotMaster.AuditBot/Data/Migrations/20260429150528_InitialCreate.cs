using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CkBotMaster.AuditBot.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_entries",
                columns: table => new
                {
                    DiscordEntryId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    MessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ActorId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    FromCatchup = table.Column<bool>(type: "boolean", nullable: false),
                    ReasonStatus = table.Column<int>(type: "integer", nullable: false),
                    ReasonText = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.DiscordEntryId);
                });

            migrationBuilder.CreateTable(
                name: "bot_state",
                columns: table => new
                {
                    Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Value = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bot_state", x => x.Key);
                });

            migrationBuilder.CreateTable(
                name: "pending_reasons",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AuditEntryId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ActorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PromptMode = table.Column<int>(type: "integer", nullable: false),
                    PromptMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PromptChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    IsOpen = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pending_reasons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_pending_reasons_audit_entries_AuditEntryId",
                        column: x => x.AuditEntryId,
                        principalTable: "audit_entries",
                        principalColumn: "DiscordEntryId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_entries_CreatedAt",
                table: "audit_entries",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_pending_reasons_ActorId_IsOpen",
                table: "pending_reasons",
                columns: new[] { "ActorId", "IsOpen" });

            migrationBuilder.CreateIndex(
                name: "IX_pending_reasons_AuditEntryId",
                table: "pending_reasons",
                column: "AuditEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_pending_reasons_IsOpen_ExpiresAt",
                table: "pending_reasons",
                columns: new[] { "IsOpen", "ExpiresAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bot_state");

            migrationBuilder.DropTable(
                name: "pending_reasons");

            migrationBuilder.DropTable(
                name: "audit_entries");
        }
    }
}
