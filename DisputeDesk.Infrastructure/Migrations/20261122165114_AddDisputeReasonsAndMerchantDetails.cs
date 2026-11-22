using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace DisputeDesk.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDisputeReasonsAndMerchantDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "city",
                table: "transactions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "merchant",
                table: "transactions",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reason_code",
                table: "disputes",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE transactions AS t
                SET merchant = COALESCE(t.merchant, source.merchant),
                    city = COALESCE(t.city, source.city)
                FROM (VALUES
                    ('20000000-0000-4000-8000-000000000001'::uuid, 'Corner Basket', 'Johannesburg'),
                    ('20000000-0000-4000-8000-000000000002'::uuid, 'Market Lane Online', NULL),
                    ('20000000-0000-4000-8000-000000000003'::uuid, 'Highway Fuel', 'Pretoria'),
                    ('20000000-0000-4000-8000-000000000004'::uuid, 'Connect Mobile', NULL)
                ) AS source(id, merchant, city)
                WHERE t.id = source.id;
                """);

            migrationBuilder.CreateTable(
                name: "dispute_reasons",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    label = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    requires_details = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dispute_reasons", x => x.code);
                });

            migrationBuilder.InsertData(
                table: "dispute_reasons",
                columns: new[] { "code", "is_active", "label", "requires_details", "sort_order" },
                values: new object[,]
                {
                    { "DUPLICATE", true, "I was charged more than once", false, 20 },
                    { "INCORRECT_AMOUNT", true, "I was charged the wrong amount", false, 30 },
                    { "NOT_RECEIVED", true, "I didn’t receive the goods or services", false, 50 },
                    { "OTHER", true, "Other", true, 60 },
                    { "REFUND_MISSING", true, "I didn’t receive my refund", false, 40 },
                    { "UNRECOGNISED", true, "I don’t recognise this transaction", false, 10 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_disputes_reason_code",
                table: "disputes",
                column: "reason_code");

            migrationBuilder.AddForeignKey(
                name: "FK_disputes_dispute_reasons_reason_code",
                table: "disputes",
                column: "reason_code",
                principalTable: "dispute_reasons",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_disputes_dispute_reasons_reason_code",
                table: "disputes");

            migrationBuilder.DropTable(
                name: "dispute_reasons");

            migrationBuilder.DropIndex(
                name: "IX_disputes_reason_code",
                table: "disputes");

            migrationBuilder.DropColumn(
                name: "city",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "merchant",
                table: "transactions");

            migrationBuilder.DropColumn(
                name: "reason_code",
                table: "disputes");
        }
    }
}
