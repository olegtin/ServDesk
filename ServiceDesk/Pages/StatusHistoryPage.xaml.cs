using ServiceDesk.Models;
using ServiceDesk.Models.Partials;
using System;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ServiceDesk.Pages
{
    public partial class StatusHistoryPage : Page
    {
        public StatusHistoryPage()
        {
            InitializeComponent();
            LoadHistory();
        }

        private void LoadHistory()
        {
            if (Navigation.IsUser)
            {
                StatusText.Text = "Нет доступа";
                return;
            }

            try
            {
                using (var context = new RequestsEntities())
                {
                    var rows = context.Database.SqlQuery<StatusHistoryItem>(
                        @"SELECT h.HistoryID, h.RequestID,
                                 ISNULL(oldS.StatusName, N'Создание заявки') AS OldStatusName,
                                 newS.StatusName AS NewStatusName,
                                 LTRIM(RTRIM(CONCAT(ch.LastName, N' ', ch.FirstName, N' ', ISNULL(ch.Patronymic, N'')))) AS ChangedByName,
                                 LTRIM(RTRIM(CONCAT(a.LastName, N' ', a.FirstName, N' ', ISNULL(a.Patronymic, N'')))) AS AssignedToName,
                                 h.ChangedAt, h.Comment
                          FROM dbo.StatusHistory h
                          INNER JOIN dbo.Requests r ON r.RequestID = h.RequestID
                          LEFT JOIN dbo.Statuses oldS ON oldS.StatusID = h.OldStatusID
                          INNER JOIN dbo.Statuses newS ON newS.StatusID = h.StatusID
                          INNER JOIN dbo.Users ch ON ch.UserID = h.ChangedBy
                          LEFT JOIN dbo.Users a ON a.UserID = r.AssignedTo
                          WHERE @IsAdmin = 1 OR r.AssignedTo = @UserID
                          ORDER BY h.ChangedAt DESC, h.HistoryID DESC",
                        new SqlParameter("@IsAdmin", Navigation.IsAdmin ? 1 : 0),
                        new SqlParameter("@UserID", Navigation.UserID)).ToList();

                    HistoryGrid.ItemsSource = rows;
                    StatusText.Text = $"Записей истории: {rows.Count}";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки истории статусов: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.GoBack();
        }
    }
}
