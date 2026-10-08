using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    /// <summary>Properties of one screen item or screen, by name.</summary>
    public class ResponseUnifiedProperties : ResponseMessage
    {
        public Dictionary<string, object?>? Items { get; set; }
    }

    /// <summary>A list of objects described by their attributes (connections, library types).</summary>
    public class ResponseUnifiedList : ResponseMessage
    {
        public List<Dictionary<string, object?>>? Items { get; set; }
    }

    public class ResponseUnifiedFaceplate : ResponseMessage
    {
        public UnifiedFaceplateResult? Result { get; set; }
    }

    public class UnifiedFaceplateResult
    {
        public string? ScreenName { get; set; }
        public string? ItemName { get; set; }

        /// <summary>True when the call created the instance.</summary>
        public bool Created { get; set; }

        /// <summary>The faceplate type and version the instance shows, e.g. "V0.0.2\MyFaceplate".</summary>
        public string? FaceplateType { get; set; }

        /// <summary>What the call set.</summary>
        public List<string> Applied { get; set; } = new List<string>();

        /// <summary>The interface of the faceplate type: what can be set through 'interfaceValues'.</summary>
        public List<UnifiedFaceplateInterfaceProperty> Interface { get; set; } = new List<UnifiedFaceplateInterfaceProperty>();
    }

    public class UnifiedFaceplateInterfaceProperty
    {
        public string? Name { get; set; }

        /// <summary>Openness class of the interface property.</summary>
        public string? Kind { get; set; }

        /// <summary>Static value, as text.</summary>
        public string? Value { get; set; }

        /// <summary>.NET type of the static value, which is the type a new value is converted to.</summary>
        public string? ValueType { get; set; }

        /// <summary>Kind of dynamization on the property, if any, e.g. "TagDynamization".</summary>
        public string? Dynamization { get; set; }

        /// <summary>HMI tag (or, for a tag interface, the tag parameter) the property is bound to, if it is.</summary>
        public string? Tag { get; set; }

        /// <summary>Script that computes the property, if it has one.</summary>
        public string? Script { get; set; }
    }

    /// <summary>One error or warning of a compile, with the place the compiler names for it.</summary>
    public class CompileMessageLine
    {
        /// <summary>"Error" or "Warning".</summary>
        public string? Severity { get; set; }

        /// <summary>Where, joined with '/': for an HMI the device, "Screens", the screen and the item; for a block the PLC, the groups, the block and the line.</summary>
        public string? Path { get; set; }

        public string? Text { get; set; }
    }

    /// <summary>What Portal.CompileUnified found.</summary>
    public class UnifiedCompileResult
    {
        public string? Device { get; set; }

        public string? State { get; set; }

        public int ErrorCount { get; set; }

        public int WarningCount { get; set; }

        public List<CompileMessageLine> Items { get; set; } = new List<CompileMessageLine>();
    }

    /// <summary>
    /// The compile that a writing tool runs after its changes were committed ('compile': true). The counts and the state are those of the
    /// whole device, the items only those of the screens or modules the call changed. A failed compile does not undo the write.
    /// </summary>
    public class UnifiedCompileSummary
    {
        public string? State { get; set; }

        public int ErrorCount { get; set; }

        public int WarningCount { get; set; }

        public List<CompileMessageLine> Items { get; set; } = new List<CompileMessageLine>();

        /// <summary>What to know about it: where the items were cut, or why the compile did not run.</summary>
        public string? Note { get; set; }
    }

    public class ResponseUnifiedCompile : ResponseMessage
    {
        /// <summary>Success, Warning or Error - of the whole device, whatever the filters left of the messages.</summary>
        public string? State { get; set; }

        public int? ErrorCount { get; set; }

        public int? WarningCount { get; set; }

        public IEnumerable<CompileMessageLine>? Items { get; set; }
    }
}
