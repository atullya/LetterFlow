using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace LetterTemplatePractice.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogChunksWithVectors : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlogChunks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PostId = table.Column<int>(type: "integer", nullable: false),
                    Content = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    ChunkIndex = table.Column<int>(type: "integer", nullable: false),
                    AuthorName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostTitle = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PostSlug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Embedding = table.Column<float[]>(type: "real[]", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogChunks", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BlogChunks_PostId",
                table: "BlogChunks",
                column: "PostId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlogChunks");
        }
    }
}
