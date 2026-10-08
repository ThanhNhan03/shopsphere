using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ShopSphere.Gateway.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GoogleAccountIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "GoogleLastLoginAt",
                table: "Accounts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GooglePictureUrl",
                table: "Accounts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "GoogleSubjectHash",
                table: "Accounts",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Accounts_GoogleSubjectHash",
                table: "Accounts",
                column: "GoogleSubjectHash",
                unique: true,
                filter: "\"GoogleSubjectHash\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Accounts_GoogleSubjectHash",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "GoogleLastLoginAt",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "GooglePictureUrl",
                table: "Accounts");

            migrationBuilder.DropColumn(
                name: "GoogleSubjectHash",
                table: "Accounts");
        }
    }
}
