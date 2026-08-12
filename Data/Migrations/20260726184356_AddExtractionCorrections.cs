using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KiefProductions.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddExtractionCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExtractionCorrections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorDocumentId = table.Column<int>(type: "int", nullable: true),
                    FieldName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExtractedValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CorrectedValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExtractionCorrections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExtractionCorrections_VendorDocuments_VendorDocumentId",
                        column: x => x.VendorDocumentId,
                        principalTable: "VendorDocuments",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExtractionCorrections_VendorDocumentId",
                table: "ExtractionCorrections",
                column: "VendorDocumentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExtractionCorrections");
        }
    }
}
