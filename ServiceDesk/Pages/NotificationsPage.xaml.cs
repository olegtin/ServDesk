using ServiceDesk.Models;
using ServiceDesk.Models.Partials;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ServiceDesk.Pages
{
    public partial class NotificationsPage : Page
    {
        private List<NotificationItem> _notifications = new List<NotificationItem>();

        public NotificationsPage()
        {
            InitializeComponent();
            if (!Navigation.IsAdmin && AddNotificationPanel != null)
                AddNotificationPanel.Visibility = Visibility.Collapsed;
            if (!Navigation.IsAdmin && DeleteNotificationButton != null)
                DeleteNotificationButton.Visibility = Visibility.Collapsed;

            LoadUsers();
            LoadNotifications();
        }

        private void LoadUsers()
        {
            using (var context = new RequestsEntities())
            {
                var users = context.Users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList();
                users.Insert(0, new Users { UserID = 0, LastName = "Все пользователи" });
                TargetUserCombo.ItemsSource = users;
                TargetUserCombo.SelectedValue = 0;
            }
        }

        private void LoadNotifications()
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    _notifications = context.Database.SqlQuery<NotificationItem>(
                        @"SELECT n.NotificationID, n.TargetUserID, n.CreatedBy, n.Title, n.Message, n.CreatedAt,
                                 CAST(CASE WHEN nr.NotificationID IS NULL THEN 0 ELSE 1 END AS bit) AS IsRead,
                                 LTRIM(RTRIM(CONCAT(c.LastName, N' ', c.FirstName, N' ', ISNULL(c.Patronymic, N'')))) AS CreatedByName,
                                 LTRIM(RTRIM(CONCAT(t.LastName, N' ', t.FirstName, N' ', ISNULL(t.Patronymic, N'')))) AS TargetUserName
                          FROM dbo.Notifications n
                          LEFT JOIN dbo.NotificationReads nr
                            ON nr.NotificationID = n.NotificationID AND nr.UserID = @UserID
                          LEFT JOIN dbo.Users c ON c.UserID = n.CreatedBy
                          LEFT JOIN dbo.Users t ON t.UserID = n.TargetUserID
                          WHERE n.TargetUserID IS NULL OR n.TargetUserID = @UserID OR @IsAdmin = 1
                          ORDER BY n.CreatedAt DESC",
                        new SqlParameter("@UserID", Navigation.UserID),
                        new SqlParameter("@IsAdmin", Navigation.IsAdmin ? 1 : 0)).ToList();

                    ApplyFilter();
                    UpdateTrayUnreadState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки уведомлений: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            if (NotificationsGrid == null || StatusText == null)
                return;

            IEnumerable<NotificationItem> filtered = _notifications;
            int selectedIndex = FilterCombo?.SelectedIndex ?? 0;

            if (selectedIndex == 1)
                filtered = filtered.Where(n => !n.IsRead);
            else if (selectedIndex == 2)
                filtered = filtered.Where(n => n.IsRead);

            var result = filtered.ToList();
            NotificationsGrid.ItemsSource = result;
            StatusText.Text = $"Уведомлений: {result.Count}";
        }

        private void AddNotification_Click(object sender, RoutedEventArgs e)
        {
            if (!Navigation.IsAdmin)
            {
                MessageBox.Show("Создавать уведомления может только администратор.", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(TitleBox.Text) || string.IsNullOrWhiteSpace(MessageTextBox.Text))
            {
                MessageBox.Show("Введите заголовок и сообщение.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int? targetUserId = TargetUserCombo.SelectedValue is int id && id > 0 ? id : (int?)null;

            try
            {
                using (var context = new RequestsEntities())
                {
                    context.Database.ExecuteSqlCommand(
                        @"INSERT INTO dbo.Notifications (Title, Message, CreatedBy, TargetUserID)
                          VALUES (@Title, @Message, @CreatedBy, @TargetUserID)",
                        new SqlParameter("@Title", TitleBox.Text.Trim()),
                        new SqlParameter("@Message", MessageTextBox.Text.Trim()),
                        new SqlParameter("@CreatedBy", Navigation.UserID),
                        new SqlParameter("@TargetUserID", (object)targetUserId ?? DBNull.Value));
                }

                TitleBox.Text = "";
                MessageTextBox.Text = "";
                TargetUserCombo.SelectedValue = 0;
                LoadNotifications();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка добавления уведомления: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void MarkSelectedRead_Click(object sender, RoutedEventArgs e)
        {
            var selected = NotificationsGrid.SelectedItems.Cast<NotificationItem>().ToList();
            if (!selected.Any())
            {
                MessageBox.Show("Выберите уведомления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MarkRead(selected.Select(n => n.NotificationID));
        }

        private void MarkAllRead_Click(object sender, RoutedEventArgs e)
        {
            MarkRead(_notifications.Where(n => !n.IsRead).Select(n => n.NotificationID));
        }

        private void MarkRead(IEnumerable<int> notificationIds)
        {
            var ids = notificationIds.Distinct().ToList();
            if (!ids.Any())
                return;

            try
            {
                using (var context = new RequestsEntities())
                {
                    foreach (int notificationId in ids)
                    {
                        context.Database.ExecuteSqlCommand(
                            @"IF NOT EXISTS (
                                  SELECT 1 FROM dbo.NotificationReads
                                  WHERE NotificationID = @NotificationID AND UserID = @UserID
                              )
                              AND EXISTS (
                                  SELECT 1 FROM dbo.Notifications
                                  WHERE NotificationID = @NotificationID
                              )
                              INSERT INTO dbo.NotificationReads (NotificationID, UserID)
                              VALUES (@NotificationID, @UserID)",
                            new SqlParameter("@NotificationID", notificationId),
                            new SqlParameter("@UserID", Navigation.UserID));
                    }
                }

                LoadNotifications();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления уведомлений: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateTrayUnreadState()
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    int unreadCount = context.Database.SqlQuery<int>(
                        @"SELECT COUNT(*)
                          FROM dbo.Notifications n
                          LEFT JOIN dbo.NotificationReads nr
                            ON nr.NotificationID = n.NotificationID AND nr.UserID = @UserID
                          WHERE nr.NotificationID IS NULL
                            AND (n.TargetUserID IS NULL OR n.TargetUserID = @UserID)",
                        new SqlParameter("@UserID", Navigation.UserID)).FirstOrDefault();

                    if (Application.Current.MainWindow is MainWindow mainWindow)
                        mainWindow.SetTrayUnreadState(unreadCount > 0);
                }
            }
            catch
            {
            }
        }

        private void DeleteSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!Navigation.IsAdmin)
            {
                MessageBox.Show("Удалять уведомления может только администратор.", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var selected = NotificationsGrid.SelectedItems.Cast<NotificationItem>().ToList();
            if (!selected.Any())
            {
                MessageBox.Show("Выберите уведомления.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Удалить {selected.Count} уведомлений?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var context = new RequestsEntities())
                {
                    foreach (var notification in selected)
                    {
                        context.Database.ExecuteSqlCommand(
                            "DELETE FROM dbo.Notifications WHERE NotificationID = @NotificationID",
                            new SqlParameter("@NotificationID", notification.NotificationID));
                    }
                }

                LoadNotifications();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка удаления уведомлений: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void FilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.GoBack();
        }
    }
}
