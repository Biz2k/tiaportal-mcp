using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseBlockData
    {
        public string? Path { get; set; }
        public string? Message { get; set; }
        public JsonObject? Meta { get; set; }
        public string? Name { get; set; }
        public string? TypeName { get; set; }
        public string? Namespace { get; set; }
        public string? ProgrammingLanguage { get; set; }
        public string? MemoryLayout { get; set; }
        public bool IsConsistent { get; set; }
        public string? HeaderName { get; set; }
        public DateTime ModifiedDate { get; set; }
        public bool IsKnowHowProtected { get; set; }
        public List<Attribute>? Attributes { get; set; }
        public string? Description { get; set; }
        
        // Optional sections
        public List<ResponseInterfaceMember>? Interface { get; set; }
        public ResponseSourceText? Source { get; set; }
    }
}
