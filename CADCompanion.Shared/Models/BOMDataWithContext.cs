using System;
using System.Collections.Generic;

namespace CADCompanion.Shared.Models
{
    /// <summary>
    /// Representa dados de BOM com contexto adicional para processamento
    /// </summary>
    public class BOMDataWithContext
    {
        public BOMDataWithContext()
        {
            BomItems = new List<BomItem>();
            ExtractedAt = DateTime.UtcNow;
            Metadata = new Dictionary<string, object>();
        }

        // Propriedades de identificação
        public string FilePath { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string ProjectName { get; set; } = string.Empty;
        public string MachineName { get; set; } = string.Empty;

        // Dados do BOM
        public int TotalItems { get; set; }
        public List<BomItem> BomItems { get; set; }

        // Metadados
        public string ExtractedBy { get; set; } = string.Empty;
        public DateTime ExtractedAt { get; set; }
        public string? FileVersion { get; set; }
        public long FileSizeBytes { get; set; }

        // Propriedades adicionais para rastreabilidade
        public string? AssemblyPath { get; set; }
        public string? InventorVersion { get; set; }
        public Dictionary<string, object> Metadata { get; set; }

        // Propriedades calculadas
        public bool HasItems => BomItems?.Count > 0;
        public bool IsValid => !string.IsNullOrEmpty(FilePath) && !string.IsNullOrEmpty(FileName);
    }

    /// <summary>
    /// Representa um item individual do BOM
    /// </summary>
    public class BomItem
    {
        public string PartNumber { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int Quantity { get; set; }
        public string? Material { get; set; }
        public double Mass { get; set; }
        public string Unit { get; set; } = "UN";
        public int Level { get; set; }
        public string? ParentPartNumber { get; set; }
        public string Category { get; set; } = "fabricado";
        public Dictionary<string, string> CustomProperties { get; set; }

        public BomItem()
        {
            CustomProperties = new Dictionary<string, string>();
        }
    }
}