using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Circles.RPC.Requests.DTOs;
using Nethereum.JsonRpc.Client;

namespace Nethereum.Circles.RPC.Requests
{
    public class GetAvatarInfoQuery
    {
        private readonly CirclesQuery<AvatarRow> _circlesQuery;

        public GetAvatarInfoQuery(IClient client)
        {
            _circlesQuery = new CirclesQuery<AvatarRow>(client);
        }

        public async Task<CirclesQueryPage<AvatarRow>> SendRequestAsync(string avatarAddress, object id = null)
        {
            if (string.IsNullOrWhiteSpace(avatarAddress))
                throw new ArgumentNullException(nameof(avatarAddress));

            var queryDefinition = BuildQueryDefinition(new List<string> { avatarAddress });
            return await _circlesQuery.SendRequestAsync(queryDefinition, id);
        }

        public async Task<CirclesQueryPage<AvatarRow>> SendRequestAsync(List<string> avatarAddresses, object id = null)
        {
            if (avatarAddresses == null || avatarAddresses.Count == 0)
                throw new ArgumentNullException(nameof(avatarAddresses));

            var queryDefinition = BuildQueryDefinition(avatarAddresses);
            return await _circlesQuery.SendRequestAsync(queryDefinition, id);
        }

        public async Task<CirclesQueryPage<AvatarRow>> MoveNextPageAsync(CirclesQueryPage<AvatarRow> currentPage, object id = null)
        {
            if (currentPage == null)
                throw new ArgumentNullException(nameof(currentPage));

            return await _circlesQuery.MoveNextRequestAsync(currentPage, id);
        }

        private QueryDefinition BuildQueryDefinition(List<string> avatarAddresses)
        {
            return new QueryDefinition
            {
                Namespace = "V_Crc",
                Table = "Avatars",
                Columns = new List<string>
                {
                    "blockNumber", "timestamp", "transactionIndex", "logIndex", "transactionHash",
                    "version", "type", "avatar", "tokenId", "cidV0Digest", "name"
                },
                Filter = new List<Filter>
                {
                    new FilterPredicate
                    {
                        FilterType = "In",
                        Column = "avatar",
                        Value = avatarAddresses.Select(a => a.ToLower()).ToList()
                    }
                },
                Order = new List<Order>
                {
                    new Order { Column = "blockNumber", SortOrder = "ASC" }
                },
                Limit = 1000
            };
        }
    }
    }
