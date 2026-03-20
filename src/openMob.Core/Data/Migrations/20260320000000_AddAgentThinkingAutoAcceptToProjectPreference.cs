using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace openMob.Core.Data.Migrations;

/// <inheritdoc />
public partial class AddAgentThinkingAutoAcceptToProjectPreference : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "AgentName",
            table: "ProjectPreferences",
            type: "TEXT",
            maxLength: 500,
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "ThinkingLevel",
            table: "ProjectPreferences",
            type: "INTEGER",
            nullable: false,
            defaultValue: 1);

        migrationBuilder.AddColumn<bool>(
            name: "AutoAccept",
            table: "ProjectPreferences",
            type: "INTEGER",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "AgentName",
            table: "ProjectPreferences");

        migrationBuilder.DropColumn(
            name: "ThinkingLevel",
            table: "ProjectPreferences");

        migrationBuilder.DropColumn(
            name: "AutoAccept",
            table: "ProjectPreferences");
    }
}
