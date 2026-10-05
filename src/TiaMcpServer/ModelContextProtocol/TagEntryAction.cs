using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class TagEntryAction
    {
        public string action { get; set; } 
        public string type { get; set; }   
        public string tagTablePath { get; set; }
        public string name { get; set; }   
        
        public string newName { get; set; }
        public string dataTypeName { get; set; }
        public string logicalAddress { get; set; } 
        public string value { get; set; }          
        
        public bool? externalAccessible { get; set; }
        public bool? externalVisible { get; set; }
        public bool? externalWritable { get; set; }
    }
}
