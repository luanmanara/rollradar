using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RollRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "channels",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: false),
                    youtube_channel_id = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_channels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "techniques",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    english = table.Column<string>(type: "text", nullable: false),
                    portuguese = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_techniques", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "videos",
                columns: table => new
                {
                    id = table.Column<string>(type: "text", nullable: false),
                    channel_id = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    upload_date = table.Column<DateOnly>(type: "date", nullable: true),
                    duration_seconds = table.Column<int>(type: "integer", nullable: true),
                    view_count = table.Column<long>(type: "bigint", nullable: true),
                    language = table.Column<string>(type: "text", nullable: true),
                    caption_status = table.Column<string>(type: "text", nullable: false, defaultValue: "pendente"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_videos", x => x.id);
                    table.ForeignKey(
                        name: "fk_videos_channels_channel_id",
                        column: x => x.channel_id,
                        principalTable: "channels",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technique_aliases",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    technique_id = table.Column<string>(type: "text", nullable: false),
                    alias = table.Column<string>(type: "text", nullable: false),
                    normalized_alias = table.Column<string>(type: "text", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technique_aliases", x => x.id);
                    table.ForeignKey(
                        name: "fk_technique_aliases_techniques_technique_id",
                        column: x => x.technique_id,
                        principalTable: "techniques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "technique_moments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    video_id = table.Column<string>(type: "text", nullable: false),
                    technique_id = table.Column<string>(type: "text", nullable: false),
                    start_seconds = table.Column<int>(type: "integer", nullable: false),
                    term = table.Column<string>(type: "text", nullable: false),
                    snippet = table.Column<string>(type: "text", nullable: false),
                    curation_status = table.Column<string>(type: "text", nullable: false, defaultValue: "pendente"),
                    curated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_technique_moments", x => x.id);
                    table.ForeignKey(
                        name: "fk_technique_moments_techniques_technique_id",
                        column: x => x.technique_id,
                        principalTable: "techniques",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_technique_moments_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transcript_segments",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    video_id = table.Column<string>(type: "text", nullable: false),
                    start_seconds = table.Column<double>(type: "double precision", nullable: false),
                    duration_seconds = table.Column<double>(type: "double precision", nullable: true),
                    text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transcript_segments", x => x.id);
                    table.ForeignKey(
                        name: "fk_transcript_segments_videos_video_id",
                        column: x => x.video_id,
                        principalTable: "videos",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "moment_votes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    moment_id = table.Column<long>(type: "bigint", nullable: false),
                    voter_id = table.Column<string>(type: "text", nullable: false),
                    ip_hash = table.Column<string>(type: "text", nullable: false),
                    useful = table.Column<bool>(type: "boolean", nullable: false),
                    voted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_moment_votes", x => x.id);
                    table.ForeignKey(
                        name: "fk_moment_votes_technique_moments_moment_id",
                        column: x => x.moment_id,
                        principalTable: "technique_moments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_channels_youtube_channel_id",
                table: "channels",
                column: "youtube_channel_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_moment_votes_moment_id_ip_hash",
                table: "moment_votes",
                columns: new[] { "moment_id", "ip_hash" });

            migrationBuilder.CreateIndex(
                name: "ix_moment_votes_moment_id_voter_id",
                table: "moment_votes",
                columns: new[] { "moment_id", "voter_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technique_aliases_normalized_alias",
                table: "technique_aliases",
                column: "normalized_alias");

            migrationBuilder.CreateIndex(
                name: "ix_technique_aliases_technique_id_normalized_alias",
                table: "technique_aliases",
                columns: new[] { "technique_id", "normalized_alias" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_technique_moments_technique_id",
                table: "technique_moments",
                column: "technique_id");

            migrationBuilder.CreateIndex(
                name: "ix_technique_moments_video_id_technique_id_start_seconds",
                table: "technique_moments",
                columns: new[] { "video_id", "technique_id", "start_seconds" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transcript_segments_video_id_start_seconds",
                table: "transcript_segments",
                columns: new[] { "video_id", "start_seconds" });

            migrationBuilder.CreateIndex(
                name: "ix_videos_channel_id_upload_date",
                table: "videos",
                columns: new[] { "channel_id", "upload_date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "moment_votes");

            migrationBuilder.DropTable(
                name: "technique_aliases");

            migrationBuilder.DropTable(
                name: "transcript_segments");

            migrationBuilder.DropTable(
                name: "technique_moments");

            migrationBuilder.DropTable(
                name: "techniques");

            migrationBuilder.DropTable(
                name: "videos");

            migrationBuilder.DropTable(
                name: "channels");
        }
    }
}
