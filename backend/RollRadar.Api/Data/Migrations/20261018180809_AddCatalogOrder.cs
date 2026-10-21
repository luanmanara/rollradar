using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RollRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCatalogOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_technique_aliases_technique_id_normalized_alias",
                table: "technique_aliases");

            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "techniques",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "position",
                table: "technique_aliases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "ix_technique_aliases_technique_id_kind_normalized_alias",
                table: "technique_aliases",
                columns: new[] { "technique_id", "kind", "normalized_alias" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_technique_aliases_technique_id_kind_normalized_alias",
                table: "technique_aliases");

            migrationBuilder.DropColumn(
                name: "position",
                table: "techniques");

            migrationBuilder.DropColumn(
                name: "position",
                table: "technique_aliases");

            migrationBuilder.CreateIndex(
                name: "ix_technique_aliases_technique_id_normalized_alias",
                table: "technique_aliases",
                columns: new[] { "technique_id", "normalized_alias" },
                unique: true);
        }
    }
}
