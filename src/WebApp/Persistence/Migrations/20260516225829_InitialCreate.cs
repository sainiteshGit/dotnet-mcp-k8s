using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApp.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pgcrypto", ",,");

            migrationBuilder.CreateTable(
                name: "tasks",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "text", nullable: false, defaultValue: "todo"),
                    priority = table.Column<string>(type: "text", nullable: false, defaultValue: "medium"),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamptz", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamptz", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tasks", x => x.id);
                    table.CheckConstraint("chk_tasks_desc_len", "description IS NULL OR char_length(description) <= 2000");
                    table.CheckConstraint("chk_tasks_priority", "priority IN ('low','medium','high')");
                    table.CheckConstraint("chk_tasks_status", "status IN ('todo','in_progress','done')");
                    table.CheckConstraint("chk_tasks_title_len", "char_length(title) BETWEEN 1 AND 200");
                });

            migrationBuilder.CreateIndex(
                name: "ix_tasks_created_at_desc",
                table: "tasks",
                column: "created_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_tasks_status_priority_due_date",
                table: "tasks",
                columns: new[] { "status", "priority", "due_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "tasks");
        }
    }
}
