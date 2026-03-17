using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace openMob.Core.Data.Migrations;

/// <inheritdoc />
public partial class AddProjectPreference : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProjectPreferences",
            columns: table => new
            {
                ProjectId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                DefaultModelId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectPreferences", x => x.ProjectId);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProjectPreferences");
    }
}
