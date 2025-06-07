using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schnauzer.Migrations
{
    /// <inheritdoc />
    public partial class AddChannelAlternateKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddUniqueConstraint(
                name: "AK_Channels_OwnerId_GuildId",
                table: "Channels",
                columns: new[] { "OwnerId", "GuildId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropUniqueConstraint(
                name: "AK_Channels_OwnerId_GuildId",
                table: "Channels");
        }
    }
}
