using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopSphere.Payment.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PaymentOperations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLive",
                table: "Payments",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "Reconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OrderId = table.Column<Guid>(type: "uuid", nullable: false),
                    Actor = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    ProviderStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ProviderPaymentStatus = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Outcome = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ErrorType = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: true),
                    ReconciledAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reconciliations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reconciliations_OrderId_ReconciledAt",
                table: "Reconciliations",
                columns: new[] { "OrderId", "ReconciledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reconciliations");

            migrationBuilder.DropColumn(
                name: "IsLive",
                table: "Payments");
        }
    }
}
