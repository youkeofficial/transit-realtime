using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TransitRealtime.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSubscriberSecret : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubscriberSecret",
                table: "TransitServices",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubscriberSecret",
                table: "TransitServices");
        }
    }
}
