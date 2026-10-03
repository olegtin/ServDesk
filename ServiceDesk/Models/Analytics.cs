using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ServiceDesk.Models
{
    /// <summary>
    /// Статистика по заявкам
    /// </summary>
    public class RequestStats
    {
        public int Total { get; set; }
        public int Completed { get; set; }
        public int InProgress { get; set; }
        public int Overdue { get; set; }
        public double AvgCompletionHours { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    /// <summary>
    /// Категория с количеством заявок
    /// </summary>
    public class CategoryLoad
    {
        public int CategoryID { get; set; }
        public string CategoryName { get; set; }
        public int RequestCount { get; set; }      // Всего заявок в категории

        // 🔹 Новые поля для статистики по статусам
        public int CompletedCount { get; set; }    // Выполнено
        public int InProgressCount { get; set; }   // В работе

        // 🔹 Теперь Percent = процент ВЫПОЛНЕНИЯ внутри категории
       public double Percent => RequestCount > 0 
        ? (double)CompletedCount / RequestCount * 100 
        : 0;
    

        public string Rank { get; set; }
    }
}