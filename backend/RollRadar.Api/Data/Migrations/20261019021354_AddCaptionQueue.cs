using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RollRadar.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaptionQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "caption_checked_at",
                table: "videos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "caption_error",
                table: "videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "event",
                table: "videos",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "url",
                table: "channels",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_videos_caption_status",
                table: "videos",
                column: "caption_status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_videos_caption_status",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "caption_checked_at",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "caption_error",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "event",
                table: "videos");

            migrationBuilder.DropColumn(
                name: "url",
                table: "channels");
        }
    }
}
