using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nethereum.BlockchainStorage.Token.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "balanceaggregationprogress",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lastprocessedrowindex = table.Column<long>(type: "bigint", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_balanceaggregationprogress", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "denormalizerprogress",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lastprocessedrowindex = table.Column<long>(type: "bigint", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_denormalizerprogress", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "nftinventory",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    address = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    contractaddress = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    tokenid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    amount = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tokentype = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    lastupdatedblocknumber = table.Column<long>(type: "bigint", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_nftinventory", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "tokenbalances",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    address = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    contractaddress = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    balance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tokentype = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    lastupdatedblocknumber = table.Column<long>(type: "bigint", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokenbalances", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "TokenBlockProgress",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lastblockprocessed = table.Column<long>(type: "bigint", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokenblockprogress", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "tokenmetadata",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    contractaddress = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    symbol = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    decimals = table.Column<int>(type: "integer", nullable: false),
                    tokentype = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokenmetadata", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "tokentransferlogs",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transactionhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    logindex = table.Column<long>(type: "bigint", nullable: false),
                    blocknumber = table.Column<long>(type: "bigint", nullable: false),
                    blockhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    contractaddress = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    eventhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    fromaddress = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    toaddress = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    amount = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    tokenid = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    operatoraddress = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    tokentype = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    iscanonical = table.Column<bool>(type: "boolean", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tokentransferlogs", x => x.rowindex);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BalanceAggregationProgress_LastProcessedRowIndex",
                table: "balanceaggregationprogress",
                column: "lastprocessedrowindex");

            migrationBuilder.CreateIndex(
                name: "IX_DenormalizerProgress_LastProcessedRowIndex",
                table: "denormalizerprogress",
                column: "lastprocessedrowindex");

            migrationBuilder.CreateIndex(
                name: "IX_NFTInventory_Address",
                table: "nftinventory",
                column: "address");

            migrationBuilder.CreateIndex(
                name: "IX_NFTInventory_Address_Contract_TokenId",
                table: "nftinventory",
                columns: new[] { "address", "contractaddress", "tokenid" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NFTInventory_Contract_TokenId",
                table: "nftinventory",
                columns: new[] { "contractaddress", "tokenid" });

            migrationBuilder.CreateIndex(
                name: "IX_TokenBalances_Address_Contract",
                table: "tokenbalances",
                columns: new[] { "address", "contractaddress" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenBalances_ContractAddress",
                table: "tokenbalances",
                column: "contractaddress");

            migrationBuilder.CreateIndex(
                name: "IX_TokenBlockProgress_LastBlockProcessed",
                table: "TokenBlockProgress",
                column: "lastblockprocessed");

            migrationBuilder.CreateIndex(
                name: "IX_TokenMetadata_ContractAddress",
                table: "tokenmetadata",
                column: "contractaddress",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransferLogs_BlockNumber",
                table: "tokentransferlogs",
                column: "blocknumber");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransferLogs_ContractAddress",
                table: "tokentransferlogs",
                column: "contractaddress");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransferLogs_FromAddress",
                table: "tokentransferlogs",
                column: "fromaddress");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransferLogs_IsCanonical",
                table: "tokentransferlogs",
                column: "iscanonical");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransferLogs_ToAddress",
                table: "tokentransferlogs",
                column: "toaddress");

            migrationBuilder.CreateIndex(
                name: "IX_TokenTransferLogs_TxHash_LogIndex",
                table: "tokentransferlogs",
                columns: new[] { "transactionhash", "logindex" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "balanceaggregationprogress");

            migrationBuilder.DropTable(
                name: "denormalizerprogress");

            migrationBuilder.DropTable(
                name: "nftinventory");

            migrationBuilder.DropTable(
                name: "tokenbalances");

            migrationBuilder.DropTable(
                name: "TokenBlockProgress");

            migrationBuilder.DropTable(
                name: "tokenmetadata");

            migrationBuilder.DropTable(
                name: "tokentransferlogs");
        }
    }
}
