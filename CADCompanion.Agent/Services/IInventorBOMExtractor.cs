using CADCompanion.Agent.Models;

namespace CADCompanion.Agent.Services
{
    /// <summary>
    /// Define o contrato para serviços que extraem informações de arquivos do Inventor.
    /// </summary>
    public interface IInventorBOMExtractor
    {
        /// <summary>
        /// Extrai a Bill of Materials (BOM) de um documento de montagem do Inventor.
        /// </summary>
        Task<BOMExtractionResult> ExtractBOMAsync(dynamic assemblyDoc);

        /// <summary>
        /// Obtém o valor de uma iProperty customizada de um documento do Inventor.
        /// </summary>
        string? GetCustomIProperty(dynamic document, string propertyName);
        void SetCustomIProperty(dynamic document, string propertyName, string value);
void SetMachineTrackingProperties(dynamic document, int machineId, int projectId, string projectCode);
MachineTrackingInfo GetMachineTrackingInfo(dynamic document);
    }
}