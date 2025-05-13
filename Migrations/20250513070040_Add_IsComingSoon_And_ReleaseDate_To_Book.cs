using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BookNook.Migrations
{
    /// <inheritdoc />
    public partial class Add_IsComingSoon_And_ReleaseDate_To_Book : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsComingSoon",
                table: "Books",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReleaseDate",
                table: "Books",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsComingSoon",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "ReleaseDate",
                table: "Books");
        }
    }
}
