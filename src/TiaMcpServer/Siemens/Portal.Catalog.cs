using System;
using System.Collections.Generic;
using System.Linq;

namespace TiaMcpServer.Siemens
{
    public class CatalogFolder
    {
        public string? Name { get; set; }

        /// <summary>Catalog entries in this folder and below it; an article in three firmware versions is three entries.</summary>
        public int Entries { get; set; }
    }

    public class CatalogEntryInfo
    {
        /// <summary>What 'hw_create_device' and 'hw_plug_module' take as typeIdentifier.</summary>
        public string? TypeIdentifier { get; set; }

        public string? TypeName { get; set; }

        public string? ArticleNumber { get; set; }

        public string? Version { get; set; }
    }

    public class CatalogLevel
    {
        /// <summary>The folder that was read, without the leading 'Root'; empty for the top of the catalog.</summary>
        public string Path { get; set; } = string.Empty;

        public int TotalEntries { get; set; }

        public List<CatalogFolder> Folders { get; set; } = new List<CatalogFolder>();

        /// <summary>The entries that sit in this folder itself - usually the versions of one article.</summary>
        public List<CatalogEntryInfo> Entries { get; set; } = new List<CatalogEntryInfo>();
    }

    public class InstalledProduct
    {
        public string? Name { get; set; }

        public string? Version { get; set; }

        public List<string>? Options { get; set; }
    }

    public class InstalledGsdFile
    {
        public string? File { get; set; }

        /// <summary>Where its devices sit in the catalog, e.g. 'Other field devices\PROFINET IO\Drives\Siemens Aktiengesellschaft'.</summary>
        public string? CatalogPath { get; set; }

        /// <summary>The head modules (devices) the file describes; each can be created with 'hw_create_device'.</summary>
        public int Devices { get; set; }

        public int Modules { get; set; }

        public List<string>? Examples { get; set; }
    }

    public class InstalledSoftwareInfo
    {
        public List<InstalledProduct> Products { get; set; } = new List<InstalledProduct>();

        public int CatalogEntries { get; set; }

        /// <summary>All GSD files with devices in the catalog, whatever the filter.</summary>
        public int? GsdFileCount { get; set; }

        public List<InstalledGsdFile>? GsdFiles { get; set; }
    }

    // The hardware catalog as a tree, and what is installed. Callers: hw_get_catalog and get_installed_software.
    //
    // Found on V21 (probe of 2026-10-07):
    //   - HardwareCatalog.Find("") returns the whole catalog - 16077 entries in about a second on the owner's machine
    //     (STEP 7, WinCC, Startdrive). CatalogEntry.CatalogPath is 'Root\Controllers\...\<article>'; the versions of
    //     an article share one path.
    //   - Devices from GSD files are entries like any other: TypeIdentifier 'GSD:<file>/DAP/<id>' for a head module
    //     and 'GSD:<file>/M/<id>' for a module, under 'Root\Other field devices\...'; Version holds the file name.
    //   - TiaPortalProcess.InstalledSoftware lists the products with their options (the Startdrive option names the
    //     drive families). Support packages (HSP) are not listed apart; what they add is in the catalog.
    //   - There is no call to install a GSD file or a support package: that is done in TIA Portal
    //     (Options > Manage general station description files / Support packages).
    public partial class Portal
    {
        private List<dynamic> WholeCatalog()
        {
            if (_portal == null)
            {
                throw new PortalException(PortalErrorCode.InvalidState, "Not connected to TIA Portal. Call 'connect' first.");
            }

            dynamic portal = _portal;

            try
            {
                var entries = new List<dynamic>();

                foreach (var entry in portal.HardwareCatalog.Find(string.Empty))
                {
                    entries.Add(entry);
                }

                return entries;
            }
            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException ex)
            {
                throw new PortalException(PortalErrorCode.NotSupported, "This TIA Portal version does not offer the hardware catalog through Openness.", null, ex);
            }
        }

        private static string[] CatalogSegments(string? path)
        {
            // only '\' separates: folder names hold '/' themselves ('Distributed I/O')
            var parts = (path ?? string.Empty).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToList();

            if (parts.Count > 0 && parts[0].Equals("Root", StringComparison.OrdinalIgnoreCase))
            {
                parts.RemoveAt(0);
            }

            return parts.ToArray();
        }

        public CatalogLevel GetHardwareCatalog(string? path, int limit, int offset)
        {
            return Operation.Run(_logger, nameof(GetHardwareCatalog), PortalErrorCode.NotSupported,
                () =>
                {
                    var wanted = CatalogSegments(path);
                    var level = new CatalogLevel { Path = string.Join("\\", wanted) };
                    var folders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    var order = new List<string>();
                    var here = new List<dynamic>();

                    foreach (var entry in WholeCatalog())
                    {
                        var segments = CatalogSegments(ReadCatalogText(() => entry.CatalogPath));

                        if (segments.Length < wanted.Length || !wanted.SequenceEqual(segments.Take(wanted.Length), StringComparer.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        level.TotalEntries++;

                        if (segments.Length == wanted.Length)
                        {
                            here.Add(entry);
                            continue;
                        }

                        var name = segments[wanted.Length];

                        if (!folders.ContainsKey(name))
                        {
                            folders[name] = 0;
                            order.Add(name);
                        }

                        folders[name]++;
                    }

                    if (level.TotalEntries == 0 && wanted.Length > 0)
                    {
                        throw new PortalException(PortalErrorCode.NotFound,
                            $"The hardware catalog has no folder '{path}'. Start with an empty path and go down by the names of 'folders', joined with '\\' (a '/' belongs to a name, as in 'Distributed I/O') - e.g. 'Controllers\\SIMATIC S7-1500\\CPU'.");
                    }

                    level.Folders = order.Select(n => new CatalogFolder { Name = n, Entries = folders[n] }).ToList();

                    foreach (var entry in here.Skip(Math.Max(0, offset)).Take(limit <= 0 ? int.MaxValue : limit))
                    {
                        level.Entries.Add(new CatalogEntryInfo
                        {
                            TypeIdentifier = ReadCatalogText(() => entry.TypeIdentifier),
                            TypeName = ReadCatalogText(() => entry.TypeName),
                            ArticleNumber = ReadCatalogText(() => entry.ArticleNumber),
                            Version = ReadCatalogText(() => entry.Version)
                        });
                    }

                    return level;
                },
                ("path", path));
        }

        public InstalledSoftwareInfo GetInstalledSoftware(bool withGsdFiles, string? gsdFilter = null)
        {
            return Operation.Run(_logger, nameof(GetInstalledSoftware), PortalErrorCode.NotSupported,
                () =>
                {
                    if (_portal == null)
                    {
                        throw new PortalException(PortalErrorCode.InvalidState, "Not connected to TIA Portal. Call 'connect' first.");
                    }

                    var info = new InstalledSoftwareInfo();

                    foreach (var product in _portal.GetCurrentProcess().InstalledSoftware)
                    {
                        var options = product.Options?.Select(o => $"{o.Name} {o.Version}".Trim()).ToList();

                        info.Products.Add(new InstalledProduct { Name = product.Name, Version = product.Version, Options = options != null && options.Count > 0 ? options : null });
                    }

                    var catalog = WholeCatalog();

                    info.CatalogEntries = catalog.Count;

                    if (!withGsdFiles)
                    {
                        return info;
                    }

                    var files = new Dictionary<string, InstalledGsdFile>(StringComparer.OrdinalIgnoreCase);

                    foreach (var entry in catalog)
                    {
                        string identifier = ReadCatalogText(() => entry.TypeIdentifier) ?? string.Empty;

                        if (!identifier.StartsWith("GSD:", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        var rest = identifier.Substring(4);
                        var cut = rest.IndexOf('/');
                        var name = cut < 0 ? rest : rest.Substring(0, cut);

                        if (!files.TryGetValue(name, out var file))
                        {
                            // 'Other field devices\PROFINET IO\Drives\<vendor>' - the part before the device itself
                            var segments = CatalogSegments(ReadCatalogText(() => entry.CatalogPath));

                            files[name] = file = new InstalledGsdFile { File = name, CatalogPath = string.Join("\\", segments.Take(Math.Min(4, Math.Max(0, segments.Length - 1)))) };
                        }

                        // 'GSD:<file>.XML/DAP/<id>' for PROFINET, 'GSD:<file>.GSD/DAP' for PROFIBUS; modules are '/M/', '/SM/'
                        var kind = cut < 0 ? string.Empty : rest.Substring(cut + 1).Split('/')[0];

                        if (kind.Equals("DAP", StringComparison.OrdinalIgnoreCase))
                        {
                            file.Devices++;
                            file.Examples ??= new List<string>();

                            if (file.Examples.Count < 2)
                            {
                                file.Examples.Add($"{ReadCatalogText(() => entry.TypeName)}: {identifier}");
                            }
                        }
                        else
                        {
                            file.Modules++;
                        }
                    }

                    var filter = (gsdFilter ?? string.Empty).Trim();

                    info.GsdFileCount = files.Count;
                    info.GsdFiles = files.Values
                        .Where(f => filter.Length == 0 || (f.File ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 || (f.CatalogPath ?? string.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0 ||
                                    (f.Examples != null && f.Examples.Any(e => e.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)))
                        .OrderBy(f => f.CatalogPath, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.File, StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    // a long list goes without the examples; a filtered one keeps them
                    if (info.GsdFiles.Count > 30)
                    {
                        foreach (var file in info.GsdFiles)
                        {
                            file.Examples = null;
                        }
                    }

                    return info;
                });
        }
    }
}
