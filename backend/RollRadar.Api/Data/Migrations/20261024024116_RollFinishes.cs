using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RollRadar.Api.Data.Migrations
{
    /// <summary>
    /// Rolls are timed, so one can have several submissions either way: the single result of a roll moves to
    /// roll_finishes (one row per submission). Existing rolls are converted before the old columns go: "finalizei" and
    /// "fui_finalizado" become one row with the technique they had, "nenhum" becomes no rows.
    /// </summary>
    public partial class RollFinishes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "duration_minutes",
                table: "rolls",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "roll_finishes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    roll_id = table.Column<long>(type: "bigint", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    by_me = table.Column<bool>(type: "boolean", nullable: false),
                    technique_id = table.Column<string>(type: "text", nullable: true),
                    node_id = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roll_finishes", x => x.id);
                    table.ForeignKey(
                        name: "fk_roll_finishes_game_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "game_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_roll_finishes_rolls_roll_id",
                        column: x => x.roll_id,
                        principalTable: "rolls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_roll_finishes_techniques_technique_id",
                        column: x => x.technique_id,
                        principalTable: "techniques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO roll_finishes (roll_id, "order", by_me, technique_id)
                SELECT id, 0, result = 'finalizei', finish_technique_id
                FROM rolls
                WHERE result IN ('finalizei', 'fui_finalizado');
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_rolls_techniques_finish_technique_id",
                table: "rolls");

            migrationBuilder.DropIndex(
                name: "ix_rolls_finish_technique_id",
                table: "rolls");

            migrationBuilder.DropColumn(
                name: "finish_technique_id",
                table: "rolls");

            migrationBuilder.DropColumn(
                name: "result",
                table: "rolls");

            migrationBuilder.CreateIndex(
                name: "ix_roll_finishes_node_id",
                table: "roll_finishes",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_roll_finishes_roll_id_order",
                table: "roll_finishes",
                columns: new[] { "roll_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_roll_finishes_technique_id",
                table: "roll_finishes",
                column: "technique_id");
        }

        /// <summary>Back to one result per roll: the first submission wins; more than one can't be kept.</summary>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "finish_technique_id",
                table: "rolls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "result",
                table: "rolls",
                type: "text",
                nullable: false,
                defaultValue: "nenhum");

            migrationBuilder.Sql("""
                UPDATE rolls r
                SET result = CASE WHEN f.by_me THEN 'finalizei' ELSE 'fui_finalizado' END,
                    finish_technique_id = f.technique_id
                FROM (SELECT DISTINCT ON (roll_id) roll_id, by_me, technique_id
                      FROM roll_finishes ORDER BY roll_id, "order") f
                WHERE f.roll_id = r.id;
                """);

            migrationBuilder.DropTable(
                name: "roll_finishes");

            migrationBuilder.DropColumn(
                name: "duration_minutes",
                table: "rolls");

            migrationBuilder.CreateIndex(
                name: "ix_rolls_finish_technique_id",
                table: "rolls",
                column: "finish_technique_id");

            migrationBuilder.AddForeignKey(
                name: "fk_rolls_techniques_finish_technique_id",
                table: "rolls",
                column: "finish_technique_id",
                principalTable: "techniques",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
