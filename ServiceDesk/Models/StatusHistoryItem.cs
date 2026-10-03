using System;

namespace ServiceDesk.Models
{
    public class StatusHistoryItem
    {
        public int HistoryID { get; set; }
        public int RequestID { get; set; }
        public string OldStatusName { get; set; }
        public string NewStatusName { get; set; }
        public string ChangedByName { get; set; }
        public string AssignedToName { get; set; }
        public DateTime ChangedAt { get; set; }
        public string Comment { get; set; }

        public string ChangedAtText => ChangedAt.ToString("dd.MM.yyyy HH:mm");
    }
}
