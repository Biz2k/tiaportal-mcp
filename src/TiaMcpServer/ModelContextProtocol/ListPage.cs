using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using TiaMcpServer.Siemens;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>
    /// One page of a long list: a tool that can return thousands of items returns the first <c>limit</c> of them and says
    /// so, so an unfiltered call does not fill the context of the client. 'offset' reads on from where the page ended.
    /// </summary>
    internal sealed class ListPage<T>
    {
        /// <summary>The default most items a list tool returns.</summary>
        internal const int DefaultLimit = 500;

        internal List<T> Items { get; }

        internal int Total { get; }

        internal int Offset { get; }

        internal bool Truncated => Offset + Items.Count < Total;

        private ListPage(List<T> items, int total, int offset)
        {
            Items = items;
            Total = total;
            Offset = offset;
        }

        /// <param name="limit">The most items; 0 or less returns all.</param>
        /// <param name="offset">Items to skip first.</param>
        internal static ListPage<T> Of(IReadOnlyList<T> all, int limit, int offset)
        {
            if (offset < 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'offset' is {offset}; it counts the items to skip and cannot be negative.");
            }

            var items = all.Skip(offset);

            if (limit > 0)
            {
                items = items.Take(limit);
            }

            return new ListPage<T>(items.ToList(), all.Count, offset);
        }

        /// <summary>Empty when the page is complete; otherwise the sentence that says what was left out and how to get it.</summary>
        internal string Note(string narrowBy)
        {
            if (!Truncated)
            {
                return Offset > 0 ? $" Items {Offset + 1} to {Offset + Items.Count} of {Total}." : string.Empty;
            }

            return $" Cut: showing items {Offset + 1} to {Offset + Items.Count} of {Total}. Pass offset={Offset + Items.Count} for the next page, or narrow the list with {narrowBy}.";
        }

        /// <summary>Adds the paging facts to a response's meta.</summary>
        internal JsonObject Meta(JsonObject meta)
        {
            meta["total"] = Total;
            meta["offset"] = Offset;
            meta["truncated"] = Truncated;

            return meta;
        }
    }
}
