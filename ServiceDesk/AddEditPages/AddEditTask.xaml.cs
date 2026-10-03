using ServiceDesk.Models;
using ServiceDesk.Models.Partials;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ServiceDesk.AddEditPages
{
    public partial class AddEditTask : Page
    {
        public AddEditTask()
        {
            InitializeComponent();
            StartDatePicker.SelectedDate = DateTime.Now.Date;
            StartTimeBox.Text = DateTime.Now.AddHours(1).ToString("HH:mm");
            LoadUsers();
            ApplyRoleUiRules();
        }

        private void LoadUsers()
        {
            using (var context = new RequestsEntities())
            {
                var users = Navigation.IsAdmin
                    ? context.Users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ToList()
                    : context.Users.Where(u => u.UserID == Navigation.UserID).ToList();

                if (Navigation.IsAdmin)
                    users.Insert(0, new Users { UserID = 0, LastName = "Без исполнителя" });

                AssignedToCombo.ItemsSource = users;
                AssignedToCombo.SelectedValue = Navigation.UserID;
                AssignedToCombo.IsEnabled = Navigation.IsAdmin;
            }
        }

        private void ApplyRoleUiRules()
        {
            if (Navigation.IsAdmin)
                return;

            if (AssignedToCombo.Parent is Grid grid)
            {
                foreach (UIElement child in grid.Children)
                {
                    if (Grid.GetRow(child) == 4)
                        child.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleBox.Text))
            {
                MessageBox.Show("Введите название задачи.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!StartDatePicker.SelectedDate.HasValue ||
                !TimeSpan.TryParseExact(StartTimeBox.Text.Trim(), "hh\\:mm", CultureInfo.InvariantCulture, out var time))
            {
                MessageBox.Show("Укажите дату и время в формате ЧЧ:ММ.", "Валидация", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var startAt = StartDatePicker.SelectedDate.Value.Date.Add(time);
            int? assignedTo = Navigation.IsAdmin
                ? (AssignedToCombo.SelectedValue is int id && id > 0 ? id : (int?)null)
                : Navigation.UserID;

            try
            {
                using (var context = new RequestsEntities())
                {
                    context.Database.ExecuteSqlCommand(
                        @"INSERT INTO dbo.PlannedTasks (Title, Description, StartAt, CreatedBy, AssignedTo)
                          VALUES (@Title, @Description, @StartAt, @CreatedBy, @AssignedTo)",
                        new SqlParameter("@Title", TitleBox.Text.Trim()),
                        new SqlParameter("@Description", (object)DescriptionBox.Text.Trim() ?? DBNull.Value),
                        new SqlParameter("@StartAt", startAt),
                        new SqlParameter("@CreatedBy", Navigation.UserID),
                        new SqlParameter("@AssignedTo", (object)assignedTo ?? DBNull.Value));

                    if (assignedTo.HasValue && assignedTo.Value != Navigation.UserID)
                    {
                        context.Database.ExecuteSqlCommand(
                            @"INSERT INTO dbo.Notifications (Title, Message, CreatedBy, TargetUserID)
                              VALUES (@Title, @Message, @CreatedBy, @TargetUserID)",
                            new SqlParameter("@Title", "Новая задача"),
                            new SqlParameter("@Message", $"Вам назначена задача: {TitleBox.Text.Trim()}"),
                            new SqlParameter("@CreatedBy", Navigation.UserID),
                            new SqlParameter("@TargetUserID", assignedTo.Value));
                    }
                }

                MessageBox.Show("Задача добавлена.", "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
                if (Navigation.MainFrame?.CanGoBack == true)
                    Navigation.MainFrame.GoBack();
                else
                    Navigation.MainFrame?.Navigate(new Pages.AdminDashboard());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка сохранения задачи: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame.GoBack();
        }
    }
}
