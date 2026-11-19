using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RollRadar.Api.Data.Migrations
{
    /// <summary>
    /// Game map (RR-014): arrows between positions (game_edges), videos pinned to them (map_videos), node coordinates and
    /// the transition of detailed roll steps. Submissions already recorded with position and technique become arrows.
    /// </summary>
    public partial class AddGameMap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "edge_id",
                table: "roll_steps",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "edge_succeeded",
                table: "roll_steps",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "x",
                table: "game_nodes",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "y",
                table: "game_nodes",
                type: "double precision",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "game_edges",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_node_id = table.Column<long>(type: "bigint", nullable: false),
                    to_node_id = table.Column<long>(type: "bigint", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    technique_id = table.Column<string>(type: "text", nullable: true),
                    label = table.Column<string>(type: "text", nullable: true),
                    against_me = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_edges", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_edges_game_nodes_from_node_id",
                        column: x => x.from_node_id,
                        principalTable: "game_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_edges_game_nodes_to_node_id",
                        column: x => x.to_node_id,
                        principalTable: "game_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_game_edges_techniques_technique_id",
                        column: x => x.technique_id,
                        principalTable: "techniques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_game_edges_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "map_videos",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    node_id = table.Column<long>(type: "bigint", nullable: true),
                    edge_id = table.Column<long>(type: "bigint", nullable: true),
                    video_id = table.Column<string>(type: "text", nullable: false),
                    start_seconds = table.Column<int>(type: "integer", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_map_videos", x => x.id);
                    table.CheckConstraint("ck_map_videos_one_target", "num_nonnulls(node_id, edge_id) = 1");
                    table.ForeignKey(
                        name: "fk_map_videos_game_edges_edge_id",
                        column: x => x.edge_id,
                        principalTable: "game_edges",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_map_videos_game_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "game_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_map_videos_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_map_videos_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_roll_steps_edge_id",
                table: "roll_steps",
                column: "edge_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_edges_from_node_id",
                table: "game_edges",
                column: "from_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_edges_technique_id",
                table: "game_edges",
                column: "technique_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_edges_to_node_id",
                table: "game_edges",
                column: "to_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_edges_user_id_from_node_id_to_node_id_kind_technique_i",
                table: "game_edges",
                columns: new[] { "user_id", "from_node_id", "to_node_id", "kind", "technique_id", "against_me" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_map_videos_edge_id",
                table: "map_videos",
                column: "edge_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_videos_node_id",
                table: "map_videos",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_videos_user_id",
                table: "map_videos",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_map_videos_video_id",
                table: "map_videos",
                column: "video_id");

            migrationBuilder.AddForeignKey(
                name: "fk_roll_steps_game_edges_edge_id",
                table: "roll_steps",
                column: "edge_id",
                principalTable: "game_edges",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            // Submissions already recorded with position and technique become the map's submission arrows, as new ones do.
            migrationBuilder.Sql("""
                INSERT INTO game_edges (user_id, from_node_id, to_node_id, kind, technique_id, against_me, created_at)
                SELECT DISTINCT s.user_id, f.node_id, NULL::bigint, 'finalizacao', f.technique_id, NOT f.by_me, now()
                FROM roll_finishes f
                JOIN rolls r ON r.id = f.roll_id
                JOIN training_sessions s ON s.id = r.session_id
                WHERE f.node_id IS NOT NULL AND f.technique_id IS NOT NULL
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_roll_steps_game_edges_edge_id",
                table: "roll_steps");

            migrationBuilder.DropTable(
                name: "map_videos");

            migrationBuilder.DropTable(
                name: "game_edges");

            migrationBuilder.DropIndex(
                name: "ix_roll_steps_edge_id",
                table: "roll_steps");

            migrationBuilder.DropColumn(
                name: "edge_id",
                table: "roll_steps");

            migrationBuilder.DropColumn(
                name: "edge_succeeded",
                table: "roll_steps");

            migrationBuilder.DropColumn(
                name: "x",
                table: "game_nodes");

            migrationBuilder.DropColumn(
                name: "y",
                table: "game_nodes");
        }
    }
}
