using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace ServiceDesk.Models.Partials
{
    public class Navigation
    {
        public static Frame MainFrame { get; set; }
        public static Users CurrentUser { get; set; }
        public static int UserID => CurrentUser?.UserID ?? 0;
        public static string Login => CurrentUser?.Login ?? string.Empty;
        public static string Role => CurrentUser?.Role ?? string.Empty;

        public static bool IsAuthenticated => CurrentUser != null;
        public static bool IsAdmin => Role == "Администратор";
        public static bool IsIT => Role == "IT-специалист";
        public static bool IsUser => Role == "Пользователь";

    }
}
