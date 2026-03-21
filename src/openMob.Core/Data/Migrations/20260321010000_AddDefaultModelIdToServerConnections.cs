using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace openMob.Core.Data.Migrations;

/// <inheritdoc />
public partial class AddDefaultModelIdToServerConnections : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "DefaultModelId",
            table: "ServerConnections",
            type: "TEXT",
            maxLength: 500,
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DefaultModelId",
            table: "ServerConnections");
    }
}
