using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RollRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTrainingDiary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "game_nodes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technique_id = table.Column<string>(type: "text", nullable: false),
                    label = table.Column<string>(type: "text", nullable: true),
                    side = table.Column<string>(type: "text", nullable: false),
                    hidden = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_game_nodes", x => x.id);
                    table.ForeignKey(
                        name: "fk_game_nodes_techniques_technique_id",
                        column: x => x.technique_id,
                        principalTable: "techniques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_game_nodes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "partners",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    normalized_name = table.Column<string>(type: "text", nullable: false),
                    belt = table.Column<string>(type: "text", nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_partners", x => x.id);
                    table.ForeignKey(
                        name: "fk_partners_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "training_sessions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    attire = table.Column<string>(type: "text", nullable: false),
                    format = table.Column<string>(type: "text", nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    technique_notes = table.Column<string>(type: "text", nullable: true),
                    sleep = table.Column<int>(type: "integer", nullable: true),
                    pain = table.Column<int>(type: "integer", nullable: true),
                    energy = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_training_sessions", x => x.id);
                    table.ForeignKey(
                        name: "fk_training_sessions_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "focus_points",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    origin_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    reviewed_session_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_focus_points", x => x.id);
                    table.ForeignKey(
                        name: "fk_focus_points_training_sessions_origin_session_id",
                        column: x => x.origin_session_id,
                        principalTable: "training_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_focus_points_training_sessions_reviewed_session_id",
                        column: x => x.reviewed_session_id,
                        principalTable: "training_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_focus_points_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "rolls",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    partner_id = table.Column<long>(type: "bigint", nullable: true),
                    partner_belt = table.Column<string>(type: "text", nullable: true),
                    result = table.Column<string>(type: "text", nullable: false),
                    finish_technique_id = table.Column<string>(type: "text", nullable: true),
                    notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_rolls", x => x.id);
                    table.ForeignKey(
                        name: "fk_rolls_partners_partner_id",
                        column: x => x.partner_id,
                        principalTable: "partners",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_rolls_techniques_finish_technique_id",
                        column: x => x.finish_technique_id,
                        principalTable: "techniques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_rolls_training_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "training_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "session_techniques",
                columns: table => new
                {
                    session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    technique_id = table.Column<string>(type: "text", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_session_techniques", x => new { x.session_id, x.technique_id });
                    table.ForeignKey(
                        name: "fk_session_techniques_techniques_technique_id",
                        column: x => x.technique_id,
                        principalTable: "techniques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_session_techniques_training_sessions_session_id",
                        column: x => x.session_id,
                        principalTable: "training_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "roll_steps",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    roll_id = table.Column<long>(type: "bigint", nullable: false),
                    order = table.Column<int>(type: "integer", nullable: false),
                    node_id = table.Column<long>(type: "bigint", nullable: false),
                    outcome = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_roll_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_roll_steps_game_nodes_node_id",
                        column: x => x.node_id,
                        principalTable: "game_nodes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_roll_steps_rolls_roll_id",
                        column: x => x.roll_id,
                        principalTable: "rolls",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_focus_points_origin_session_id",
                table: "focus_points",
                column: "origin_session_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_focus_points_reviewed_session_id",
                table: "focus_points",
                column: "reviewed_session_id");

            migrationBuilder.CreateIndex(
                name: "ix_focus_points_user_id_review_created_at",
                table: "focus_points",
                columns: new[] { "user_id", "review", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_game_nodes_technique_id",
                table: "game_nodes",
                column: "technique_id");

            migrationBuilder.CreateIndex(
                name: "ix_game_nodes_user_id_technique_id_side",
                table: "game_nodes",
                columns: new[] { "user_id", "technique_id", "side" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_partners_user_id_normalized_name",
                table: "partners",
                columns: new[] { "user_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_roll_steps_node_id",
                table: "roll_steps",
                column: "node_id");

            migrationBuilder.CreateIndex(
                name: "ix_roll_steps_roll_id_order",
                table: "roll_steps",
                columns: new[] { "roll_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_rolls_finish_technique_id",
                table: "rolls",
                column: "finish_technique_id");

            migrationBuilder.CreateIndex(
                name: "ix_rolls_partner_id",
                table: "rolls",
                column: "partner_id");

            migrationBuilder.CreateIndex(
                name: "ix_rolls_session_id_order",
                table: "rolls",
                columns: new[] { "session_id", "order" });

            migrationBuilder.CreateIndex(
                name: "ix_session_techniques_technique_id",
                table: "session_techniques",
                column: "technique_id");

            migrationBuilder.CreateIndex(
                name: "ix_training_sessions_user_id_date",
                table: "training_sessions",
                columns: new[] { "user_id", "date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "focus_points");

            migrationBuilder.DropTable(
                name: "roll_steps");

            migrationBuilder.DropTable(
                name: "session_techniques");

            migrationBuilder.DropTable(
                name: "game_nodes");

            migrationBuilder.DropTable(
                name: "rolls");

            migrationBuilder.DropTable(
                name: "partners");

            migrationBuilder.DropTable(
                name: "training_sessions");
        }
    }
}
