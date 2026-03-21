using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace openMob.Core.Data.Migrations;

/// <inheritdoc />
public partial class AddAppStateTable : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AppStates",
            columns: table => new
            {
                Key = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AppStates", x => x.Key);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "AppStates");
    }
}
