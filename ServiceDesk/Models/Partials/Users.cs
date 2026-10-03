using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ServiceDesk.Models;

namespace ServiceDesk.Models
{
    public partial class Users
    {
        public string FullName
        {
            get
            {
                var parts = new[] { LastName, FirstName, Patronymic }
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .ToArray();

                return parts.Length > 0 ? string.Join(" ", parts) : "";
            }
        }
    }
}
