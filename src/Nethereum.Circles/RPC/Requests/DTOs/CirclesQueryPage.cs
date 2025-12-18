using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Nethereum.Circles.RPC.Requests.DTOs
{
    public abstract class CirclesQueryPage
    {
        [JsonProperty("columns")]
        public List<string> Columns { get; set; } = new List<string>();

        [JsonProperty("rows")]
        public List<string[]> Rows { get; set; } = new List<string[]>();

        /// <summary>
        /// The original filter for this query, without the cursor filter.
        /// </summary>
        public QueryDefinition QueryDefinition { get; set; }

        public EventRow CurrentCursor { get; set; }

        public virtual EventRow GetNextCursor()
        {
            throw new NotImplementedException();
        }

    }

    /// <summary>
    /// Represents a generic paginated response.
    /// </summary>
    public class CirclesQueryPage<TEventRow> : CirclesQueryPage where TEventRow : EventRow, new()
    {

        private List<TEventRow> _response;
        public List<TEventRow> Response
        {
            get
            {
                if (_response == null) BuildResponse();
                return _response;
            }
        }

        public virtual void BuildResponse()
        {
            _response = QueryRowMapper.MapRows<TEventRow>(Columns, Rows);
        }

        /// <summary>
        /// Returns the next cursor for the query based on the last row.
        /// </summary>
        public override EventRow GetNextCursor()
        {
            if (Response.Count == 0) return null;

            var lastRow = Response.Last();
            return new EventRow
            {
                BlockNumber = lastRow.BlockNumber,
                TransactionIndex = lastRow.TransactionIndex,
                LogIndex = lastRow.LogIndex,
                BatchIndex = lastRow.BatchIndex
            };
        }

    }


    public class QueryRowMapper
    {
        public static T MapRow<T>(List<string> columns, string[] row) where T : new()
        {
            // Normalize column names for case-insensitive matching
            var normalizedColumns = columns.Select(c => c.ToLowerInvariant()).ToList();

            // Cache property info with lowercase names for case-insensitive matching
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);

            var mappedObject = new T();

            for (int i = 0; i < normalizedColumns.Count; i++)
            {
                var columnName = normalizedColumns[i];
                if (properties.TryGetValue(columnName, out var property) && row[i] != null)
                {
                    property.SetValue(mappedObject, Convert.ChangeType(row[i], property.PropertyType));
                }
            }

            return mappedObject;
        }


        public static List<T> MapRows<T>(List<string> columns, List<string[]> rows) where T : new()
        {
            // Normalize column names for case-insensitive matching
            var normalizedColumns = columns.Select(c => c.ToLowerInvariant()).ToList();

            // Cache property info with lowercase names for case-insensitive matching
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                                      .ToDictionary(p => p.Name.ToLowerInvariant(), p => p);

            var returnList = new List<T>();

            foreach (var row in rows)
            {

                var mappedObject = new T();

                for (int i = 0; i < normalizedColumns.Count; i++)
                {
                    var columnName = normalizedColumns[i];
                    if (properties.TryGetValue(columnName, out var property) && row[i] != null)
                    {
                        property.SetValue(mappedObject, Convert.ChangeType(row[i], property.PropertyType));
                    }
                }

                returnList.Add(mappedObject);

            }

            return returnList;
        }
    }
}

