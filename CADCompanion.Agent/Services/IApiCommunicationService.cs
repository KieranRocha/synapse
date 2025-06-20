// Services/IApiCommunicationService.cs - CORRIGIDO

using CADCompanion.Agent.Models;
using CADCompanion.Shared.Contracts;

namespace CADCompanion.Agent.Services;

public interface IApiCommunicationService
{
    // O método principal que já está funcionando no servidor
    Task<bool> SubmitBomAsync(BomSubmissionDto bomData);

    // Outros métodos que sua aplicação precisa para se comunicar com a API
    Task SendWorkSessionEndedAsync(WorkSession session);
    Task SendWorkSessionUpdatedAsync(WorkSession session);
    Task SendWorkSessionUpdatedAsync(WorkSession session, string updateReason); // Sobrecarga
    Task SendHeartbeatAsync();
    Task SendBOMDataAsync(BOMDataWithContext bomData);
    Task<bool> SendMachineStatusAsync(object statusData);
    Task SendPartDataAsync(object partData); // Considere criar um DTO específico em vez de 'object'
    Task SendDocumentActivityAsync(DocumentEvent documentEvent);
}