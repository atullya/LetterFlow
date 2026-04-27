using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LetterTemplatePractice.Migrations
{
    /// <inheritdoc />
    public partial class ResetAppLogsSequence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reset the AppLogs Id sequence to be above the current max row Id.
            // Fixes duplicate PK errors caused by the sequence falling behind
            // after rows were inserted with manually-assigned Ids.
            migrationBuilder.Sql(@"
                SELECT setval(
                    pg_get_serial_sequence('""AppLogs""', 'Id'),
                    COALESCE((SELECT MAX(""Id"") FROM ""AppLogs""), 0) + 1,
                    false
                );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
