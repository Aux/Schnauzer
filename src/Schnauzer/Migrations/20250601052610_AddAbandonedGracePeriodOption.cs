using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schnauzer.Migrations
{
    /// <inheritdoc />
    public partial class AddAbandonedGracePeriodOption : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeSpan>(
                name: "AbandonedGracePeriod",
                table: "Guilds",
                type: "interval",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AbandonedGracePeriod",
                table: "Guilds");
        }
    }
}
