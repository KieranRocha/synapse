// CADCompanion.Agent/Models/DocumentEvent.cs (Corrigido e Expandido)
using System;
using System.Collections.Generic;
using System.IO;

namespace CADCompanion.Agent.Models
{
    public enum DocumentEventType
    {
        Opened,
        Closed,
        Saved,
        Modified // Adicionado
    }

    public enum DocumentType
    {
        Part,
        Assembly,
        Drawing,
        Presentation,
        Unknown
    }

    public class WorkSession
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string MachineName { get; set; } = Environment.MachineName;
        public string UserName { get; set; } = Environment.UserName;
        public string Engineer { get; set; } = Environment.UserName;
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? EndTime { get; set; }
        public DateTime LastActivity { get; set; } = DateTime.UtcNow;
        public bool IsActive { get; set; } = true;
        public List<DocumentEvent> DocumentEvents { get; set; } = new List<DocumentEvent>();
        public string ProjectNumber { get; set; } = "N/A";
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
        public int SaveCount { get; set; } = 0;

        public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;
    }

    public class DocumentEvent
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public DocumentEventType EventType { get; set; }
        public string DocumentPath { get; set; } = string.Empty;
        public string FileName => Path.GetFileName(DocumentPath);
        public DocumentType DocType { get; set; }
        public Guid SessionId { get; set; }
        public int? ProjectId { get; set; }
        public string? ProjectName { get; set; }
    }

    public class DocumentWatcher : IDisposable
    {
        public string FullPath { get; set; }
        public string FileName => Path.GetFileName(FullPath);
        public DocumentType Type { get; set; }
        public FileSystemWatcher Watcher { get; set; }

        public DocumentWatcher(string path, DocumentType type)
        {
            FullPath = path;
            Type = type;
            var directory = Path.GetDirectoryName(path);
            var fileName = Path.GetFileName(path);
            if (directory != null)
            {
                Watcher = new FileSystemWatcher(directory, fileName);
            }
            else
            {
                throw new ArgumentException("Caminho do arquivo inválido para o Watcher.", nameof(path));
            }
        }

        public void Dispose()
        {
            Watcher?.Dispose();
        }
    }
}