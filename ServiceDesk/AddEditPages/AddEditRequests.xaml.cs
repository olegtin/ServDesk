using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using ServiceDesk.Models;
using ServiceDesk.Models.Partials;

namespace ServiceDesk.AddEditPages
{
    /// <summary>
    /// Логика взаимодействия для AddEditRequests.xaml
    /// </summary>
    public partial class AddEditRequests : Page
    {
        public ObservableCollection<Categories> CategoriesList { get; set; }
        public ObservableCollection<Statuses> StatusesList { get; set; }
        public ObservableCollection<Users> UsersList { get; set; }
        public ObservableCollection<Users> SpecialistsList { get; set; }
        private ServiceDesk.Models.Requests _currentRequest;
        bool isEdit = false;


        public AddEditRequests(ServiceDesk.Models.Requests CurrentReq)
        {
            InitializeComponent();

            if (CurrentReq != null)
            {
                _currentRequest = CurrentReq;
                isEdit = true;
            }

            LoadReferenceData();
            DataContext = _currentRequest;
            UpdatedDate.IsEnabled = false;
            UpdatedDateLabel.Visibility = Visibility.Visible;
            UpdatedDate.Visibility = Visibility.Visible;
        }
        public AddEditRequests()
        {
            InitializeComponent();
            _currentRequest = new ServiceDesk.Models.Requests
            {
                StatusID = 1,
                CreatedDate = DateTime.Now,
                CreatedBy = Navigation.UserID
            };
            LoadReferenceData(); // ← загружаем справочники
            DataContext = _currentRequest;
            isEdit = false;
            UpdatedDate.IsEnabled = false;
            UpdatedDateLabel.Visibility = Visibility.Collapsed;
            UpdatedDate.Visibility = Visibility.Collapsed;
        }
        private void LoadReferenceData()
        {
            using (var context = new RequestsEntities())
            {
                CategoriesList = new ObservableCollection<Categories>(
                    context.Categories.ToList()
                );
                var statuses = context.Statuses.ToList();
                if (!Navigation.IsAdmin)
                    statuses = statuses.Where(s => s.StatusID != 5 && s.StatusID != 6).ToList();
                StatusesList = new ObservableCollection<Statuses>(statuses);
                var requestAuthors = context.Users
                    .OrderBy(p => p.UserID)
                    .ToList();
                UsersList = new ObservableCollection<Users>(requestAuthors);
                SpecialistsList = new ObservableCollection<Users>(
                    context.Users.OrderBy(p => p.UserID).Where(r => r.Role == "IT-специалист").ToList()
                );
                if (Navigation.IsUser || Navigation.IsIT)
                {
                    if (!isEdit)
                        _currentRequest.CreatedBy = Navigation.UserID;
                    ComboCreatedBy.IsEnabled = false;
                }

                if (Navigation.IsUser)
                {
                    ComboAssignedTo.IsEnabled = false;
                    ComboStatus.IsEnabled = false;
                    CreatedDate.IsEnabled = false;
                    UpdatedDate.IsEnabled = false;
                }

            }

            ComboCategory.ItemsSource = CategoriesList;
            ComboStatus.ItemsSource = StatusesList;
            ComboCreatedBy.ItemsSource = UsersList;
            ComboAssignedTo.ItemsSource = SpecialistsList;
            ApplyRoleUiRules();
        }

        private void ApplyRoleUiRules()
        {
            bool isOwnRequest = _currentRequest?.CreatedBy == Navigation.UserID;

            if ((Navigation.IsUser || Navigation.IsIT) && isEdit)
            {
                ComboAssignedTo.IsEnabled = false;
            }

            if ((Navigation.IsUser || Navigation.IsIT) && !isEdit)
            {
                ComboAssignedTo.IsEnabled = false;
                ComboStatus.IsEnabled = false;
                CreatedDate.IsEnabled = false;
                UpdatedDate.IsEnabled = false;
                ComboAssignedTo.Opacity = 0.55;
                ComboStatus.Opacity = 0.55;
                CreatedDate.Opacity = 0.55;
                UpdatedDate.Opacity = 0.55;
            }

            if (Navigation.IsIT && isEdit && !isOwnRequest)
            {
                ComboCreatedBy.IsEnabled = false;
                ComboAssignedTo.IsEnabled = false;
                ComboCategory.IsEnabled = false;
                Description.IsEnabled = false;
                CreatedDate.IsEnabled = false;
                UpdatedDate.IsEnabled = false;
                ComboStatus.IsEnabled = true;
                ComboCreatedBy.Opacity = 0.55;
                ComboAssignedTo.Opacity = 0.55;
                ComboCategory.Opacity = 0.55;
                Description.Opacity = 0.55;
                CreatedDate.Opacity = 0.55;
                UpdatedDate.Opacity = 0.55;
            }

            if (Navigation.IsIT && isEdit && isOwnRequest)
            {
                ComboStatus.IsEnabled = false;
                CreatedDate.IsEnabled = false;
                UpdatedDate.IsEnabled = false;
                ComboAssignedTo.Opacity = 0.55;
                ComboStatus.Opacity = 0.55;
                CreatedDate.Opacity = 0.55;
                UpdatedDate.Opacity = 0.55;
            }
        }
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            int? oldStatusId = null;
            int? oldAssignedTo = null;
            int? createdBy = null;
            int? originalAssignedTo = null;
            int? originalCategoryId = null;
            string originalDescription = null;
            DateTime? originalCreatedDate = null;
            bool? originalIsDeleted = null;
            DateTime? originalDeletedAt = null;
            int? originalDeletedBy = null;
            if (isEdit && _currentRequest.RequestID > 0)
            {
                using (var tempContext = new RequestsEntities())
                {
                    var original = tempContext.Requests
                        .AsNoTracking() // 🔹 Важно: не отслеживать, чтобы не было конфликтов
                        .FirstOrDefault(r => r.RequestID == _currentRequest.RequestID);
                    if (original != null)
                    {
                        oldStatusId = original.StatusID;
                        oldAssignedTo = original.AssignedTo;
                        createdBy = original.CreatedBy;
                        originalAssignedTo = original.AssignedTo;
                        originalCategoryId = original.CategoryID;
                        originalDescription = original.Description;
                        originalCreatedDate = original.CreatedDate;
                        originalIsDeleted = original.IsDeleted;
                        originalDeletedAt = original.DeletedAt;
                        originalDeletedBy = original.DeletedBy;
                    }
                }
            }
            _currentRequest.Categories = null;
            _currentRequest.Statuses = null;
            _currentRequest.Users = null;   
            _currentRequest.Users1 = null;     
            _currentRequest.StatusHistory = null;

            UpdatedDate.IsEnabled = true;
            _currentRequest.UpdatedDate = DateTime.Now;

            if (string.IsNullOrWhiteSpace(Description.Text))
            {
                MessageBox.Show("Описание заявки не может быть пустым", "Ошибка валидации",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentRequest.CategoryID <= 0)
            {
                MessageBox.Show("Выберите категорию", "Ошибка валидации",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentRequest.StatusID <= 0)
            {
                MessageBox.Show("Выберите статус", "Ошибка валидации",
                               MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                using (var context = new RequestsEntities())
                {
                    if (!isEdit && !_currentRequest.AssignedTo.HasValue)
                    {
                        _currentRequest.AssignedTo = FindLeastLoadedSpecialistId(context);
                    }

                    if (_currentRequest.AssignedTo.HasValue && _currentRequest.StatusID == 1)
                    {
                        _currentRequest.StatusID = 2;
                    }

                    if (isEdit)
                    {
                        if (Navigation.IsIT && createdBy != Navigation.UserID)
                        {
                            _currentRequest.CreatedBy = createdBy ?? _currentRequest.CreatedBy;
                            _currentRequest.AssignedTo = originalAssignedTo;
                            _currentRequest.CategoryID = originalCategoryId ?? _currentRequest.CategoryID;
                            _currentRequest.Description = originalDescription;
                            _currentRequest.CreatedDate = originalCreatedDate ?? _currentRequest.CreatedDate;
                            _currentRequest.IsDeleted = originalIsDeleted ?? _currentRequest.IsDeleted;
                            _currentRequest.DeletedAt = originalDeletedAt;
                            _currentRequest.DeletedBy = originalDeletedBy;
                        }

                        if (_currentRequest.StatusID == 5)
                        {
                            _currentRequest.IsDeleted = true;
                            _currentRequest.DeletedAt = DateTime.Now;
                            _currentRequest.DeletedBy = Navigation.UserID;
                        }

                        context.Requests.Attach(_currentRequest);
                        var entry = context.Entry(_currentRequest);
                        entry.State = System.Data.Entity.EntityState.Modified;

                        if (oldStatusId.HasValue && oldStatusId.Value != _currentRequest.StatusID)
                        {
                            context.StatusHistory.Add(new StatusHistory
                            {
                                RequestID = _currentRequest.RequestID,
                                OldStatusID = oldStatusId.Value,
                                StatusID = _currentRequest.StatusID,
                                ChangedBy = Navigation.UserID,
                                ChangedAt = DateTime.Now,
                                Comment = $"Статус изменён через форму редактирования"
                            });
                        }
                        context.SaveChanges();

                        if (_currentRequest.StatusID == 5)
                            DeleteRequestNotifications(context, _currentRequest.RequestID);

                        if (oldStatusId.HasValue && oldStatusId.Value != _currentRequest.StatusID && createdBy.HasValue && _currentRequest.StatusID != 5)
                            CreateStatusChangeNotification(context, _currentRequest.RequestID, createdBy.Value, oldStatusId.Value, _currentRequest.StatusID);

                        if (oldAssignedTo != _currentRequest.AssignedTo && _currentRequest.AssignedTo.HasValue)
                            CreateAssignedNotification(context, _currentRequest.RequestID, _currentRequest.AssignedTo.Value);
                    }
                    else
                    {
                        context.Requests.Add(_currentRequest);
                        context.SaveChanges();

                        context.StatusHistory.Add(new StatusHistory
                        {
                            RequestID = _currentRequest.RequestID, 
                            OldStatusID = null,
                            StatusID = _currentRequest.StatusID,
                            ChangedBy = Navigation.UserID,
                            ChangedAt = DateTime.Now,
                            Comment = "Заявка создана"
                        });

                        if (_currentRequest.AssignedTo.HasValue)
                            CreateAssignedNotification(context, _currentRequest.RequestID, _currentRequest.AssignedTo.Value);

                        if (_currentRequest.StatusID == 2)
                            CreateStatusChangeNotification(context, _currentRequest.RequestID, _currentRequest.CreatedBy, 1, 2);
                    }
                    context.SaveChanges();
                    MessageBox.Show("Информация сохранена");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
                Console.WriteLine(ex);
                return;
            }

            if (Navigation.MainFrame?.CanGoBack == true)
                Navigation.MainFrame.GoBack();
            else
                Navigation.MainFrame?.Navigate(new Pages.AdminDashboard());
        }

        private int? FindLeastLoadedSpecialistId(RequestsEntities context)
        {
            var closedStatusId = context.Statuses
                .Where(s => s.StatusName == "Закрыта")
                .Select(s => (int?)s.StatusID)
                .FirstOrDefault();

            var specialists = context.Users
                .Where(u => u.Role == "IT-специалист")
                .Select(u => new
                {
                    u.UserID,
                    ActiveRequests = context.Requests.Count(r =>
                        r.AssignedTo == u.UserID &&
                        r.IsDeleted == false &&
                        r.StatusID != 4 &&
                        (!closedStatusId.HasValue || r.StatusID != closedStatusId.Value))
                })
                .Where(u => u.UserID != _currentRequest.CreatedBy)
                .OrderBy(u => u.ActiveRequests)
                .ThenBy(u => u.UserID)
                .ToList();

            if (specialists.Any())
                return specialists.First().UserID;

            return context.Users
                .Where(u => u.Role == "IT-специалист")
                .OrderBy(u => u.UserID)
                .Select(u => (int?)u.UserID)
                .FirstOrDefault();
        }

        private void CreateStatusChangeNotification(RequestsEntities context, int requestId, int targetUserId, int oldStatusId, int newStatusId)
        {
            string oldStatus = context.Statuses.FirstOrDefault(s => s.StatusID == oldStatusId)?.StatusName ?? oldStatusId.ToString();
            string newStatus = context.Statuses.FirstOrDefault(s => s.StatusID == newStatusId)?.StatusName ?? newStatusId.ToString();

            context.Database.ExecuteSqlCommand(
                @"INSERT INTO dbo.Notifications (Title, Message, CreatedBy, TargetUserID)
                  VALUES (@Title, @Message, @CreatedBy, @TargetUserID)",
                new SqlParameter("@Title", $"Изменен статус заявки #{requestId}"),
                new SqlParameter("@Message", $"Статус вашей заявки изменен: {oldStatus} -> {newStatus}."),
                new SqlParameter("@CreatedBy", Navigation.UserID),
                new SqlParameter("@TargetUserID", targetUserId));
        }

        private void CreateAssignedNotification(RequestsEntities context, int requestId, int targetUserId)
        {
            context.Database.ExecuteSqlCommand(
                @"INSERT INTO dbo.Notifications (Title, Message, CreatedBy, TargetUserID)
                  VALUES (@Title, @Message, @CreatedBy, @TargetUserID)",
                new SqlParameter("@Title", $"Назначена заявка #{requestId}"),
                new SqlParameter("@Message", $"Вам назначена новая заявка #{requestId}."),
                new SqlParameter("@CreatedBy", Navigation.UserID),
                new SqlParameter("@TargetUserID", targetUserId));
        }

        private void DeleteRequestNotifications(RequestsEntities context, int requestId)
        {
            var requestMarker = $"#{requestId}";
            context.Database.ExecuteSqlCommand(
                @"DELETE FROM dbo.Notifications
                  WHERE Title LIKE @Marker OR Message LIKE @Marker",
                new SqlParameter("@Marker", "%" + requestMarker + "%"));
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame.GoBack();
        }
    }
}
