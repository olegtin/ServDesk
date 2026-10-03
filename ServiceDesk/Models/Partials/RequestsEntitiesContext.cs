using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using ServiceDesk.Models;

namespace ServiceDesk.Models.Partials
{
    public partial class RequestsEntitiesContext
    {
        public static RequestsEntities GetContext() 
        { 
            return new RequestsEntities(); 
        }
    }
}
