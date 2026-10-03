using ServiceDesk.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ServiceDesk
{

    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            FixIdentityGaps();
        }

        private void FixIdentityGaps()
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    var database = context.Database;

                    FixIdentityForTable(database, "Users", "UserID");

                    FixIdentityForTable(database, "Requests", "RequestID");
                }
            }
            catch (Exception ex)
            {

                System.Diagnostics.Debug.WriteLine($"Identity fix warning: {ex.Message}");
            }
        }

        private void FixIdentityForTable(System.Data.Entity.Database database, string tableName, string idColumn)
        {

            var maxId = database.SqlQuery<int?>($"SELECT MAX({idColumn}) FROM {tableName}").FirstOrDefault() ?? 0;

            database.ExecuteSqlCommand($"DBCC CHECKIDENT ('{tableName}', RESEED, {maxId})");
        }
    }
}
