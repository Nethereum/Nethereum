using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Circles.RPC.Requests.DTOs;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Circles.RPC.Requests
{
    /// <summary>
    /// Executes the Circles `circles_query` RPC method.
    /// </summary>
    public class CirclesQuery<TEventRow> : RpcRequestResponseHandler<CirclesQueryPage<TEventRow>>
         where TEventRow : EventRow, new()
    {
        public CirclesQuery(IClient client) : base(client, "circles_query") { }

        /// <summary>
        /// Sends a paginated query request to Circles.
        /// </summary>
        /// <param name="queryDefinition">The query definition object.</param>
        /// <param name="id">Optional request identifier.</param>
        /// <returns>A paginated query response.</returns>
        public async Task<CirclesQueryPage<TEventRow>> SendRequestAsync(QueryDefinition queryDefinition, object id = null)
        {
            if (queryDefinition == null)
                throw new ArgumentNullException(nameof(queryDefinition));

            if (queryDefinition == null)
                throw new ArgumentNullException(nameof(queryDefinition));

            var circleQueryPage =  await base.SendRequestAsync(id, queryDefinition);
            circleQueryPage.QueryDefinition = queryDefinition;
            return circleQueryPage;
        }

        public async Task<CirclesQueryPage<TEventRow>> MoveNextRequestAsync(CirclesQueryPage<TEventRow> circlesQueryPage, object id = null)
        {
            var nextQueryDefinition = GetMoveNextQueryDefinition(circlesQueryPage);
            var responseCircleQueryPage = await base.SendRequestAsync(id, nextQueryDefinition);
            responseCircleQueryPage.CurrentCursor = circlesQueryPage.GetNextCursor();
            responseCircleQueryPage.QueryDefinition = circlesQueryPage.QueryDefinition;
            return responseCircleQueryPage;

        }


        public QueryDefinition GetMoveNextQueryDefinition(CirclesQueryPage page)
        {
            if (page == null)
                throw new ArgumentNullException(nameof(page));

            // Combine current filter with cursor filter
            var nextFilter = BuildCursorFilter(page.GetNextCursor(), page.QueryDefinition.Filter);

            return new QueryDefinition
            {
                Namespace = page.QueryDefinition.Namespace,
                Table = page.QueryDefinition.Table,
                Columns = page.QueryDefinition.Columns,
                Filter = new List<Filter> { nextFilter },
                Order = page.QueryDefinition.Order,
                Limit = page.QueryDefinition.Limit
            };
        }

        private Conjunction BuildCursorFilter(EventRow cursor, List<Filter> currentFilter)
        {
            // Cursor-specific filter
            var cursorFilter = new Conjunction
            {
                ConjunctionType = "Or",
                Predicates = new List<Filter>
        {
            // Case 1: blockNumber > cursor.blockNumber
            new Conjunction
            {
                ConjunctionType = "And",
                Predicates = new List<Filter>
                {
                    new FilterPredicate
                    {
                        FilterType = "GreaterThan",
                        Column = "blockNumber",
                        Value = cursor.BlockNumber
                    }
                }
            },
            // Case 2: Same blockNumber, transactionIndex > cursor.transactionIndex
            new Conjunction
            {
                ConjunctionType = "And",
                Predicates = new List<Filter>
                {
                    new FilterPredicate
                    {
                        FilterType = "Equals",
                        Column = "blockNumber",
                        Value = cursor.BlockNumber
                    },
                    new FilterPredicate
                    {
                        FilterType = "GreaterThan",
                        Column = "transactionIndex",
                        Value = cursor.TransactionIndex
                    }
                }
            },
            // Case 3: Same blockNumber and transactionIndex, logIndex > cursor.logIndex
            new Conjunction
            {
                ConjunctionType = "And",
                Predicates = new List<Filter>
                {
                    new FilterPredicate
                    {
                        FilterType = "Equals",
                        Column = "blockNumber",
                        Value = cursor.BlockNumber
                    },
                    new FilterPredicate
                    {
                        FilterType = "Equals",
                        Column = "transactionIndex",
                        Value = cursor.TransactionIndex
                    },
                    new FilterPredicate
                    {
                        FilterType = "GreaterThan",
                        Column = "logIndex",
                        Value = cursor.LogIndex
                    }
                }
            }
        }
            };

            // Combine current filter with cursor filter using "And"
            return new Conjunction
            {
                ConjunctionType = "And",
                Predicates = new List<Filter>
        {
            new Conjunction
            {
                ConjunctionType = "And",
                Predicates = currentFilter // Original filter
            },
            cursorFilter // Cursor-specific filter
        }
            };
        }

    }
}
