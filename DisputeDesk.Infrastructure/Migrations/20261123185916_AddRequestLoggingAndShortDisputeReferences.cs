using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace DisputeDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRequestLoggingAndShortDisputeReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence<int>(
                name: "dispute_reference_numbers",
                maxValue: 99999999L);

            // A rollback preserves the short references already issued.
            migrationBuilder.Sql("""
                SELECT setval(
                    'dispute_reference_numbers',
                    GREATEST(COALESCE(MAX(substring(reference_number from 4)::bigint), 0), 1),
                    COUNT(*) > 0)
                FROM disputes
                WHERE reference_number ~ '^DD-[0-9]{8}$';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "reference_number",
                table: "disputes",
                type: "text",
                nullable: false,
                defaultValueSql: "'DD-' || lpad(nextval('dispute_reference_numbers')::text, 8, '0')",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "api_request_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    route_pattern = table.Column<string>(type: "text", nullable: false),
                    status_code = table.Column<int>(type: "integer", nullable: false),
                    duration_ms = table.Column<double>(type: "double precision", nullable: false),
                    level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_api_request_logs", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_api_request_logs_correlation_id",
                table: "api_request_logs",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "IX_api_request_logs_occurred_at",
                table: "api_request_logs",
                column: "occurred_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "api_request_logs");

            migrationBuilder.AlterColumn<string>(
                name: "reference_number",
                table: "disputes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValueSql: "'DD-' || lpad(nextval('dispute_reference_numbers')::text, 8, '0')");

            migrationBuilder.DropSequence(
                name: "dispute_reference_numbers");
        }
    }
}
