// CADCompanion.Agent/Services/WorkSessionService.cs (Corrigido)
using CADCompanion.Agent.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CADCompanion.Agent.Services
{
    public class WorkSessionService
    {
        private readonly Dictionary<Guid, WorkSession> _sessions = new();
        private WorkSession? _currentSession;
        private readonly ILogger<WorkSessionService> _logger;
        private readonly IApiCommunicationService _apiCommunicationService;

        public WorkSessionService(ILogger<WorkSessionService> logger, IApiCommunicationService apiCommunicationService)
        {
            _logger = logger;
            _apiCommunicationService = apiCommunicationService;
        }

        public WorkSession StartNewSession(string projectNumber)
        {
            EndCurrentSession();
            _currentSession = new WorkSession
            {
                ProjectNumber = projectNumber
            };
            _sessions[_currentSession.Id] = _currentSession;
            _logger.LogInformation("Nova sessão de trabalho iniciada {SessionId} para o projeto {ProjectNumber}", _currentSession.Id, projectNumber);
            return _currentSession;
        }

        public void EndCurrentSession()
        {
            if (_currentSession != null && _currentSession.IsActive)
            {
                _currentSession.EndTime = DateTime.UtcNow;
                _currentSession.IsActive = false;
                _logger.LogInformation("Encerrando sessão {SessionId}. Duração: {Duration}", _currentSession.Id, _currentSession.Duration);
                _apiCommunicationService.SendWorkSessionEndedAsync(_currentSession);
                RemoveSession(_currentSession.Id);
                _currentSession = null;
            }
        }

        public void AddEventToCurrentSession(DocumentEvent ev)
        {
            if (_currentSession != null)
            {
                ev.SessionId = _currentSession.Id;
                _currentSession.DocumentEvents.Add(ev);
                _currentSession.LastActivity = DateTime.UtcNow;

                if (ev.EventType == DocumentEventType.Saved)
                {
                    _currentSession.SaveCount++;
                }

                _logger.LogInformation("Evento '{EventType}' adicionado ao documento '{FileName}' na sessão {SessionId}.", ev.EventType, ev.FileName, ev.SessionId);
                _apiCommunicationService.SendWorkSessionUpdatedAsync(_currentSession, $"Evento de documento: {ev.EventType}");
            }
        }

        public WorkSession? GetCurrentSession() => _currentSession;

        // Outros métodos corrigidos...
    }
}