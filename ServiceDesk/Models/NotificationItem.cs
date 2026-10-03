using System;

namespace ServiceDesk.Models
{
    public class NotificationItem
    {
        public int NotificationID { get; set; }
        public int? TargetUserID { get; set; }
        public int? CreatedBy { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public bool IsRead { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; }
        public string TargetUserName { get; set; }

        public string CreatedAtText => CreatedAt.ToString("dd.MM.yyyy HH:mm");
        public string StatusText => IsRead ? "Прочитано" : "Новое";
        public string TargetText => TargetUserID.HasValue ? TargetUserName : "Все пользователи";
    }
}
