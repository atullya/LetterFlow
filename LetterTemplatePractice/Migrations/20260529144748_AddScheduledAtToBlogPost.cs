using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetterTemplatePractice.Migrations
{
    /// <inheritdoc />
    public partial class AddScheduledAtToBlogPost : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScheduledAt",
                table: "BlogPosts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_BlogPosts_IsPublished_ScheduledAt",
                table: "BlogPosts",
                columns: new[] { "IsPublished", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BlogPosts_IsPublished_ScheduledAt",
                table: "BlogPosts");

            migrationBuilder.DropColumn(
                name: "ScheduledAt",
                table: "BlogPosts");
        }
    }
}
