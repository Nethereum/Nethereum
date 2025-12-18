using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Circles.RPC.Requests.DTOs;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Circles.RPC.Requests
{
    
    public class GetTransactionHistoryQuery
    {
        private readonly CirclesQuery<TransactionHistoryRow> _circlesQuery;

        public GetTransactionHistoryQuery(IClient client)
        {
            if (client == null) throw new ArgumentNullException(nameof(client));
            _circlesQuery = new CirclesQuery<TransactionHistoryRow>(client);
        }

        /// <summary>
        /// Fetches the first page of transaction history for a given account.
        /// </summary>
        /// <param name="accountAddress">The account address to fetch transaction history for.</param>
        /// <param name="pageSize">The maximum number of rows per page.</param>
        /// <param name="id">Optional request identifier.</param>
        /// <returns>A CirclesQueryPage containing transaction history rows.</returns>
        public async Task<CirclesQueryPage<TransactionHistoryRow>> SendRequestAsync(string accountAddress, int pageSize = 100, object id = null)
        {
            if (string.IsNullOrWhiteSpace(accountAddress))
                throw new ArgumentNullException(nameof(accountAddress));

            // Build the QueryDefinition
            var queryDefinition = BuildQueryDefinition(accountAddress, pageSize);

            // Fetch the first page
            return await _circlesQuery.SendRequestAsync(queryDefinition, id);
        }

        /// <summary>
        /// Moves to the next page of the transaction history.
        /// </summary>
        /// <param name="currentPage">The current page of the query.</param>
        /// <param name="id">Optional request identifier.</param>
        /// <returns>The next CirclesQueryPage containing transaction history rows.</returns>
        public async Task<CirclesQueryPage<TransactionHistoryRow>> MoveNextPageAsync(CirclesQueryPage<TransactionHistoryRow> currentPage, object id = null)
        {
            if (currentPage == null)
                throw new ArgumentNullException(nameof(currentPage));

            return await _circlesQuery.MoveNextRequestAsync(currentPage, id);
        }

        /// <summary>
        /// Builds the QueryDefinition for the transaction history query.
        /// </summary>
        /// <param name="accountAddress">The account address to fetch transaction history for.</param>
        /// <param name="pageSize">The maximum number of rows per page.</param>
        /// <returns>A QueryDefinition object.</returns>
        private QueryDefinition BuildQueryDefinition(string accountAddress, int pageSize)
        {
            return new QueryDefinition
            {
                Namespace = "V_Crc",
                Table = "Transfers",
                Columns = new List<string>
                {
                    "blockNumber", "timestamp", "transactionIndex", "logIndex", "batchIndex",
                    "transactionHash", "version", "operator", "from", "to", "id", "value", "type", "tokenType"
                },
                Filter = new List<Filter>
                {
                    new Conjunction
                    {
                        ConjunctionType = "Or",
                        Predicates = new List<Filter>
                        {
                            new FilterPredicate
                            {
                                FilterType = "Equals",
                                Column = "from",
                                Value = accountAddress.ToLower()
                            },
                            new FilterPredicate
                            {
                                FilterType = "Equals",
                                Column = "to",
                                Value = accountAddress.ToLower()
                            }
                        }
                    }
                },
                Order = new List<Order>
                {
                    new Order { Column = "blockNumber", SortOrder = "ASC" },
                    new Order { Column = "transactionIndex", SortOrder = "ASC" },
                    new Order { Column = "logIndex", SortOrder = "ASC" }
                },
                Limit = pageSize
            };
        }
    }
}
