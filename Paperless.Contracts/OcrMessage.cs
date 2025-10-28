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
        public string? OcrText { get; set; }                   
        public bool IsResult => !string.IsNullOrEmpty(OcrText);
    }
}
