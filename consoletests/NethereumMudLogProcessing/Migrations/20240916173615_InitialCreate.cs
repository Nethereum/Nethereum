using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace NethereumMudLogProcessing.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "blockprogress",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lastblockprocessed = table.Column<string>(type: "text", nullable: true),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blockprogress", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "storedrecords",
                columns: table => new
                {
                    tableid = table.Column<byte[]>(type: "bytea", nullable: false),
                    key = table.Column<byte[]>(type: "bytea", nullable: false),
                    address = table.Column<byte[]>(type: "bytea", nullable: false),
                    key0 = table.Column<byte[]>(type: "bytea", nullable: true),
                    key1 = table.Column<byte[]>(type: "bytea", nullable: true),
                    key2 = table.Column<byte[]>(type: "bytea", nullable: true),
                    key3 = table.Column<byte[]>(type: "bytea", nullable: true),
                    blocknumber = table.Column<decimal>(type: "numeric(1000,0)", nullable: true),
                    logindex = table.Column<int>(type: "integer", nullable: true),
                    isdeleted = table.Column<bool>(type: "boolean", nullable: false),
                    static_data = table.Column<byte[]>(type: "bytea", nullable: true),
                    encoded_lengths = table.Column<byte[]>(type: "bytea", nullable: true),
                    dynamic_data = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_storedrecords", x => new { x.address, x.tableid, x.key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_Address_TableId_Key0",
                table: "storedrecords",
                columns: new[] { "address", "tableid", "key0" });

            migrationBuilder.CreateIndex(
                name: "IX_Address_TableId_Key1",
                table: "storedrecords",
                columns: new[] { "address", "tableid", "key1" });

            migrationBuilder.CreateIndex(
                name: "IX_Address_TableId_Key2",
                table: "storedrecords",
                columns: new[] { "address", "tableid", "key2" });

            migrationBuilder.CreateIndex(
                name: "IX_Address_TableId_Key3",
                table: "storedrecords",
                columns: new[] { "address", "tableid", "key3" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "blockprogress");

            migrationBuilder.DropTable(
                name: "storedrecords");
        }
    }
}
