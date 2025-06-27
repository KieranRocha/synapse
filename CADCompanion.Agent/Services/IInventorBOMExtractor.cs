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

        /// <summary>
        /// Extrai o BOM de um documento assembly do Inventor.
        /// </summary>
        List<BomItem> GetBOMFromDocument(dynamic assemblyDoc);

        /// <summary>
        /// Gera um hash SHA256 do BOM serializado.
        /// </summary>
        string GetBOMHash(List<BomItem> bomItems);
    }
}