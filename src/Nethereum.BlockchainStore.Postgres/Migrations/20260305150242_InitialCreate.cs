using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Nethereum.BlockchainStore.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AccountStates",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    address = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: false),
                    balance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    nonce = table.Column<long>(type: "bigint", nullable: false),
                    iscontract = table.Column<bool>(type: "boolean", nullable: false),
                    lastupdatedblock = table.Column<long>(type: "bigint", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accountstates", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "AddressTransactions",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    blocknumber = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    address = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_addresstransactions", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "BlockProgress",
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
                    table.PrimaryKey("pk_blockprogress", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "Blocks",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    blocknumber = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    parenthash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    nonce = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    extradata = table.Column<string>(type: "text", nullable: true),
                    difficulty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    totaldifficulty = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    size = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    miner = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    gaslimit = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gasused = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    timestamp = table.Column<long>(type: "bigint", nullable: false),
                    iscanonical = table.Column<bool>(type: "boolean", nullable: false),
                    isfinalized = table.Column<bool>(type: "boolean", nullable: false),
                    chainid = table.Column<int>(type: "integer", nullable: true),
                    transactioncount = table.Column<long>(type: "bigint", nullable: false),
                    basefeepergas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    stateroot = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    receiptsroot = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    logsbloom = table.Column<string>(type: "text", nullable: true),
                    withdrawalsroot = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    blobgasused = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    excessblobgas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    parentbeaconblockroot = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    requestshash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    transactionsroot = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    mixhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    sha3uncles = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_blocks", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "ChainStates",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    lastcanonicalblocknumber = table.Column<long>(type: "bigint", nullable: true),
                    lastcanonicalblockhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    finalizedblocknumber = table.Column<long>(type: "bigint", nullable: true),
                    chainid = table.Column<int>(type: "integer", nullable: true),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_chainstates", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "Contracts",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    address = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    abi = table.Column<string>(type: "text", nullable: true),
                    code = table.Column<string>(type: "text", nullable: true),
                    creator = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    transactionhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_contracts", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "InternalTransactionBlockProgress",
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
                    table.PrimaryKey("pk_internaltransactionblockprogress", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "InternalTransactions",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transactionhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    blocknumber = table.Column<long>(type: "bigint", nullable: false),
                    blockhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    traceindex = table.Column<int>(type: "integer", nullable: false),
                    depth = table.Column<int>(type: "integer", nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    addressfrom = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    addressto = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gasused = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    input = table.Column<string>(type: "text", nullable: true),
                    output = table.Column<string>(type: "text", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true),
                    revertreason = table.Column<string>(type: "text", nullable: true),
                    iscanonical = table.Column<bool>(type: "boolean", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_internaltransactions", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "TransactionLogs",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    transactionhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    logindex = table.Column<long>(type: "bigint", nullable: false),
                    address = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    eventhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    indexval1 = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    indexval2 = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    indexval3 = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    data = table.Column<string>(type: "text", nullable: true),
                    blocknumber = table.Column<long>(type: "bigint", nullable: false),
                    blockhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    iscanonical = table.Column<bool>(type: "boolean", nullable: false),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactionlogs", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "TransactionLogVmStacks",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    address = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    transactionhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    structlogs = table.Column<string>(type: "text", nullable: true),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactionlogvmstacks", x => x.rowindex);
                });

            migrationBuilder.CreateTable(
                name: "Transactions",
                columns: table => new
                {
                    rowindex = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    rowcreated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rowupdated = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    blockhash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    blocknumber = table.Column<long>(type: "bigint", nullable: false),
                    hash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: false),
                    addressfrom = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    timestamp = table.Column<long>(type: "bigint", nullable: false),
                    transactionindex = table.Column<long>(type: "bigint", nullable: false),
                    value = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    addressto = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    gas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    gasprice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    input = table.Column<string>(type: "text", nullable: true),
                    nonce = table.Column<long>(type: "bigint", nullable: false),
                    failed = table.Column<bool>(type: "boolean", nullable: false),
                    receipthash = table.Column<string>(type: "character varying(67)", maxLength: 67, nullable: true),
                    gasused = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    cumulativegasused = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    effectivegasprice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    haslog = table.Column<bool>(type: "boolean", nullable: false),
                    error = table.Column<string>(type: "text", nullable: true),
                    hasvmstack = table.Column<bool>(type: "boolean", nullable: false),
                    newcontractaddress = table.Column<string>(type: "character varying(43)", maxLength: 43, nullable: true),
                    failedcreatecontract = table.Column<bool>(type: "boolean", nullable: false),
                    maxfeepergas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    maxpriorityfeepergas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    transactiontype = table.Column<long>(type: "bigint", nullable: false),
                    revertreason = table.Column<string>(type: "text", nullable: true),
                    iscanonical = table.Column<bool>(type: "boolean", nullable: false),
                    maxfeeperblobgas = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    blobgasused = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    blobgasprice = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.rowindex);
                });

            migrationBuilder.CreateIndex(
                name: "ix_accountstates_address",
                table: "AccountStates",
                column: "address",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accountstates_lastupdatedblock",
                table: "AccountStates",
                column: "lastupdatedblock");

            migrationBuilder.CreateIndex(
                name: "ix_addresstransactions_address",
                table: "AddressTransactions",
                column: "address");

            migrationBuilder.CreateIndex(
                name: "ix_addresstransactions_address_blocknumber",
                table: "AddressTransactions",
                columns: new[] { "address", "blocknumber" });

            migrationBuilder.CreateIndex(
                name: "ix_addresstransactions_blocknumber_hash_address",
                table: "AddressTransactions",
                columns: new[] { "blocknumber", "hash", "address" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_addresstransactions_hash",
                table: "AddressTransactions",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_blockprogress_lastblockprocessed",
                table: "BlockProgress",
                column: "lastblockprocessed");

            migrationBuilder.CreateIndex(
                name: "ix_blocks_blocknumber",
                table: "Blocks",
                column: "blocknumber");

            migrationBuilder.CreateIndex(
                name: "ix_blocks_blocknumber_hash",
                table: "Blocks",
                columns: new[] { "blocknumber", "hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_blocks_hash",
                table: "Blocks",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_blocks_iscanonical_blocknumber",
                table: "Blocks",
                columns: new[] { "iscanonical", "blocknumber" });

            migrationBuilder.CreateIndex(
                name: "ix_blocks_parenthash",
                table: "Blocks",
                column: "parenthash");

            migrationBuilder.CreateIndex(
                name: "ix_chainstates_lastcanonicalblocknumber",
                table: "ChainStates",
                column: "lastcanonicalblocknumber");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_address",
                table: "Contracts",
                column: "address");

            migrationBuilder.CreateIndex(
                name: "ix_contracts_name",
                table: "Contracts",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_internaltransactionblockprogress_lastblockprocessed",
                table: "InternalTransactionBlockProgress",
                column: "lastblockprocessed");

            migrationBuilder.CreateIndex(
                name: "ix_internaltransactions_addressfrom",
                table: "InternalTransactions",
                column: "addressfrom");

            migrationBuilder.CreateIndex(
                name: "ix_internaltransactions_addressto",
                table: "InternalTransactions",
                column: "addressto");

            migrationBuilder.CreateIndex(
                name: "ix_internaltransactions_blocknumber",
                table: "InternalTransactions",
                column: "blocknumber");

            migrationBuilder.CreateIndex(
                name: "ix_internaltransactions_iscanonical_blocknumber",
                table: "InternalTransactions",
                columns: new[] { "iscanonical", "blocknumber" });

            migrationBuilder.CreateIndex(
                name: "ix_internaltransactions_transactionhash_traceindex",
                table: "InternalTransactions",
                columns: new[] { "transactionhash", "traceindex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogs_address",
                table: "TransactionLogs",
                column: "address");

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogs_blocknumber",
                table: "TransactionLogs",
                column: "blocknumber");

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogs_eventhash",
                table: "TransactionLogs",
                column: "eventhash");

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogs_indexval1",
                table: "TransactionLogs",
                column: "indexval1");

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogs_indexval2",
                table: "TransactionLogs",
                column: "indexval2");

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogs_indexval3",
                table: "TransactionLogs",
                column: "indexval3");

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogs_iscanonical_blocknumber",
                table: "TransactionLogs",
                columns: new[] { "iscanonical", "blocknumber" });

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogs_transactionhash_logindex",
                table: "TransactionLogs",
                columns: new[] { "transactionhash", "logindex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogvmstacks_address",
                table: "TransactionLogVmStacks",
                column: "address");

            migrationBuilder.CreateIndex(
                name: "ix_transactionlogvmstacks_transactionhash",
                table: "TransactionLogVmStacks",
                column: "transactionhash");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_addressfrom",
                table: "Transactions",
                column: "addressfrom");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_addressto",
                table: "Transactions",
                column: "addressto");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_blocknumber_hash",
                table: "Transactions",
                columns: new[] { "blocknumber", "hash" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_hash",
                table: "Transactions",
                column: "hash");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_iscanonical_blocknumber",
                table: "Transactions",
                columns: new[] { "iscanonical", "blocknumber" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_newcontractaddress",
                table: "Transactions",
                column: "newcontractaddress");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountStates");

            migrationBuilder.DropTable(
                name: "AddressTransactions");

            migrationBuilder.DropTable(
                name: "BlockProgress");

            migrationBuilder.DropTable(
                name: "Blocks");

            migrationBuilder.DropTable(
                name: "ChainStates");

            migrationBuilder.DropTable(
                name: "Contracts");

            migrationBuilder.DropTable(
                name: "InternalTransactionBlockProgress");

            migrationBuilder.DropTable(
                name: "InternalTransactions");

            migrationBuilder.DropTable(
                name: "TransactionLogs");

            migrationBuilder.DropTable(
                name: "TransactionLogVmStacks");

            migrationBuilder.DropTable(
                name: "Transactions");
        }
    }
}
