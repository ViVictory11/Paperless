using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Paperless.Contracts
{
    public class OcrMessage
    {
        public string DocumentId { get; set; } = string.Empty;
        public string ObjectName { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = "";
        public string? OcrText { get; set; }
        public string Language { get; set; } = "deu+eng";
        public bool IsSummaryAllowed { get; set; } = true;
        public bool IsResult => !string.IsNullOrEmpty(OcrText);
        public string? Summary { get; set; }
    }
}
