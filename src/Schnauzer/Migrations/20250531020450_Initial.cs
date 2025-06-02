using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Schnauzer.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Guilds",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsBanned = table.Column<bool>(type: "boolean", nullable: true),
                    PreferredLocale = table.Column<string>(type: "text", nullable: true),
                    CreateChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    CanOwnRoleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true),
                    DenyDeafenedOwnership = table.Column<bool>(type: "boolean", nullable: true),
                    DenyMutedOwnership = table.Column<bool>(type: "boolean", nullable: true),
                    DefaultLobbySize = table.Column<int>(type: "integer", nullable: true),
                    MaxLobbySize = table.Column<int>(type: "integer", nullable: true),
                    MaxLobbyCount = table.Column<int>(type: "integer", nullable: true),
                    IsAutoModEnabled = table.Column<bool>(type: "boolean", nullable: true),
                    AutoModLogChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    AutomodRuleIds = table.Column<decimal[]>(type: "numeric(20,0)[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guilds", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    IsBanned = table.Column<bool>(type: "boolean", nullable: true),
                    PreferredLocale = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Channels",
                columns: table => new
                {
                    Id = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    CreatorId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    OwnerId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    PanelMessageId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    PreferredLocale = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Channels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Channels_Guilds_GuildId",
                        column: x => x.GuildId,
                        principalTable: "Guilds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Channels_GuildId",
                table: "Channels",
                column: "GuildId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Channels");

            migrationBuilder.DropTable(
                name: "Users");

            migrationBuilder.DropTable(
                name: "Guilds");
        }
    }
}
