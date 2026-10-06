using Siemens.Engineering;
using Siemens.Engineering.CrossReference;
using Siemens.Engineering.HW.Features;
using Siemens.Engineering.HmiUnified;
using Siemens.Engineering.Multiuser;
using Siemens.Engineering.SW;
using Siemens.Engineering.SW.Blocks;
using Siemens.Engineering.SW.ExternalSources;
using Siemens.Engineering.SW.Tags;
using Siemens.Engineering.SW.WatchAndForceTables;
using Siemens.Engineering.Safety;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using TiaMcpServer.ModelContextProtocol;

namespace TiaMcpServer.Siemens
{
    public partial class Portal
    {
        // From the former Portal.CrossReferences.cs:
        // Cross references for PLC software objects.
        //
        // Callers: the GetCrossReferences tool in McpServer.Software.CrossReferences.cs. Affected API: none
        // existing - all members are new. Reads/writes no data files.
        //
        // CrossReferenceService lives in Siemens.Engineering.Base, not Step7. Probed on V21 (PLC (A0) of the test project,
        // 2026-10-06): blocks, types and tags offer it; the PLC software, the root block, type and tag table groups, and
        // tag tables do NOT (GetService returns null). So the answer for the software, a group or a tag table is put together
        // from the objects inside it, and says so in a note. Watch tables, force tables and external sources have no
        // cross references at all, which is reported as NotSupported rather than NotFound.
        //
        // With the filter AllObjects every block, type and tag gives exactly one source (checked on 16 blocks and 50 tags),
        // so the whole-software answer pages over the objects first and asks Openness only for the page: reading the
        // sources of ~1900 tags takes seconds. Any other filter has to read them all to know how many sources remain.

        #region cross references

        /// <param name="objectPath">Empty targets the whole PLC software.</param>
        /// <param name="objectKind">
        /// "auto" (default) resolves the path against blocks, then types, then tag tables, then
        /// tags, then block groups. Pass an explicit kind to skip the probing.
        /// </param>
        public CrossReferenceResult GetCrossReferences(
            string softwarePath,
            string objectPath = "",
            string objectKind = "auto",
            CrossReferenceFilter filter = CrossReferenceFilter.AllObjects)
        {
            return Operation.Run(_logger, nameof(GetCrossReferences), PortalErrorCode.NotFound,
                () =>
                {
                    var provider = ResolveCrossReferenceProvider(softwarePath, objectPath, objectKind);

                    var service = provider.GetService<CrossReferenceService>()
                        ?? throw new PortalException(PortalErrorCode.NotSupported,
                            $"'{(string.IsNullOrEmpty(objectPath) ? softwarePath : objectPath)}' does not provide cross references. " +
                            "Watch tables, force tables and external sources have none. Blocks, types and tags offer them directly; the PLC software, a group and a tag table are answered through the objects inside (see plc_get_cross_references).");

                    return service.GetCrossReferences(filter);
                },
                ("softwarePath", softwarePath), ("objectPath", objectPath), ("objectKind", objectKind), ("filter", filter));
        }

        /// <summary>
        /// One page of the cross reference sources of an object. An empty <paramref name="objectPath"/> is the whole PLC
        /// software: blocks and types of all groups and the tags of all tag tables (<paramref name="objectKind"/> narrows it
        /// to 'block', 'type' or 'tagTable'/'tag'). Any other object goes through <see cref="GetCrossReferenceSources"/>.
        /// </summary>
        public (List<SourceObject> Sources, int Total) GetCrossReferencePage(
            string softwarePath,
            string objectPath,
            string objectKind,
            CrossReferenceFilter filter,
            int limit,
            int offset,
            out string? note)
        {
            string? pageNote = null;

            var page = Operation.Run(_logger, nameof(GetCrossReferencePage), PortalErrorCode.NotFound,
                () =>
                {
                    if (NormalizeGroupPath(objectPath).Length > 0)
                    {
                        var all = GetCrossReferenceSources(softwarePath, objectPath, objectKind, filter, out pageNote);
                        var (start, count) = ListPage<SourceObject>.Window(all.Count, limit, offset);

                        return (all.Skip(start).Take(count).ToList(), all.Count);
                    }

                    var software = GetPlcSoftwareOrThrow(softwarePath);
                    var kind = (objectKind ?? "auto").Trim().ToLowerInvariant();

                    var withBlocks = kind == "auto" || kind == "block" || kind == "blockgroup";
                    var withTypes = kind == "auto" || kind == "type";
                    var withTags = kind == "auto" || kind == "tagtable" || kind == "tag";

                    if (!withBlocks && !withTypes && !withTags)
                    {
                        throw new PortalException(PortalErrorCode.InvalidParams,
                            $"Unknown objectKind '{objectKind}'. Allowed values are 'auto', 'block', 'type', 'tagTable', 'tag' and 'blockGroup'.");
                    }

                    var providers = new List<IEngineeringServiceProvider>();
                    var counts = new List<string>();

                    if (withBlocks)
                    {
                        var blocks = 0;

                        void WalkBlocks(PlcBlockGroup group)
                        {
                            providers.AddRange(group.Blocks);
                            blocks += group.Blocks.Count;

                            foreach (var sub in group.Groups)
                            {
                                WalkBlocks(sub);
                            }
                        }

                        WalkBlocks(software.BlockGroup);
                        counts.Add($"{blocks} block(s)");
                    }

                    if (withTypes)
                    {
                        var types = 0;

                        void WalkTypes(global::Siemens.Engineering.SW.Types.PlcTypeUserGroup group)
                        {
                            providers.AddRange(group.Types);
                            types += group.Types.Count;

                            foreach (var sub in group.Groups)
                            {
                                WalkTypes(sub);
                            }
                        }

                        providers.AddRange(software.TypeGroup.Types);
                        types += software.TypeGroup.Types.Count;

                        foreach (var sub in software.TypeGroup.Groups)
                        {
                            WalkTypes(sub);
                        }
                        counts.Add($"{types} type(s)");
                    }

                    if (withTags)
                    {
                        var tags = 0;

                        void WalkTables(PlcTagTableGroup group)
                        {
                            foreach (var table in group.TagTables)
                            {
                                providers.AddRange(table.Tags);
                                tags += table.Tags.Count;
                            }

                            foreach (var sub in group.Groups)
                            {
                                WalkTables(sub);
                            }
                        }

                        WalkTables(software.TagTableGroup);
                        counts.Add($"{tags} tag(s)");
                    }

                    List<SourceObject> Read(IEnumerable<IEngineeringServiceProvider> objects)
                    {
                        return objects
                            .Select(o => o.GetService<CrossReferenceService>())
                            .Where(s => s != null)
                            .SelectMany(s => s!.GetCrossReferences(filter).Sources.Cast<SourceObject>())
                            .ToList();
                    }

                    pageNote = $"Openness gives the PLC software no cross references of its own; this is the answer for its {string.Join(", ", counts)}, read object by object.";

                    if (filter == CrossReferenceFilter.AllObjects)
                    {
                        var (start, count) = ListPage<SourceObject>.Window(providers.Count, limit, offset);

                        return (Read(providers.Skip(start).Take(count)), providers.Count);
                    }

                    var every = Read(providers);
                    var (from, take) = ListPage<SourceObject>.Window(every.Count, limit, offset);

                    return (every.Skip(from).Take(take).ToList(), every.Count);
                },
                ("softwarePath", softwarePath), ("objectPath", objectPath), ("objectKind", objectKind), ("filter", filter));

            note = pageNote;

            return page;
        }

        /// <summary>
        /// The cross reference sources of an object. A user group of blocks offers no service of its own in Openness, so
        /// its answer is the sources of its blocks, subgroups included; <paramref name="note"/> says so.
        /// </summary>
        public IReadOnlyList<SourceObject> GetCrossReferenceSources(
            string softwarePath,
            string objectPath,
            string objectKind,
            CrossReferenceFilter filter,
            out string? note)
        {
            string? groupNote = null;

            var sources = Operation.Run(_logger, nameof(GetCrossReferenceSources), PortalErrorCode.NotFound,
                () =>
                {
                    var provider = ResolveCrossReferenceProvider(softwarePath, objectPath, objectKind);
                    var service = provider.GetService<CrossReferenceService>();

                    if (service != null)
                    {
                        return (IReadOnlyList<SourceObject>)service.GetCrossReferences(filter).Sources.Cast<SourceObject>().ToList();
                    }

                    if (provider is PlcBlockGroup group)
                    {
                        var collected = new List<SourceObject>();
                        var blocks = 0;

                        void Walk(PlcBlockGroup current)
                        {
                            foreach (var block in current.Blocks)
                            {
                                var blockService = block.GetService<CrossReferenceService>();

                                if (blockService != null)
                                {
                                    blocks++;
                                    collected.AddRange(blockService.GetCrossReferences(filter).Sources.Cast<SourceObject>());
                                }
                            }

                            foreach (var sub in current.Groups)
                            {
                                Walk(sub);
                            }
                        }

                        Walk(group);
                        groupNote = $"Openness gives a block group no cross references of its own; this is the answer for its {blocks} block(s), subgroups included.";

                        return collected;
                    }

                    if (provider is PlcTagTable table)
                    {
                        var tagSources = new List<SourceObject>();

                        foreach (var tag in table.Tags)
                        {
                            var tagService = tag.GetService<CrossReferenceService>();

                            if (tagService != null)
                            {
                                tagSources.AddRange(tagService.GetCrossReferences(filter).Sources.Cast<SourceObject>());
                            }
                        }

                        groupNote = $"Openness gives a tag table no cross references of its own; this is the answer for its {table.Tags.Count} tag(s).";

                        return tagSources;
                    }

                    throw new PortalException(PortalErrorCode.NotSupported,
                        $"'{(string.IsNullOrEmpty(objectPath) ? softwarePath : objectPath)}' does not provide cross references. " +
                        "Watch tables, force tables and external sources have none. Blocks, types and tags offer them directly; the PLC software, a block group and a tag table are answered through the objects inside.");
                },
                ("softwarePath", softwarePath), ("objectPath", objectPath), ("objectKind", objectKind), ("filter", filter));

            note = groupNote;

            return sources;
        }

        /// <summary>
        /// Maps a path plus kind onto the Openness object that offers the cross reference
        /// service. Probing order matters: block names and type names can collide.
        /// </summary>
        private IEngineeringServiceProvider ResolveCrossReferenceProvider(string softwarePath, string objectPath, string objectKind)
        {
            var plcSoftware = GetPlcSoftwareOrThrow(softwarePath);

            if (NormalizeGroupPath(objectPath).Length == 0)
            {
                return plcSoftware;
            }

            var kind = (objectKind ?? "auto").Trim().ToLowerInvariant();

            IEngineeringServiceProvider? provider;

            switch (kind)
            {
                case "block":
                    provider = GetBlock(softwarePath, objectPath);
                    break;
                case "type":
                    provider = GetType(softwarePath, objectPath);
                    break;
                case "tagtable":
                    provider = GetTagTable(softwarePath, objectPath);
                    break;
                case "tag":
                    provider = GetTag(softwarePath, objectPath);
                    break;
                case "blockgroup":
                    provider = GetPlcBlockGroupByPath(softwarePath, objectPath);
                    break;
                case "auto":
                    // Each candidate needs its own cast: the ?? operator has no common type
                    // across PlcBlock, PlcType, PlcTagTable, PlcTag and PlcBlockGroup.
                    provider = (IEngineeringServiceProvider?)GetBlock(softwarePath, objectPath)
                               ?? (IEngineeringServiceProvider?)GetType(softwarePath, objectPath)
                               ?? (IEngineeringServiceProvider?)GetTagTable(softwarePath, objectPath)
                               ?? (IEngineeringServiceProvider?)TryGetTag(softwarePath, objectPath)
                               ?? (IEngineeringServiceProvider?)GetPlcBlockGroupByPath(softwarePath, objectPath);
                    break;
                default:
                    throw new PortalException(PortalErrorCode.InvalidParams,
                        $"Unknown objectKind '{objectKind}'. Allowed values are 'auto', 'block', 'type', 'tagTable', 'tag' and 'blockGroup'.");
            }

            return provider
                ?? throw new PortalException(PortalErrorCode.NotFound,
                    $"No object found at '{objectPath}' in '{softwarePath}'. Use 'plc_get_software_tree' to discover valid paths.");
        }

        /// <summary>
        /// GetTag throws InvalidParams for a path without a table segment, which is a normal
        /// outcome while probing in "auto" mode rather than a failure.
        /// </summary>
        private PlcTag? TryGetTag(string softwarePath, string objectPath)
        {
            try
            {
                return GetTag(softwarePath, objectPath);
            }
            catch (PortalException)
            {
                return null;
            }
        }

        #endregion
    }
}
