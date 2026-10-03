using System;

namespace ServiceDesk.Models
{
    public class PlannedTaskItem
    {
        public int TaskID { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime StartAt { get; set; }
        public DateTime? EndAt { get; set; }
        public int CreatedBy { get; set; }
        public int? AssignedTo { get; set; }
        public bool IsCompleted { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; }
        public string AssignedToName { get; set; }

        public string TimeText => StartAt.ToString("dd.MM.yyyy HH:mm");
        public string ShortTimeText => StartAt.ToString("HH:mm");
        public string DescriptionText => string.IsNullOrWhiteSpace(Description) ? "Без описания" : Description;
        public string StatusText => IsCompleted ? "Выполнена" : "Запланирована";
    }
}
