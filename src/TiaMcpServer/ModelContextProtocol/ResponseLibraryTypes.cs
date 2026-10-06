using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseLibraryTypes : ResponseMessage
    {
        public List<LibraryTypeInfo>? Items { get; set; }

        /// <summary>How many types each system has, before the 'system' filter.</summary>
        public Dictionary<string, int>? Systems { get; set; }
    }

    public class LibraryTypeInfo
    {
        /// <summary>The values of <see cref="System"/>.</summary>
        public static readonly string[] Systems = { "unified", "classic", "plc", "universal" };

        public string? Name { get; set; }

        /// <summary>Folder of the type inside the library, '/'-separated; empty at the top.</summary>
        public string? Path { get; set; }

        /// <summary>Openness class of the type, e.g. "FaceplateLibraryType".</summary>
        public string? Kind { get; set; }

        /// <summary>"unified", "classic", "plc" or "universal".</summary>
        public string? System { get; set; }

        /// <summary>What the system value means for this type and what it was derived from.</summary>
        public string? SystemNote { get; set; }

        /// <summary>Lowest device version the type can be used on; set for WinCC Unified types.</summary>
        public string? MinimumTargetDeviceVersion { get; set; }

        public string? Author { get; set; }

        public string? Status { get; set; }

        public List<LibraryTypeVersionInfo> Versions { get; set; } = new List<LibraryTypeVersionInfo>();
    }

    public class LibraryTypeVersionInfo
    {
        public string? Version { get; set; }

        public string? State { get; set; }

        public bool IsDefault { get; set; }

        /// <summary>For a WinCC Unified type: the value 'unified_manage_faceplate' takes as faceplateType.</summary>
        public string? ContainedType { get; set; }
    }
}
