using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ShopSphere.Catalog.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    Brand = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Products", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Products",
                columns: new[] { "Id", "Brand", "Category", "Description", "ImageUrl", "Name", "Price" },
                values: new object[,]
                {
                    { new Guid("00000000-0000-0000-0000-000000000001"), "Apple", "Laptops", "A light, powerful laptop for everyday creative work. 13-inch display, 16 GB memory, 256 GB SSD.", "/products/laptop.svg", "MacBook Air M4", 999m },
                    { new Guid("00000000-0000-0000-0000-000000000002"), "Sony", "Audio", "Wireless over-ear headphones with noise cancellation and up to 30 hours of battery life.", "/products/headphones.svg", "WH-1000XM5", 299m },
                    { new Guid("00000000-0000-0000-0000-000000000003"), "Logitech", "Accessories", "A comfortable wireless mouse with quiet clicks, precise scrolling and USB-C charging.", "/products/mouse.svg", "MX Master 3S", 99m },
                    { new Guid("00000000-0000-0000-0000-000000000004"), "Apple", "Monitors", "A bright 27-inch 5K display for your desk, with an integrated camera and speakers.", "/products/monitor.svg", "Studio Display", 1599m },
                    { new Guid("00000000-0000-0000-0000-000000000005"), "Keychron", "Accessories", "A compact wireless mechanical keyboard with a tactile typing feel and a durable aluminum frame.", "/products/keyboard.svg", "Keychron K2", 89m },
                    { new Guid("00000000-0000-0000-0000-000000000006"), "Samsung", "Storage", "1 TB of fast, portable storage in a pocket-sized aluminum enclosure.", "/products/ssd.svg", "Portable SSD T7", 109m }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Products");
        }
    }
}
