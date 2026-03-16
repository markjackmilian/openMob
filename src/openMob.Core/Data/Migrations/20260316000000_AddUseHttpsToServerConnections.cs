using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace openMob.Core.Data.Migrations;

/// <inheritdoc />
public partial class AddUseHttpsToServerConnections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "UseHttps",
            table: "ServerConnections",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "UseHttps",
            table: "ServerConnections");
    }
}
