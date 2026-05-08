using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LetterTemplatePractice.Migrations
{
    /// <inheritdoc />
    public partial class AddGoogleOAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Drop old legacy tables only if they exist (safe for both fresh and existing DBs)
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""FollowUps"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""GeneratedLetters"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Guarantors"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Loans"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Customers"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""Templates"" CASCADE;");
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""AppLogs"" CASCADE;");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "GoogleId",
                table: "Users",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_GoogleId",
                table: "Users",
                column: "GoogleId",
                unique: true,
                filter: "\"GoogleId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_GoogleId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "GoogleId",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                table: "Users",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200,
                oldNullable: true);
        }
    }
}
