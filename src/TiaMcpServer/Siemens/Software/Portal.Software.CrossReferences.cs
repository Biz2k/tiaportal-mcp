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
        // CrossReferenceService lives in Siemens.Engineering.Base, not Step7, and is offered by
        // software, blocks, types, their groups, tags, tag tables and constants - but NOT by watch
        // tables, force tables or external sources. A null service therefore means "this object
        // kind has no cross references", which is reported as NotSupported rather than NotFound.

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
                            "Watch tables, force tables and external sources have none; blocks, types, groups, tags and tag tables do.");

                    return service.GetCrossReferences(filter);
                },
                ("softwarePath", softwarePath), ("objectPath", objectPath), ("objectKind", objectKind), ("filter", filter));
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

                    throw new PortalException(PortalErrorCode.NotSupported,
                        $"'{(string.IsNullOrEmpty(objectPath) ? softwarePath : objectPath)}' does not provide cross references. " +
                        "Watch tables, force tables and external sources have none; blocks, types, tags and tag tables do, and a block group is answered through its blocks.");
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
