using System.Collections.Generic;

namespace TiaMcpServer.ModelContextProtocol
{
    public class ResponseHmiManageItems : ResponseMessage
    {
        public List<HmiItemResult>? Results { get; set; }

        /// <summary>Number of actions applied. A call either applies all of them or fails as a whole.</summary>
        public int SuccessCount { get; set; }
    }

    public class HmiItemResult
    {
        public string? Action { get; set; }
        public string? ScreenName { get; set; }
        public string? ItemName { get; set; }

        /// <summary>"success" or "error".</summary>
        public string? Status { get; set; }
        public string? Error { get; set; }

        /// <summary>The code of the first failure that had one; the batch error reports it (not part of the answer).</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public TiaMcpServer.Siemens.PortalErrorCode? Code { get; set; }

        /// <summary>Properties that were set.</summary>
        public List<string>? Applied { get; set; }

        /// <summary>Properties that were not set, each with the reason.</summary>
        public List<HmiPropertyFailure>? Failed { get; set; }

        /// <summary>Things worth knowing that are not failures, e.g. a dynamization overriding a static value.</summary>
        public List<string>? Notes { get; set; }
    }

    public class HmiPropertyFailure
    {
        public string? Property { get; set; }
        public string? Error { get; set; }

        /// <summary>The code of the failure when it was a PortalException; the batch error reports it (not part of the answer).</summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public TiaMcpServer.Siemens.PortalErrorCode? Code { get; set; }
    }
}
