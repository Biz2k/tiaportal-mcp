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

        /// <summary>The 'offset' that reads the next page; meaningful when <see cref="Truncated"/>.</summary>
        internal int NextOffset => Offset + Items.Count;

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
            var (start, count) = Window(all.Count, limit, offset);

            return new ListPage<T>(all.Skip(start).Take(count).ToList(), all.Count, offset);
        }

        /// <summary>A page whose items were produced on their own, for a list too costly to build whole: <paramref name="total"/> says how long the list is.</summary>
        internal static ListPage<T> Ready(List<T> pageItems, int total, int offset)
        {
            return new ListPage<T>(pageItems, total, offset);
        }

        /// <summary>The first item and the number of items of a page of a list of <paramref name="total"/> items.</summary>
        internal static (int Start, int Count) Window(int total, int limit, int offset)
        {
            if (offset < 0)
            {
                throw new PortalException(PortalErrorCode.InvalidParams, $"'offset' is {offset}; it counts the items to skip and cannot be negative.");
            }

            var start = Math.Min(offset, total);
            var count = limit > 0 ? Math.Min(limit, total - start) : total - start;

            return (start, count);
        }

        /// <summary>Empty when the page is complete; otherwise the sentence that says what was left out and how to get it.</summary>
        internal string Note(string narrowBy)
        {
            if (!Truncated)
            {
                // found live (task 36): offset 9999 on 2556 tags said "Items 10000 to 9999 of 2556."
                if (Items.Count == 0 && Total > 0)
                {
                    return $" The offset {Offset} is past the end: the list has {Total} item(s), the last offset is {Total - 1}.";
                }

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

            if (Truncated)
            {
                meta["nextOffset"] = NextOffset;
            }

            return meta;
        }
    }

    /// <summary>The same words for the paging parameters of every list tool, and the page of a list in one call.</summary>
    internal static class Paging
    {
        internal const string LimitText = "limit: the most items to return in one page; 0 returns all. A longer list is cut and the answer says so";

        internal const string OffsetText = "offset: items to skip, to read the next page of a long list: pass the 'nextOffset' of the previous answer (default 0)";

        internal static ListPage<T> Page<T>(IReadOnlyList<T> all, int limit, int offset) => ListPage<T>.Of(all, limit, offset);
    }
}
