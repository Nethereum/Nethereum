using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nethereum.BlockchainStore.Postgres.Migrations
{
    public partial class AddBlockAccessListAndAuthorizationList : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "blockaccesslisthash",
                table: "Blocks",
                type: "character varying(67)",
                maxLength: 67,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "slotnumber",
                table: "Blocks",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "authorizationlist",
                table: "Transactions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "blocktimestamp",
                table: "TransactionLogs",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "BlockAccessListAccounts",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    blocknumber = table.Column<long>(type: "bigint", nullable: false),
                    blockhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    address = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    iscanonical = table.Column<bool>(type: "boolean", nullable: false),
                    storagereads = table.Column<string>(type: "text", nullable: true),
                    storagechanges = table.Column<string>(type: "text", nullable: true),
                    balancechanges = table.Column<string>(type: "text", nullable: true),
                    noncechanges = table.Column<string>(type: "text", nullable: true),
                    codechanges = table.Column<string>(type: "text", nullable: true),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blockaccesslistaccounts", x => x.rowindex);
                });

            migrationBuilder.CreateIndex(
                name: "ix_blockaccesslistaccounts_address",
                table: "BlockAccessListAccounts",
                column: "address");

            migrationBuilder.CreateIndex(
                name: "ix_blockaccesslistaccounts_blocknumber_address",
                table: "BlockAccessListAccounts",
                columns: new[] { "blocknumber", "address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_blockaccesslistaccounts_iscanonical_blocknumber",
                table: "BlockAccessListAccounts",
                columns: new[] { "iscanonical", "blocknumber" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlockAccessListAccounts");

            migrationBuilder.DropColumn(
                name: "authorizationlist",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "blocktimestamp",
                table: "TransactionLogs");

            migrationBuilder.DropColumn(
                name: "blockaccesslisthash",
                table: "Blocks");

            migrationBuilder.DropColumn(
                name: "slotnumber",
                table: "Blocks");
        }
    }
}
