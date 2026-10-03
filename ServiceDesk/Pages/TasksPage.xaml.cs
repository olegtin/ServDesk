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
    public partial class TasksPage : Page
    {
        private List<PlannedTaskItem> _tasks = new List<PlannedTaskItem>();

        public TasksPage()
        {
            InitializeComponent();
            LoadTasks();
        }

        private void LoadTasks()
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    _tasks = context.Database.SqlQuery<PlannedTaskItem>(
                        @"SELECT t.TaskID, t.Title, t.Description, t.StartAt, t.EndAt, t.CreatedBy, t.AssignedTo,
                                 t.IsCompleted, t.CreatedAt,
                                 LTRIM(RTRIM(CONCAT(c.LastName, N' ', c.FirstName, N' ', ISNULL(c.Patronymic, N'')))) AS CreatedByName,
                                 LTRIM(RTRIM(CONCAT(a.LastName, N' ', a.FirstName, N' ', ISNULL(a.Patronymic, N'')))) AS AssignedToName
                          FROM dbo.PlannedTasks t
                          INNER JOIN dbo.Users c ON c.UserID = t.CreatedBy
                          LEFT JOIN dbo.Users a ON a.UserID = t.AssignedTo
                          WHERE t.CreatedBy = @UserID OR t.AssignedTo = @UserID OR @IsAdmin = 1
                          ORDER BY t.StartAt",
                        new SqlParameter("@UserID", Navigation.UserID),
                        new SqlParameter("@IsAdmin", Navigation.IsAdmin ? 1 : 0)).ToList();

                    ApplyFilter();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки задач: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            if (TasksGrid == null || StatusText == null)
                return;

            IEnumerable<PlannedTaskItem> filtered = _tasks;
            int selectedIndex = TaskFilterCombo?.SelectedIndex ?? 0;

            if (selectedIndex == 1)
                filtered = filtered.Where(t => !t.IsCompleted);
            else if (selectedIndex == 2)
                filtered = filtered.Where(t => t.IsCompleted);

            var result = filtered.ToList();
            TasksGrid.ItemsSource = result;
            StatusText.Text = $"Загружено задач: {result.Count}";
        }

        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.Navigate(new AddEditPages.AddEditTask());
        }

        private void CompleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (!(TasksGrid.SelectedItem is PlannedTaskItem task))
            {
                MessageBox.Show("Выберите задачу.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                using (var context = new RequestsEntities())
                {
                    context.Database.ExecuteSqlCommand(
                        @"UPDATE dbo.PlannedTasks
                          SET IsCompleted = 1, CompletedAt = SYSDATETIME()
                          WHERE TaskID = @TaskID",
                        new SqlParameter("@TaskID", task.TaskID));
                }

                LoadTasks();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления задачи: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteTask_Click(object sender, RoutedEventArgs e)
        {
            if (!(TasksGrid.SelectedItem is PlannedTaskItem task))
            {
                MessageBox.Show("Выберите задачу.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Удалить задачу \"{task.Title}\"?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var context = new RequestsEntities())
                {
                    context.Database.ExecuteSqlCommand(
                        "DELETE FROM dbo.PlannedTasks WHERE TaskID = @TaskID",
                        new SqlParameter("@TaskID", task.TaskID));
                }

                LoadTasks();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка удаления задачи: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void TaskFilterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.GoBack();
        }
    }
}
