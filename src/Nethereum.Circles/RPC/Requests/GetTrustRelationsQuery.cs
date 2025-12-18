using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Circles.RPC.Requests.DTOs;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Circles.RPC.Requests
{

    public class GetTrustRelationsQuery
    {
        private readonly CirclesQuery<TrustListRow> _circlesQuery;

        public GetTrustRelationsQuery(IClient client)
        {
            _circlesQuery = new CirclesQuery<TrustListRow>(client);
        }

        public async Task<CirclesQueryPage<TrustListRow>> SendRequestAsync(string avatarAddress, int pageSize, object id = null)
        {
            if (string.IsNullOrWhiteSpace(avatarAddress))
                throw new ArgumentNullException(nameof(avatarAddress));

            var queryDefinition = BuildQueryDefinition(avatarAddress, pageSize);
            return await _circlesQuery.SendRequestAsync(queryDefinition, id);
        }

        public async Task<CirclesQueryPage<TrustListRow>> MoveNextPageAsync(CirclesQueryPage<TrustListRow> currentPage, object id = null)
        {
            if (currentPage == null)
                throw new ArgumentNullException(nameof(currentPage));
            return await _circlesQuery.MoveNextRequestAsync(currentPage, id);
        }

        private QueryDefinition BuildQueryDefinition(string avatarAddress, int pageSize)
        {
            return new QueryDefinition
            {
                Namespace = "V_Crc",
                Table = "TrustRelations",
                Columns = new List<string>
                {
                    "blockNumber", "timestamp", "transactionIndex", "logIndex", "transactionHash",
                    "version", "trustee", "truster", "expiryTime", "limit"
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
                                Column = "trustee",
                                Value = avatarAddress.ToLower()
                            },
                            new FilterPredicate
                            {
                                FilterType = "Equals",
                                Column = "truster",
                                Value = avatarAddress.ToLower()
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
