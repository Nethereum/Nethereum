using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nethereum.Mud.Repositories.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "mud_blockprogress",
                columns: table => new
                {
                    RowIndex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastBlockProcessed = table.Column<long>(type: "bigint", nullable: false),
                    RowCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mud_blockprogress", x => x.RowIndex);
                });

            migrationBuilder.CreateTable(
                name: "mud_chainstates",
                columns: table => new
                {
                    RowIndex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LastCanonicalBlockNumber = table.Column<long>(type: "bigint", nullable: true),
                    LastCanonicalBlockHash = table.Column<string>(type: "text", nullable: true),
                    FinalizedBlockNumber = table.Column<long>(type: "bigint", nullable: true),
                    ChainId = table.Column<int>(type: "integer", nullable: true),
                    RowCreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RowUpdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mud_chainstates", x => x.RowIndex);
                });

            migrationBuilder.CreateTable(
                name: "StoredRecords",
                columns: table => new
                {
                    tableid = table.Column<byte[]>(type: "bytea", nullable: false),
                    key = table.Column<byte[]>(type: "bytea", nullable: false),
                    address = table.Column<byte[]>(type: "bytea", nullable: false),
                    RowId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    key0 = table.Column<byte[]>(type: "bytea", nullable: true),
                    key1 = table.Column<byte[]>(type: "bytea", nullable: true),
                    key2 = table.Column<byte[]>(type: "bytea", nullable: true),
                    key3 = table.Column<byte[]>(type: "bytea", nullable: true),
                    BlockNumber = table.Column<decimal>(type: "numeric(1000,0)", nullable: true),
                    LogIndex = table.Column<int>(type: "integer", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    static_data = table.Column<byte[]>(type: "bytea", nullable: true),
                    encoded_lengths = table.Column<byte[]>(type: "bytea", nullable: true),
                    dynamic_data = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredRecords", x => new { x.address, x.tableid, x.key });
                });

            migrationBuilder.CreateIndex(
                name: "IX_MudChainStates_LastCanonicalBlockNumber",
                table: "mud_chainstates",
                column: "LastCanonicalBlockNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Address_TableId_Key0",
                table: "StoredRecords",
                columns: new[] { "address", "tableid", "key0" });

            migrationBuilder.CreateIndex(
                name: "IX_Address_TableId_Key1",
                table: "StoredRecords",
                columns: new[] { "address", "tableid", "key1" });

            migrationBuilder.CreateIndex(
                name: "IX_Address_TableId_Key2",
                table: "StoredRecords",
                columns: new[] { "address", "tableid", "key2" });

            migrationBuilder.CreateIndex(
                name: "IX_Address_TableId_Key3",
                table: "StoredRecords",
                columns: new[] { "address", "tableid", "key3" });

            migrationBuilder.CreateIndex(
                name: "IX_RowId",
                table: "StoredRecords",
                column: "RowId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "mud_blockprogress");

            migrationBuilder.DropTable(
                name: "mud_chainstates");

            migrationBuilder.DropTable(
                name: "StoredRecords");
        }
    }
}
