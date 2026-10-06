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
}
