using ServiceDesk.Models;
using ServiceDesk.Models.Partials;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Security;
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
using System.Windows.Threading;

namespace ServiceDesk.Pages
{
    /// <summary>
    /// Логика взаимодействия для AdminDashboard.xaml
    /// </summary>
    public partial class AdminDashboard : Page
    {
        //Поля Статистики
        private bool _isStatsTabInitialized = false;
        private int _statsTotalRequests = 0;
        private int _statsInProgressRequests = 0;
        private int _statsCompletedRequests = 0;
        private int _statsImportantRequests = 0;
        private int _statsUsersCount = 0;
        private DateTime? _statsDateFrom;
        private DateTime? _statsDateTo;
        //Поля Заявок
        private ServiceDesk.Models.Requests _selectedRequest;
        private int? _selectedStatusId = null;
        private DispatcherTimer _searchTimer;
        private const int DebounceDelayMs = 300;
        private List<ServiceDesk.Models.Requests> _allRequests;
        private int _itemcount = 0;
        private DateTime? _dateFrom;
        private DateTime? _dateTo;
        private int _searchFieldIndex = 0;
        private bool _isRequestsTabInitialized = false;
        private bool _sortRequestsByNewest = false;
        //Поля Пользователей
        private ServiceDesk.Models.Users _selectedUser;
        private DispatcherTimer _userSearchTimer;
        private const int UserDebounceDelayMs = 300;
        private List<ServiceDesk.Models.Users> _allUsers;
        private int _userItemCount = 0;
        private int _userSearchFieldIndex = 0;
        private bool _isUsersTabInitialized = false;
        //Поля Аналитики
        private bool _isAnalyticsTabInitialized = false;
        private RequestStats _currentStats;
        private List<CategoryLoad> _categoryLoads;
        private int? _selectedAnalyticsSpecialistId = null;
        private DateTime? _analyticsDateFrom;
        private DateTime? _analyticsDateTo;
        private DateTime? _analyticsSummaryDateFrom;
        private DateTime? _analyticsSummaryDateTo;
        //Поля планирования и уведомлений
        private bool _isCalendarTabInitialized = false;
        private DispatcherTimer _notificationsTimer;
        private int _tasksDisplayCount = 5;
        private int _notificationsDisplayCount = 5;
        private int _lastShownNotificationId = 0;
        private bool _notificationsStarted = false;
        public AdminDashboard()
        {
            InitializeComponent();
            if (Navigation.IsUser)
            {
                UsersTab.Visibility = Visibility.Collapsed;
                Statictics.Visibility = Visibility.Collapsed;
                ReqDelBtn.Visibility = Visibility.Collapsed;
                Analitics.Visibility = Visibility.Collapsed;
                EventTest.Visibility = Visibility.Collapsed;
                if (RequestsExportBtn != null)
                    RequestsExportBtn.Visibility = Visibility.Collapsed;
                if (DashboardTabs != null)
                    DashboardTabs.SelectedItem = MainTabControl;
            }
            if (Navigation.IsUser && StatusHistoryBtn != null)
                StatusHistoryBtn.Visibility = Visibility.Collapsed;
            if (Navigation.IsIT)
            {
                if (ReqDelBtn != null)
                    ReqDelBtn.Visibility = Visibility.Collapsed;
                if (ReqEditBtn != null)
                {
                    ReqEditBtn.Content = "Изменить статус";
                    ReqEditBtn.Width = 160;
                }
            }
            if (!Navigation.IsUser && ReturnToWorkBtn != null)
                ReturnToWorkBtn.Visibility = Visibility.Collapsed;
            if (AssignedToMeCheckBox != null)
            {

                AssignedToMeCheckBox.Visibility = Navigation.IsIT ? Visibility.Visible : Visibility.Collapsed;
                AssignedToMeCheckBox.IsChecked = true;
            }

            Loaded += AdminDashboard_Loaded;
            Unloaded += AdminDashboard_Unloaded;
        }

        private void AdminDashboard_Loaded(object sender, RoutedEventArgs e)
        {
            if (_notificationsStarted)
            {
                RefreshActiveTab();
                EnsureNotificationsTimerRunning();
                return;
            }

            _notificationsStarted = true;
            AutoCloseCompletedRequests();
            Dispatcher.BeginInvoke(new Action(InitNotificationsTimer), DispatcherPriority.ApplicationIdle);
        }

        private void AdminDashboard_Unloaded(object sender, RoutedEventArgs e)
        {
            StopNotificationsTimer();
        }

        private void RefreshActiveTab()
        {
            if (DashboardTabs?.SelectedItem is TabItem selectedTab)
            {
                switch (selectedTab.Name)
                {
                    case "Statictics":
                        if (_isStatsTabInitialized)
                            LoadStatsData();
                        break;
                    case "MainTabControl":
                        if (_isRequestsTabInitialized)
                            LoadAllRequests();
                        break;
                    case "UsersTab":
                        if (_isUsersTabInitialized)
                            LoadAllUsers();
                        break;
                    case "Analitics":
                        if (_isAnalyticsTabInitialized)
                            LoadAnalyticsData();
                        break;
                    case "EventTest":
                        if (_isCalendarTabInitialized)
                        {
                            LoadUpcomingTasks();
                            LoadDashboardNotifications();
                        }
                        break;
                }
            }
        }

        private void InitNotificationsTimer()
        {
            InitializeNotificationBaseline();

            if (ToastBorder != null)
                ToastBorder.Visibility = Visibility.Collapsed;

            _notificationsTimer = new DispatcherTimer();
            _notificationsTimer.Interval = TimeSpan.FromSeconds(30);
            _notificationsTimer.Tick += (s, e) => UpdateUnreadNotificationsCount();
            _notificationsTimer.Start();
            UpdateUnreadNotificationsCount();
        }

        private void EnsureNotificationsTimerRunning()
        {
            if (_notificationsTimer == null)
            {
                _notificationsTimer = new DispatcherTimer();
                _notificationsTimer.Interval = TimeSpan.FromSeconds(30);
                _notificationsTimer.Tick += (s, e) => UpdateUnreadNotificationsCount();
            }

            if (!_notificationsTimer.IsEnabled)
                _notificationsTimer.Start();

            UpdateUnreadNotificationsCount();
        }

        private void StopNotificationsTimer()
        {
            if (_notificationsTimer != null)
                _notificationsTimer.Stop();
        }
        private void StatsTab_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isStatsTabInitialized)
            {
                LoadStatsData();
                _isStatsTabInitialized = true;
            }
        }

        private void RefreshStats_Click(object sender, RoutedEventArgs e)
        {
            LoadStatsData();
        }

        private void PeriodComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatsCustomPeriodVisibility();

            if (_isStatsTabInitialized)
                LoadStatsData();
        }

        private void StatsDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _statsDateFrom = StatsDatePickerFrom?.SelectedDate;
            _statsDateTo = StatsDatePickerTo?.SelectedDate;

            if (_isStatsTabInitialized && IsStatsCustomPeriodSelected())
                LoadStatsData();
        }
        private void LoadStatsData()
        {
            try
            {

                var periodRange = GetStatsPeriodRange();

                using (var context = new RequestsEntities())
                {
                    var completedStatusIds = context.Statuses
                        .Where(s => s.StatusName == "Выполнена" || s.StatusName == "Закрыта")
                        .Select(s => s.StatusID)
                        .ToList();
                    if (!completedStatusIds.Any())
                        completedStatusIds.Add(4);

                    var query = context.Requests.AsQueryable().Where(r => r.IsDeleted == false);

                    if (periodRange.DateFrom.HasValue)
                    {
                        query = query.Where(r => r.CreatedDate >= periodRange.DateFrom.Value);
                    }

                    if (periodRange.DateTo.HasValue)
                    {
                        var dateToExclusive = periodRange.DateTo.Value.Date.AddDays(1);
                        query = query.Where(r => r.CreatedDate < dateToExclusive);
                    }

                    var requests = query.ToList();

                    _statsTotalRequests = requests.Count;
                    _statsInProgressRequests = requests.Count(r => r.StatusID == 2); // В работе
                    _statsCompletedRequests = requests.Count(r => completedStatusIds.Contains(r.StatusID));

                    _statsImportantRequests = requests.Count(r =>
                    !completedStatusIds.Contains(r.StatusID) &&
                    r.UpdatedDate < DateTime.Now.AddDays(-7));

                    _statsUsersCount = context.Users.Count();

                    UpdateStatsUI();

                    var recentRequests = query
                        .OrderByDescending(r => r.UpdatedDate)
                        .ThenByDescending(r => r.CreatedDate)
                        .ThenByDescending(r => r.RequestID)
                        .Take(10)
                        .ToList();

                    LoadRecentActivities(recentRequests);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки статистики: " + ex.Message, "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        public class ActivityItem
        {
            public string Description { get; set; }
            public string Time { get; set; }
            public string User { get; set; }
        }

        private void LoadRecentActivities(List<ServiceDesk.Models.Requests> recentRequests)
        {
            var activities = recentRequests.Where(r => r.IsDeleted == false).Select(r => new ActivityItem
            {
                Description = $"Заявка #{r.RequestID}: {BuildActivityDescription(r.Description)}",
                Time = (r.UpdatedDate != default(DateTime) ? r.UpdatedDate : r.CreatedDate).ToString("dd.MM HH:mm"),
                User = !string.IsNullOrWhiteSpace(r.Users1?.FullName) ? r.Users1.FullName : $"Пользователь #{r.CreatedBy}"
            }).ToList();

            Dispatcher.Invoke(() =>
            {
                if (ActivitiesListBox != null)
                    ActivitiesListBox.ItemsSource = activities;
            });
        }

        private string BuildActivityDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
                return "Без описания";

            const int maxLength = 50;
            if (description.Length <= maxLength)
                return description;

            return description.Substring(0, maxLength) + "...";
        }

        private (DateTime? DateFrom, DateTime? DateTo) GetStatsPeriodRange()
        {
            if (PeriodComboBox?.SelectedItem is ComboBoxItem periodItem)
            {
                var content = periodItem.Content?.ToString();

                switch (content)
                {
                    case "Сегодня":
                        return (DateTime.Now.Date, null);

                    case "Эта неделя":

                        var daysToMonday = (int)DateTime.Now.DayOfWeek - (int)DayOfWeek.Monday;
                        if (daysToMonday < 0) daysToMonday += 7;
                        return (DateTime.Now.Date.AddDays(-daysToMonday), null);

                    case "Этот месяц":
                        return (new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1), null);

                    case "Этот год":
                        return (new DateTime(DateTime.Now.Year, 1, 1), null);

                    case "Свой период":
                        return (_statsDateFrom?.Date, _statsDateTo?.Date);

                    case "Все время":
                        return (null, null);

                    default:
                        return (null, null); 
                }
            }

            return (null, null);
        }

        private void UpdateStatsCustomPeriodVisibility()
        {
            if (StatsCustomPeriodPanel != null)
                StatsCustomPeriodPanel.Visibility = IsStatsCustomPeriodSelected() ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool IsStatsCustomPeriodSelected()
        {
            return (PeriodComboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Свой период";
        }

        private void UpdateStatsUI()
        {
            Dispatcher.Invoke(() =>
            {
                if (TotalRequestsText != null)
                    TotalRequestsText.Text = _statsTotalRequests.ToString();

                if (InProgressText != null)
                    InProgressText.Text = _statsInProgressRequests.ToString();

                if (CompletedText != null)
                    CompletedText.Text = _statsCompletedRequests.ToString();

                if (CompletedPercentText != null)
                {
                    var percent = _statsTotalRequests > 0
                        ? (double)_statsCompletedRequests / _statsTotalRequests * 100
                        : 0;
                    CompletedPercentText.Text = $"{percent:F1}%";
                }

                if (OverdueText != null)
                    OverdueText.Text = _statsImportantRequests.ToString();

                if (UsersCountText != null)
                    UsersCountText.Text = _statsUsersCount.ToString();
            });
        }
        private void RequestsTab_Loaded(object sender, RoutedEventArgs e)
        {

            if (!_isRequestsTabInitialized)
            {
                InitRequestsTab();
                _isRequestsTabInitialized = true;
            }
        }

        private void InitRequestsTab()
        {

            LoadAllRequests();

            if (ComboSearch != null)
                ComboSearch.SelectionChanged += ComboSearch_SelectionChanged;

            if (StatusFilter != null)
                StatusFilter.SelectionChanged += StatusFilter_SelectionChanged;

            if (DatePickerFrom != null)
                DatePickerFrom.SelectedDateChanged += DatePickerFrom_SelectedDateChanged;

            if (DatePickerTo != null)
                DatePickerTo.SelectedDateChanged += DatePickerTo_SelectedDateChanged;

            _searchTimer = new DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(DebounceDelayMs);
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                ApplyFilter();
            };

            if (TBoxSearch != null)
            {
                TBoxSearch.GotFocus += (s, e) => { if (TBoxSearch.Text == "🔍 Поиск...") TBoxSearch.Text = ""; };
                TBoxSearch.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(TBoxSearch.Text)) TBoxSearch.Text = "🔍 Поиск..."; };
                TBoxSearch.TextChanged += TBoxSearchTextChanged;
            }
        }

        private void UsersTab_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isUsersTabInitialized)
            {
                InitUsersTab();
                _isUsersTabInitialized = true;
            }
        }
        private void InitUsersTab()
        {
            LoadAllUsers();
            UpdateUsersUIByRole();
            UserSearchBox.Text = "🔍 Поиск...";
            _userSearchTimer = new DispatcherTimer();
            _userSearchTimer.Interval = TimeSpan.FromMilliseconds(UserDebounceDelayMs);
            _userSearchTimer.Tick += (s, e) =>
            {
                _userSearchTimer.Stop();
                ApplyUserFilter();
            };

            if (UserSearchBox != null)
            {
                UserSearchBox.GotFocus += (s, e) => { if (UserSearchBox.Text == "🔍 Поиск...") UserSearchBox.Text = ""; };
                UserSearchBox.LostFocus += (s, e) => { if (string.IsNullOrWhiteSpace(UserSearchBox.Text)) UserSearchBox.Text = "🔍 Поиск..."; };
            }
        }

        private void UpdateUsersUIByRole()
        {
            if (UserBtnAdd == null || UserBtnEdit == null || UserBtnDelete == null)
                return;

            if (Navigation.IsAdmin)
            {
                UserBtnAdd.Visibility = Visibility.Visible;
                UserBtnEdit.Visibility = Visibility.Visible;
                UserBtnDelete.Visibility = Visibility.Visible;
            }
            else
            {
                UserBtnAdd.Visibility = Visibility.Collapsed;
                UserBtnEdit.Visibility = Visibility.Collapsed;
                UserBtnDelete.Visibility = Visibility.Collapsed;
            }
        }
        private void LoadAllUsers()
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    _allUsers = context.Users.ToList();
                }

                _userItemCount = _allUsers?.Count ?? 0;
                ApplyUserFilter();

                if (UserSearchCombo?.Items.Count > 0) UserSearchCombo.SelectedIndex = 0;

                UpdateUserStatus($"Найдено {_userItemCount} записей");
            }
            catch (Exception ex)
            {
                _allUsers = new List<ServiceDesk.Models.Users>();
                UpdateUserStatus($"Ошибка: {ex.Message}");
                MessageBox.Show("Не удалось загрузить пользователей: " + ex.Message, "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void ApplyUserFilter()
        {

            if (_allUsers == null || UsersDataGrid == null || UserInfoText == null)
                return;

            var filtered = _allUsers.AsEnumerable();

            var searchText = UserSearchBox?.Text?.Trim() ?? "";
            if (!string.IsNullOrWhiteSpace(searchText) && searchText != "🔍 Поиск..." && _userSearchFieldIndex >= 0)
            {
                searchText = searchText.ToLower();
                switch (_userSearchFieldIndex)
                {
                    case 0: 
                        if (int.TryParse(searchText, out int id))
                            filtered = filtered.Where(p => p.UserID == id);
                        break;
                    case 1:
                        filtered = filtered.Where(p => p.LastName?.ToLower().Contains(searchText) == true);
                        break;
                    case 2:
                        filtered = filtered.Where(p => p.FirstName?.ToLower().Contains(searchText) == true);
                        break;
                    case 3:
                        filtered = filtered.Where(p => p.Patronymic?.ToLower().Contains(searchText) == true);
                        break;
                    case 4:
                        filtered = filtered.Where(p => p.Department?.ToLower().Contains(searchText) == true);
                        break;
                    case 5:
                        filtered = filtered.Where(p => p.Role?.ToLower().Contains(searchText) == true);
                        break;
                    case 6:
                        filtered = filtered.Where(p => p.Email?.ToLower().Contains(searchText) == true);
                        break;
                    case 7:
                        filtered = filtered.Where(p => p.TelephoneNum?.ToLower().Contains(searchText) == true);
                        break;
                }
            }

            var result = filtered?.OrderBy(p => p.UserID).ToList() ?? new List<ServiceDesk.Models.Users>();
            UsersDataGrid.ItemsSource = result;
            UserInfoText.Text = $"Найдено: {result.Count} из {_userItemCount} записей";
        }
        private void AnalyticsTab_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isAnalyticsTabInitialized)
            {
                LoadAnalyticsSpecialists();
                UpdateAnalyticsCustomPeriodVisibility();
                UpdateAnalyticsSummaryCustomPeriodVisibility();
                LoadAnalyticsData();
                _isAnalyticsTabInitialized = true;
            }
        }

        private void LoadAnalyticsSpecialists()
        {
            if (AnalyticsSpecialistCombo == null)
                return;

            try
            {
                using (var context = new RequestsEntities())
                {
                    var specialists = context.Users
                        .Where(u => u.Role == "IT-специалист")
                        .OrderBy(u => u.LastName)
                        .ThenBy(u => u.FirstName)
                        .ToList();

                    AnalyticsSpecialistCombo.ItemsSource = specialists;
                    AnalyticsSpecialistCombo.IsEnabled = Navigation.IsAdmin;

                    var selected = specialists.FirstOrDefault(u => u.UserID == Navigation.UserID)
                        ?? specialists.FirstOrDefault();

                    if (selected != null)
                    {
                        AnalyticsSpecialistCombo.SelectedValue = selected.UserID;
                        _selectedAnalyticsSpecialistId = selected.UserID;
                    }
                    else
                    {
                        _selectedAnalyticsSpecialistId = null;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки списка специалистов: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RefreshAnalytics_Click(object sender, RoutedEventArgs e)
        {
            LoadAnalyticsData();
        }

        private void AnalyticsPeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAnalyticsCustomPeriodVisibility();

            if (_isAnalyticsTabInitialized)
                LoadAnalyticsData();
        }

        private void AnalyticsDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _analyticsDateFrom = AnalyticsDatePickerFrom?.SelectedDate;
            _analyticsDateTo = AnalyticsDatePickerTo?.SelectedDate;

            if (_isAnalyticsTabInitialized && IsAnalyticsCustomPeriodSelected())
                LoadAnalyticsData();
        }

        private void AnalyticsSummaryPeriodCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAnalyticsSummaryCustomPeriodVisibility();

            if (_isAnalyticsTabInitialized)
                LoadAnalyticsData();
        }

        private void AnalyticsSummaryDatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _analyticsSummaryDateFrom = AnalyticsSummaryDatePickerFrom?.SelectedDate;
            _analyticsSummaryDateTo = AnalyticsSummaryDatePickerTo?.SelectedDate;

            if (_isAnalyticsTabInitialized && IsAnalyticsSummaryCustomPeriodSelected())
                LoadAnalyticsData();
        }

        private void AnalyticsSpecialistCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AnalyticsSpecialistCombo?.SelectedValue is int userId)
                _selectedAnalyticsSpecialistId = userId;
            else if (AnalyticsSpecialistCombo?.SelectedItem is ServiceDesk.Models.Users user)
                _selectedAnalyticsSpecialistId = user.UserID;
            else
                _selectedAnalyticsSpecialistId = null;

            if (_isAnalyticsTabInitialized)
                LoadAnalyticsData();
        }

        private void LoadAnalyticsData()
        {
            try
            {

                var specialistPeriodRange = GetAnalyticsPeriodRange();
                var summaryPeriodRange = GetAnalyticsSummaryPeriodRange();

                using (var context = new RequestsEntities())
                {
                    var completedStatusIds = context.Statuses
                        .Where(s => s.StatusName == "Выполнена" || s.StatusName == "Закрыта")
                        .Select(s => s.StatusID)
                        .ToList();
                    if (!completedStatusIds.Any())
                        completedStatusIds.Add(4);

                    var completedStatusId = context.Statuses
                        .Where(s => s.StatusName == "Выполнена")
                        .Select(s => (int?)s.StatusID)
                        .FirstOrDefault() ?? 4;

                    var query = context.Requests
                        .Include("Categories")
                        .AsQueryable()
                        .Where(r => r.IsDeleted == false);

                    var specialistQuery = query;
                    if (specialistPeriodRange.DateFrom.HasValue)
                        specialistQuery = specialistQuery.Where(r => r.CreatedDate >= specialistPeriodRange.DateFrom.Value);
                    if (specialistPeriodRange.DateTo.HasValue)
                    {
                        var specialistDateToExclusive = specialistPeriodRange.DateTo.Value.Date.AddDays(1);
                        specialistQuery = specialistQuery.Where(r => r.CreatedDate < specialistDateToExclusive);
                    }

                    var summaryQuery = query;
                    if (summaryPeriodRange.DateFrom.HasValue)
                        summaryQuery = summaryQuery.Where(r => r.CreatedDate >= summaryPeriodRange.DateFrom.Value);
                    if (summaryPeriodRange.DateTo.HasValue)
                    {
                        var summaryDateToExclusive = summaryPeriodRange.DateTo.Value.Date.AddDays(1);
                        summaryQuery = summaryQuery.Where(r => r.CreatedDate < summaryDateToExclusive);
                    }

                    var specialistPeriodRequests = specialistQuery.ToList();
                    var allRequests = summaryQuery.ToList();
                    var specialistRequests = _selectedAnalyticsSpecialistId.HasValue
                        ? specialistPeriodRequests.Where(r => r.AssignedTo == _selectedAnalyticsSpecialistId.Value).ToList()
                        : new List<ServiceDesk.Models.Requests>();

                    _currentStats = new RequestStats
                    {
                        Total = specialistRequests.Count,
                        Completed = specialistRequests.Count(r => completedStatusIds.Contains(r.StatusID)),
                        InProgress = specialistRequests.Count(r => r.StatusID == 2), // ID=2: "В работе"
                        Overdue = specialistRequests.Count(r =>
                        !completedStatusIds.Contains(r.StatusID) &&
                        r.UpdatedDate < DateTime.Now.AddDays(-7)),
                        LastUpdated = DateTime.Now
                    };

                    _currentStats.AvgCompletionHours = CalculateAverageCompletionHours(context, specialistRequests, completedStatusIds, completedStatusId);

                    UpdateAnaliticStatsUI();
                    var totalRequestsCount = allRequests.Count;

                    _categoryLoads = allRequests
                        .GroupBy(r => new { r.CategoryID, r.Categories.CategoryName })
                        .Select(g => new CategoryLoad
                        {
                            CategoryID = g.Key.CategoryID,
                            CategoryName = g.Key.CategoryName ?? "Без категории",
                            RequestCount = g.Count(),
                            CompletedCount = g.Count(r => completedStatusIds.Contains(r.StatusID)),
                            InProgressCount = g.Count(r => !completedStatusIds.Contains(r.StatusID)),
                        })
                        .Where(c => c.InProgressCount > 0)  
                        .OrderByDescending(c => c.InProgressCount)
                        .Take(3)
                        .ToList();

                    if (_categoryLoads.Count < 3)
                    {
                        var remainingCategories = allRequests
                            .GroupBy(r => new { r.CategoryID, r.Categories.CategoryName })
                            .Select(g => new CategoryLoad
                            {
                                CategoryID = g.Key.CategoryID,
                                CategoryName = g.Key.CategoryName ?? "Без категории",
                                RequestCount = g.Count(),
                                CompletedCount = g.Count(r => completedStatusIds.Contains(r.StatusID)),
                                InProgressCount = g.Count(r => !completedStatusIds.Contains(r.StatusID)),
                            })
                            .Where(c => !_categoryLoads.Any(x => x.CategoryID == c.CategoryID))
                            .OrderByDescending(c => c.RequestCount)
                            .Take(3 - _categoryLoads.Count)
                            .ToList();

                        _categoryLoads.AddRange(remainingCategories);
                    }

                    for (int i = 0; i < _categoryLoads.Count; i++)
                    {
                        _categoryLoads[i].Rank = $"{i + 1}";
                    }

                    var categoryDetails = allRequests
                        .GroupBy(r => new { r.CategoryID, r.Categories.CategoryName })
                        .Select(g => new
                        {
                            CategoryName = g.Key.CategoryName ?? "Без категории",
                            Total = g.Count(),
                            Completed = g.Count(r => completedStatusIds.Contains(r.StatusID)),
                            InProgress = g.Count(r => r.StatusID == 2),
                            Percent = g.Count(r => completedStatusIds.Contains(r.StatusID)) * 100.0 / g.Count()
                        })
                        .OrderByDescending(c => c.Total)
                        .ToList();

                    TopCategoriesList.ItemsSource = _categoryLoads;
                    CategoriesStatsGrid.ItemsSource = categoryDetails;
                    DrawPieChart(BuildCategoryGroupsForChart(allRequests));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки аналитики: " + ex.Message, "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateAnaliticStatsUI()
        {
            if (_currentStats == null) return;

            Dispatcher.Invoke(() =>
            {
                if (StatsTotalText != null) StatsTotalText.Text = _currentStats.Total.ToString();

                if (StatsCompletedText != null)
                {
                    StatsCompletedText.Text = _currentStats.Completed.ToString();
                    var percent = _currentStats.Total > 0
                        ? (double)_currentStats.Completed / _currentStats.Total * 100
                        : 0;
                    if (StatsCompletedPercentText != null)
                        StatsCompletedPercentText.Text = $"{percent:F1}%";
                }

                if (StatsInProgressText != null) StatsInProgressText.Text = _currentStats.InProgress.ToString();

                if (StatsAvgTimeText != null)
                    StatsAvgTimeText.Text = FormatHours(_currentStats.AvgCompletionHours);
            });
        }

        private (DateTime? DateFrom, DateTime? DateTo) GetAnalyticsPeriodRange()
        {
            if (IsAnalyticsCustomPeriodSelected())
                return (_analyticsDateFrom?.Date, _analyticsDateTo?.Date);

            return GetPeriodRange(AnalyticsPeriodCombo);
        }

        private (DateTime? DateFrom, DateTime? DateTo) GetAnalyticsSummaryPeriodRange()
        {
            if (IsAnalyticsSummaryCustomPeriodSelected())
                return (_analyticsSummaryDateFrom?.Date, _analyticsSummaryDateTo?.Date);

            return GetPeriodRange(AnalyticsSummaryPeriodCombo);
        }

        private (DateTime? DateFrom, DateTime? DateTo) GetPeriodRange(ComboBox comboBox)
        {
            if (comboBox?.SelectedItem is ComboBoxItem periodItem)
            {
                var content = periodItem.Content?.ToString();

                switch (content)
                {
                    case "Сегодня":
                        return (DateTime.Now.Date, null);

                    case "Эта неделя":
                        var daysToMonday = (int)DateTime.Now.DayOfWeek - (int)DayOfWeek.Monday;
                        if (daysToMonday < 0) daysToMonday += 7;
                        return (DateTime.Now.Date.AddDays(-daysToMonday), null);

                    case "Этот месяц":
                        return (new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1), null);

                    case "Этот год":
                        return (new DateTime(DateTime.Now.Year, 1, 1), null);
                }
            }

            return (null, null);
        }

        private void UpdateAnalyticsCustomPeriodVisibility()
        {
            if (AnalyticsCustomPeriodPanel != null)
                AnalyticsCustomPeriodPanel.Visibility = IsAnalyticsCustomPeriodSelected() ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateAnalyticsSummaryCustomPeriodVisibility()
        {
            if (AnalyticsSummaryCustomPeriodPanel != null)
                AnalyticsSummaryCustomPeriodPanel.Visibility = IsAnalyticsSummaryCustomPeriodSelected() ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool IsAnalyticsCustomPeriodSelected()
        {
            return (AnalyticsPeriodCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Свой период";
        }

        private bool IsAnalyticsSummaryCustomPeriodSelected()
        {
            return (AnalyticsSummaryPeriodCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() == "Свой период";
        }

        private double CalculateAverageCompletionHours(RequestsEntities context, List<ServiceDesk.Models.Requests> requests, List<int> completedStatusIds, int completedStatusId)
        {
            var completedRequests = requests
                .Where(r => completedStatusIds.Contains(r.StatusID))
                .ToList();
            var requestIds = completedRequests.Select(r => r.RequestID).ToList();
            if (!requestIds.Any())
                return 0;

            var completionHistory = context.StatusHistory
                .Where(h => requestIds.Contains(h.RequestID) && completedStatusIds.Contains(h.StatusID))
                .Select(h => new
                {
                    h.RequestID,
                    h.StatusID,
                    h.ChangedAt,
                    h.Comment
                })
                .ToList();

            var completedAtByRequest = new Dictionary<int, DateTime>();
            foreach (var requestId in requestIds)
            {
                var requestHistory = completionHistory
                    .Where(h => h.RequestID == requestId)
                    .OrderBy(h => h.ChangedAt)
                    .ToList();

                var completedHistory = requestHistory.FirstOrDefault(h => h.StatusID == completedStatusId);
                if (completedHistory != null)
                {
                    completedAtByRequest[requestId] = completedHistory.ChangedAt;
                    continue;
                }

                var autoCloseHistory = requestHistory.FirstOrDefault(h =>
                    !string.IsNullOrWhiteSpace(h.Comment) &&
                    h.Comment.Contains("Автоматическое закрытие"));
                if (autoCloseHistory != null)
                {
                    completedAtByRequest[requestId] = autoCloseHistory.ChangedAt.AddDays(-3);
                    continue;
                }

                var anyCompletionHistory = requestHistory.FirstOrDefault();
                if (anyCompletionHistory != null)
                    completedAtByRequest[requestId] = anyCompletionHistory.ChangedAt;
            }

            var completedWithTime = completedRequests
                .Select(r =>
                {
                    DateTime completedAt;
                    if (!completedAtByRequest.TryGetValue(r.RequestID, out completedAt))
                        completedAt = r.UpdatedDate;

                    return (completedAt - r.CreatedDate).TotalHours;
                })
                .Where(h => h >= 0)
                .ToList();

            return completedWithTime.Any() ? completedWithTime.Average() : 0;
        }

        private static string FormatHours(double hours)
        {
            return hours >= 24
                ? $"{hours / 24:F1} дн"
                : $"{hours:F1} ч";
        }

        private class CategoryChartItem
        {
            public string CategoryName { get; set; }
            public int Count { get; set; }
        }

        private class PieLegendItem
        {
            public Brush Brush { get; set; }
            public string Text { get; set; }
        }

        private List<CategoryChartItem> BuildCategoryGroupsForChart(List<ServiceDesk.Models.Requests> requests)
        {
            return requests
                .GroupBy(r => GetCategoryGroupName(r.Categories?.CategoryName))
                .Select(g => new CategoryChartItem
                {
                    CategoryName = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(c => c.Count)
                .ThenBy(c => c.CategoryName)
                .ToList();
        }

        private static string GetCategoryGroupName(string categoryName)
        {
            switch (categoryName)
            {
                case "Оборудование":
                case "Офисная техника":
                case "Периферийные устройства":
                case "Мобильные устройства":
                case "Сканирование":
                    return "Оборудование и периферия";

                case "Программное обеспечение":
                case "Операционная система":
                    return "ПО и операционные системы";

                case "Сеть":
                case "Интернет":
                case "Телефония":
                    return "Сеть и связь";

                case "Учетные записи":
                case "Доступ к системам":
                    return "Учетные записи и доступ";

                case "Безопасность":
                case "Шифрование":
                    return "Безопасность";

                case "Документооборот":
                case "Архивация документов":
                case "Корпоративный портал":
                case "Электронная почта":
                    return "Документы и коммуникации";

                case "Базы данных":
                case "Резервное копирование":
                    return "Данные и резервирование";

                default:
                    return "Прочее";
            }
        }

        private void DrawPieChart(List<CategoryChartItem> categories)
        {
            if (PieChartCanvas == null || PieLegendList == null)
                return;

            PieChartCanvas.Children.Clear();
            var total = categories.Sum(c => c.Count);
            if (total <= 0)
            {
                PieLegendList.ItemsSource = new List<PieLegendItem>();
                return;
            }

            var brushes = new[]
            {
                new SolidColorBrush(Color.FromRgb(0, 114, 178)),
                new SolidColorBrush(Color.FromRgb(213, 94, 0)),
                new SolidColorBrush(Color.FromRgb(0, 158, 115)),
                new SolidColorBrush(Color.FromRgb(204, 121, 167)),
                new SolidColorBrush(Color.FromRgb(230, 159, 0)),
                new SolidColorBrush(Color.FromRgb(86, 180, 233)),
                new SolidColorBrush(Color.FromRgb(240, 228, 66)),
                new SolidColorBrush(Color.FromRgb(120, 74, 190))
            };

            const double size = 360;
            const double radius = size / 2;
            var center = new Point(180, 180);
            double startAngle = -90;
            var legend = new List<PieLegendItem>();

            for (int i = 0; i < categories.Count; i++)
            {
                var category = categories[i];
                double sweepAngle = 360.0 * category.Count / total;
                var brush = brushes[i % brushes.Length];

                var path = CreatePieSlice(center, radius, startAngle, sweepAngle, brush);
                PieChartCanvas.Children.Add(path);

                double percent = category.Count * 100.0 / total;
                legend.Add(new PieLegendItem
                {
                    Brush = brush,
                    Text = $"{category.CategoryName}: {category.Count} ({percent:F1}%)"
                });

                startAngle += sweepAngle;
            }

            PieLegendList.ItemsSource = legend;
        }

        private static System.Windows.Shapes.Path CreatePieSlice(Point center, double radius, double startAngle, double sweepAngle, Brush brush)
        {
            if (sweepAngle >= 359.9)
            {
                return new System.Windows.Shapes.Path
                {
                    Fill = brush,
                    Data = new EllipseGeometry(center, radius, radius)
                };
            }

            double startRadians = startAngle * Math.PI / 180;
            double endRadians = (startAngle + sweepAngle) * Math.PI / 180;
            var startPoint = new Point(center.X + radius * Math.Cos(startRadians), center.Y + radius * Math.Sin(startRadians));
            var endPoint = new Point(center.X + radius * Math.Cos(endRadians), center.Y + radius * Math.Sin(endRadians));

            var figure = new PathFigure { StartPoint = center };
            figure.Segments.Add(new LineSegment(startPoint, true));
            figure.Segments.Add(new ArcSegment(endPoint, new Size(radius, radius), 0, sweepAngle > 180, SweepDirection.Clockwise, true));
            figure.Segments.Add(new LineSegment(center, true));

            return new System.Windows.Shapes.Path
            {
                Fill = brush,
                Stroke = Brushes.White,
                StrokeThickness = 1,
                Data = new PathGeometry(new[] { figure })
            };
        }

        private void LoadAllRequests()
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    _allRequests = context.Requests
                        .Include("Categories")
                        .Include("Statuses")
                        .Include("Users")
                        .Include("Users1")
                        .ToList();
                }

                var baseRequests = _allRequests.AsEnumerable();
                if (Navigation.IsUser)
                {
                    baseRequests = baseRequests.Where(r => r.CreatedBy == Navigation.UserID);
                }
                _itemcount = baseRequests.Count();


                ApplyFilter();
                TBoxSearch.Text = "🔍 Поиск...";
            }
            catch (Exception ex)
            {
                StatusMessage.Text = $"Ошибка: {ex.Message}";
                MessageBox.Show("Не удалось загрузить заявки: " + ex.Message, "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ApplyFilter()
        {
            if (_allRequests == null)
                return;

            var filtered = _allRequests.AsEnumerable();
            filtered = _selectedStatusId == 5
                ? filtered.Where(r => r.IsDeleted == true || r.StatusID == 5)
                : filtered.Where(r => r.IsDeleted == false);

            if (Navigation.IsUser)
            {
                filtered = filtered.Where(r => r.CreatedBy == Navigation.UserID);
            }

            if (Navigation.IsIT && AssignedToMeCheckBox?.IsChecked == true)
            {
                filtered = filtered.Where(r => r.AssignedTo == Navigation.UserID);
            }

            if (_selectedStatusId.HasValue)
            {
                filtered = filtered.Where(r => r.StatusID == _selectedStatusId.Value);
            }

            if (_dateFrom.HasValue || _dateTo.HasValue)
            {
                DateTime? startDate = _dateFrom?.Date;
                DateTime? endDate = (_dateTo?.Date).HasValue ? _dateTo.Value.Date.AddDays(1) : (DateTime?)null;

                filtered = filtered.Where(r =>
                {
                    var created = r.CreatedDate.Date;
                    var updated = r.UpdatedDate.Date;
                    bool inRange = true;

                    if (startDate.HasValue)
                        inRange &= (created >= startDate || updated >= startDate);
                    if (endDate.HasValue)
                        inRange &= (created < endDate || updated < endDate);

                    return inRange;
                });
            }

            if (TBoxSearch != null && !string.IsNullOrWhiteSpace(TBoxSearch.Text) && _searchFieldIndex >= 0)
            {
                var searchText = TBoxSearch.Text.ToLower();
                switch (_searchFieldIndex)
                {
                    case 0:
                        if (int.TryParse(searchText, out int id))
                            filtered = filtered.Where(p => p.RequestID == id);
                        break;
                    case 1:
                        filtered = filtered.Where(p => p.Users1?.FullName?.ToLower().Contains(searchText) == true);
                        break;
                    case 2:
                        filtered = filtered.Where(p => p.Users?.FullName?.ToLower().Contains(searchText) == true);
                        break;
                    case 3:
                        filtered = filtered.Where(p => p.Categories?.CategoryName?.ToLower().Contains(searchText) == true);
                        break;
                    case 4:
                        filtered = filtered.Where(p => p.Description?.ToLower().Contains(searchText) == true);
                        break;
                }
            }

            var result = _sortRequestsByNewest
                ? filtered.OrderByDescending(p => p.CreatedDate).ToList()
                : filtered.OrderBy(p => p.RequestID).ToList();
            DBGridRequests.ItemsSource = result;
            TextBlockInfo.Text = $"Найдено: {result.Count} из {_itemcount} записей";
        }

        private void ComboSearch_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _searchFieldIndex = ComboSearch.SelectedIndex;
            ApplyFilter();
        }

        private void TBoxSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = TBoxSearch.Text.ToLower().Trim();
            _searchTimer?.Stop();
            _searchTimer?.Start();
        }

        private void TBoxSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            if (TBoxSearch.Text == "🔍 Поиск...") TBoxSearch.Text = "";
        }

        private void TBoxSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TBoxSearch.Text)) TBoxSearch.Text = "🔍 Поиск...";
        }

        private void DatePickerFrom_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _dateFrom = DatePickerFrom.SelectedDate;
            ApplyFilter();
        }

        private void DatePickerTo_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            _dateTo = DatePickerTo.SelectedDate;
            ApplyFilter();
        }

        private void StatusFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusFilter.SelectedItem is ComboBoxItem item)
            {
                if (item.Tag is string tag && int.TryParse(tag, out int statusId))
                {
                    _selectedStatusId = statusId;
                }
                else
                {
                    _selectedStatusId = null;
                }
                ApplyFilter();
            }
        }

        private void DBGridRequests_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedRequest = DBGridRequests.SelectedItem as ServiceDesk.Models.Requests;
        }

        private void BtnClearFilter_Click(object sender, RoutedEventArgs e)
        {
            DatePickerFrom.SelectedDate = null;
            DatePickerTo.SelectedDate = null;
            _dateFrom = null;
            _dateTo = null;
            TBoxSearch.Text = "🔍 Поиск...";
            ComboSearch.SelectedIndex = 0;
            _searchFieldIndex = 0;
            StatusFilter.SelectedIndex = 0;
            _selectedStatusId = null;
            _sortRequestsByNewest = false;
            if (AssignedToMeCheckBox != null)
                AssignedToMeCheckBox.IsChecked = false;

            ApplyFilter();
            StatusMessage.Text = "Фильтры сброшены";
        }

        private void AssignedToMeCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void BtnAdd_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.Navigate(new AddEditPages.AddEditRequests());
        }

        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRequest == null)
            {
                MessageBox.Show("Выберите заявку для редактирования.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (Navigation.IsIT && _selectedRequest.AssignedTo != Navigation.UserID && _selectedRequest.CreatedBy != Navigation.UserID)
            {
                MessageBox.Show("Вы можете изменить только свои собственные заявки.", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Navigation.MainFrame?.Navigate(new AddEditPages.AddEditRequests(_selectedRequest));
        }

        private void ReturnToWork_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedRequest == null)
            {
                MessageBox.Show("Выберите заявку.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (!Navigation.IsUser || _selectedRequest.CreatedBy != Navigation.UserID)
            {
                MessageBox.Show("Вернуть в работу можно только свою заявку.", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var context = new RequestsEntities())
                {
                    var request = context.Requests.FirstOrDefault(r => r.RequestID == _selectedRequest.RequestID);
                    if (request == null)
                        return;

                    int closedStatusId = GetOrCreateClosedStatusId(context);
                    if (request.StatusID != 4 && request.StatusID != closedStatusId)
                    {
                        MessageBox.Show("Вернуть в работу можно только выполненную или закрытую заявку.", "Внимание",
                            MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    int oldStatusId = request.StatusID;
                    request.StatusID = 2;
                    request.UpdatedDate = DateTime.Now;

                    context.StatusHistory.Add(new StatusHistory
                    {
                        RequestID = request.RequestID,
                        OldStatusID = oldStatusId,
                        StatusID = 2,
                        ChangedBy = Navigation.UserID,
                        ChangedAt = DateTime.Now,
                        Comment = "Пользователь вернул заявку в работу"
                    });

                    if (request.AssignedTo.HasValue)
                    {
                        CreateNotification(context,
                            "Заявка возвращена в работу",
                            $"Заявка #{request.RequestID} возвращена пользователем в работу.",
                            request.AssignedTo.Value);
                    }

                    context.SaveChanges();
                }

                LoadAllRequests();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка возврата заявки в работу: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = DBGridRequests.SelectedItems.Cast<ServiceDesk.Models.Requests>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Выберите заявки для удаления.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show($"Удалить {selectedItems.Count} заявок?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var context = new RequestsEntities())
                {
                    var ids = selectedItems.Select(r => r.RequestID).ToList();
                    var toUpdate = context.Requests.Where(r => ids.Contains(r.RequestID)).ToList();

                    foreach (var request in toUpdate)
                    {
                        var oldStatusId = request.StatusID;
                        request.IsDeleted = true;
                        request.StatusID = 5;
                        request.UpdatedDate = DateTime.Now;
                        request.DeletedAt = DateTime.Now;
                        request.DeletedBy = Navigation.UserID;

                        LogStatusChange(context, request.RequestID, oldStatusId, 5, Navigation.UserID, "Заявка удалена");
                        DeleteRequestNotifications(context, request.RequestID);
                    }
                    context.SaveChanges();
                }

                MessageBox.Show("Заявки удалены!", "Успех",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAllRequests();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка удаления: " + ex.Message, "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void LogStatusChange(RequestsEntities context, int requestId, int? oldStatusId, int newStatusId, int changedBy, string comment = null)
        {
            try
            {
                // Не логируем, если статус не изменился
                if (oldStatusId == newStatusId)
                    return;

                context.StatusHistory.Add(new StatusHistory
                {
                    RequestID = requestId,
                    OldStatusID = oldStatusId,
                    StatusID = newStatusId,
                    ChangedBy = changedBy,
                    ChangedAt = DateTime.Now,
                    Comment = comment ?? $"Статус изменён с {oldStatusId} на {newStatusId}"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка логирования статуса: {ex.Message}");
            }
        }

        private void AutoCloseCompletedRequests()
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    int closedStatusId = GetOrCreateClosedStatusId(context);
                    DateTime closeBorder = DateTime.Now.AddDays(-3);
                    var requestsToClose = context.Requests
                        .Where(r => r.IsDeleted == false && r.StatusID == 4 && r.UpdatedDate <= closeBorder)
                        .ToList();

                    foreach (var request in requestsToClose)
                    {
                        request.StatusID = closedStatusId;
                        request.UpdatedDate = DateTime.Now;

                        context.StatusHistory.Add(new StatusHistory
                        {
                            RequestID = request.RequestID,
                            OldStatusID = 4,
                            StatusID = closedStatusId,
                            ChangedBy = Navigation.UserID,
                            ChangedAt = DateTime.Now,
                            Comment = "Автоматическое закрытие выполненной заявки спустя 3 дня"
                        });

                        CreateNotification(context,
                            "Заявка автоматически закрыта",
                            $"Заявка #{request.RequestID} автоматически закрыта, так как была выполнена более 3 дней назад.",
                            request.CreatedBy);
                    }

                    if (requestsToClose.Any())
                        context.SaveChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ошибка автозакрытия заявок: {ex.Message}");
            }
        }

        private int GetOrCreateClosedStatusId(RequestsEntities context)
        {
            var status = context.Statuses.FirstOrDefault(s => s.StatusName == "Закрыта");
            if (status != null)
                return status.StatusID;

            status = new Statuses { StatusName = "Закрыта" };
            context.Statuses.Add(status);
            context.SaveChanges();
            return status.StatusID;
        }

        private void CreateNotification(RequestsEntities context, string title, string message, int targetUserId)
        {
            context.Database.ExecuteSqlCommand(
                @"INSERT INTO dbo.Notifications (Title, Message, CreatedBy, TargetUserID)
                  VALUES (@Title, @Message, @CreatedBy, @TargetUserID)",
                new SqlParameter("@Title", title),
                new SqlParameter("@Message", message),
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

        public void BtnNotifications_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.Navigate(new NotificationsPage());
        }
        public void ShowAllActivities_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRequestsTabInitialized)
            {
                InitRequestsTab();
                _isRequestsTabInitialized = true;
            }

            _selectedStatusId = null;
            _dateFrom = null;
            _dateTo = null;
            _sortRequestsByNewest = true;

            if (DatePickerFrom != null) DatePickerFrom.SelectedDate = null;
            if (DatePickerTo != null) DatePickerTo.SelectedDate = null;
            if (StatusFilter != null) StatusFilter.SelectedIndex = 0;
            if (ComboSearch != null) ComboSearch.SelectedIndex = 0;
            if (TBoxSearch != null) TBoxSearch.Text = "🔍 Поиск...";

            ApplyFilter();
            if (DashboardTabs != null)
                DashboardTabs.SelectedIndex = 1;
        }
        public void AddEvent_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.Navigate(new AddEditPages.AddEditTask());
        }
        public void MyTasks_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.Navigate(new TasksPage());
        }

        private void CalendarTab_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_isCalendarTabInitialized)
            {
                LoadUpcomingTasks();
                LoadDashboardNotifications();
                _isCalendarTabInitialized = true;
            }
            else
            {
                LoadUpcomingTasks();
                LoadDashboardNotifications();
            }
        }

        private void LoadUpcomingTasks()
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    var tasks = context.Database.SqlQuery<PlannedTaskItem>(
                        $@"SELECT TOP ({_tasksDisplayCount}) t.TaskID, t.Title, t.Description, t.StartAt, t.EndAt, t.CreatedBy, t.AssignedTo,
                                 t.IsCompleted, t.CreatedAt,
                                 LTRIM(RTRIM(CONCAT(c.LastName, N' ', c.FirstName, N' ', ISNULL(c.Patronymic, N'')))) AS CreatedByName,
                                 LTRIM(RTRIM(CONCAT(a.LastName, N' ', a.FirstName, N' ', ISNULL(a.Patronymic, N'')))) AS AssignedToName
                          FROM dbo.PlannedTasks t
                          INNER JOIN dbo.Users c ON c.UserID = t.CreatedBy
                          LEFT JOIN dbo.Users a ON a.UserID = t.AssignedTo
                          WHERE t.IsCompleted = 0
                            AND (t.CreatedBy = @UserID OR t.AssignedTo = @UserID OR @IsAdmin = 1)
                          ORDER BY t.StartAt",
                        new SqlParameter("@UserID", Navigation.UserID),
                        new SqlParameter("@IsAdmin", Navigation.IsAdmin ? 1 : 0)).ToList();

                    EventsListBox.ItemsSource = tasks;
                    EventsStatusText.Text = tasks.Count == 0 ? "Нет ближайших задач" : $"Показано задач: {tasks.Count}";
                    TasksCountText.Text = _tasksDisplayCount.ToString();
                }
            }
            catch (Exception ex)
            {
                EventsStatusText.Text = "Ошибка загрузки задач";
                MessageBox.Show("Ошибка загрузки планирования: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadDashboardNotifications()
        {
            if (DashboardNotificationsListBox == null ||
                DashboardNotificationsStatusText == null ||
                NotificationsCountText == null)
            {
                return;
            }

            try
            {
                using (var context = new RequestsEntities())
                {
                    bool unreadOnly = UnreadNotificationsCheckBox?.IsChecked == true;
                    string topClause = _notificationsDisplayCount > 0
                        ? $"TOP ({_notificationsDisplayCount})"
                        : "";
                    string readCondition = unreadOnly ? "AND nr.NotificationID IS NULL" : "";

                    var notifications = context.Database.SqlQuery<NotificationItem>(
                        $@"SELECT {topClause} n.NotificationID, n.TargetUserID, n.CreatedBy, n.Title, n.Message, n.CreatedAt,
                                  CAST(CASE WHEN nr.NotificationID IS NULL THEN 0 ELSE 1 END AS bit) AS IsRead,
                                  LTRIM(RTRIM(CONCAT(c.LastName, N' ', c.FirstName, N' ', ISNULL(c.Patronymic, N'')))) AS CreatedByName,
                                  LTRIM(RTRIM(CONCAT(t.LastName, N' ', t.FirstName, N' ', ISNULL(t.Patronymic, N'')))) AS TargetUserName
                           FROM dbo.Notifications n
                           LEFT JOIN dbo.NotificationReads nr
                             ON nr.NotificationID = n.NotificationID AND nr.UserID = @UserID
                           LEFT JOIN dbo.Users c ON c.UserID = n.CreatedBy
                           LEFT JOIN dbo.Users t ON t.UserID = n.TargetUserID
                           WHERE (n.TargetUserID IS NULL OR n.TargetUserID = @UserID)
                             {readCondition}
                           ORDER BY n.CreatedAt DESC",
                        new SqlParameter("@UserID", Navigation.UserID)).ToList();

                    notifications = notifications ?? new List<NotificationItem>();

                    DashboardNotificationsListBox.ItemsSource = notifications;
                    DashboardNotificationsStatusText.Text = notifications.Count == 0
                        ? "Нет непрочитанных уведомлений"
                        : $"Показано уведомлений: {notifications.Count}";
                    NotificationsCountText.Text = _notificationsDisplayCount.ToString();
                }
            }
            catch
            {
                if (DashboardNotificationsStatusText != null)
                    DashboardNotificationsStatusText.Text = "Таблица уведомлений недоступна";
            }
        }

        private void UpdateUnreadNotificationsCount()
        {
            if (Navigation.CurrentUser == null || NotificationsButton == null)
                return;

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

                    var latest = context.Database.SqlQuery<NotificationItem>(
                        @"SELECT TOP 1 n.NotificationID, n.TargetUserID, n.CreatedBy, n.Title, n.Message, n.CreatedAt,
                                 CAST(0 AS bit) AS IsRead,
                                 LTRIM(RTRIM(CONCAT(c.LastName, N' ', c.FirstName, N' ', ISNULL(c.Patronymic, N'')))) AS CreatedByName,
                                 LTRIM(RTRIM(CONCAT(t.LastName, N' ', t.FirstName, N' ', ISNULL(t.Patronymic, N'')))) AS TargetUserName
                          FROM dbo.Notifications n
                          LEFT JOIN dbo.NotificationReads nr
                            ON nr.NotificationID = n.NotificationID AND nr.UserID = @UserID
                          LEFT JOIN dbo.Users c ON c.UserID = n.CreatedBy
                          LEFT JOIN dbo.Users t ON t.UserID = n.TargetUserID
                          WHERE nr.NotificationID IS NULL
                            AND (n.TargetUserID IS NULL OR n.TargetUserID = @UserID)
                          ORDER BY n.NotificationID DESC",
                        new SqlParameter("@UserID", Navigation.UserID)).FirstOrDefault();

                    NotificationsButton.ToolTip = unreadCount > 0
                        ? $"Непрочитанных уведомлений: {unreadCount}"
                        : "Нет новых уведомлений";

                    SetWindowsTrayUnreadState(unreadCount > 0);

                    if (latest != null && latest.NotificationID > _lastShownNotificationId)
                    {
                        _lastShownNotificationId = latest.NotificationID;
                        ShowWindowsNotification(Navigation.UserID, latest.NotificationID, latest.Title, latest.Message);
                    }

                    if (_isCalendarTabInitialized)
                        LoadDashboardNotifications();
                }
            }
            catch
            {
                NotificationsButton.ToolTip = "Таблица уведомлений недоступна";
            }
        }

        private void InitializeNotificationBaseline()
        {
            _lastShownNotificationId = 0;
        }

        private void ShowWindowsNotification(int userId, int notificationId, string title, string message)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ShowWindowsNotification(
                    userId,
                    notificationId,
                    string.IsNullOrWhiteSpace(title) ? "ServiceDesk" : title,
                    "У вас новое уведомление");
            }
        }

        private void SetWindowsTrayUnreadState(bool hasUnread)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
                mainWindow.SetTrayUnreadState(hasUnread);
        }

        private void TasksCountDown_Click(object sender, RoutedEventArgs e)
        {
            _tasksDisplayCount = Math.Max(1, _tasksDisplayCount - 1);
            LoadUpcomingTasks();
        }

        private void TasksCountUp_Click(object sender, RoutedEventArgs e)
        {
            _tasksDisplayCount = Math.Min(20, _tasksDisplayCount + 1);
            LoadUpcomingTasks();
        }

        private void NotificationsCountDown_Click(object sender, RoutedEventArgs e)
        {
            _notificationsDisplayCount = Math.Max(1, _notificationsDisplayCount - 1);
            if (_isCalendarTabInitialized)
                LoadDashboardNotifications();
        }

        private void NotificationsCountUp_Click(object sender, RoutedEventArgs e)
        {
            _notificationsDisplayCount = Math.Min(20, _notificationsDisplayCount + 1);
            if (_isCalendarTabInitialized)
                LoadDashboardNotifications();
        }

        private void NotificationLimit_Changed(object sender, RoutedEventArgs e)
        {
            if (_isCalendarTabInitialized)
                LoadDashboardNotifications();
        }

        private void DashboardTaskCompleted_Checked(object sender, RoutedEventArgs e)
        {
            if (!((sender as CheckBox)?.Tag is PlannedTaskItem task))
                return;

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

                LoadUpcomingTasks();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления задачи: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DashboardNotificationRead_Checked(object sender, RoutedEventArgs e)
        {
            if (!((sender as CheckBox)?.Tag is NotificationItem notification))
                return;

            MarkNotificationRead(notification.NotificationID);
            LoadDashboardNotifications();
            UpdateUnreadNotificationsCount();
        }

        private void MarkNotificationRead(int notificationId)
        {
            try
            {
                using (var context = new RequestsEntities())
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
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка обновления уведомления: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateUnreadNotificationsCount();
        }

        private void ExportRequests_Click(object sender, RoutedEventArgs e)
        {
            var requests = DBGridRequests.ItemsSource as IEnumerable<ServiceDesk.Models.Requests>;
            if (requests == null || !requests.Any())
            {
                MessageBox.Show("Нет заявок для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var rows = requests.Select(r => new Dictionary<string, object>
            {
                ["ID"] = r.RequestID,
                ["Создано"] = r.Users1?.FullName,
                ["Назначено"] = r.Users?.FullName,
                ["Категория"] = r.Categories?.CategoryName,
                ["Описание"] = r.Description,
                ["Статус"] = r.Statuses?.StatusName,
                ["Дата создания"] = r.CreatedDate,
                ["Дата обновления"] = r.UpdatedDate
            }).ToList();

            ExportRowsToExcel("Заявки", rows);
        }

        private void ExportUsers_Click(object sender, RoutedEventArgs e)
        {
            var users = UsersDataGrid.ItemsSource as IEnumerable<ServiceDesk.Models.Users>;
            if (users == null || !users.Any())
            {
                MessageBox.Show("Нет пользователей для экспорта.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var rows = users.Select(u => new Dictionary<string, object>
            {
                ["ID"] = u.UserID,
                ["Фамилия"] = u.LastName,
                ["Имя"] = u.FirstName,
                ["Отчество"] = u.Patronymic,
                ["Отдел"] = u.Department,
                ["Роль"] = u.Role,
                ["Email"] = u.Email,
                ["Телефон"] = u.TelephoneNum
            }).ToList();

            ExportRowsToExcel("Пользователи", rows);
        }

        private void ExportAnalytics_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStats == null)
                LoadAnalyticsData();

            var rows = new List<Dictionary<string, object>>();
            rows.Add(new Dictionary<string, object>
            {
                ["Раздел"] = "Параметры отчета",
                ["Показатель"] = "Специалист",
                ["Значение"] = GetSelectedAnalyticsSpecialistName()
            });
            rows.Add(new Dictionary<string, object>
            {
                ["Раздел"] = "Параметры отчета",
                ["Показатель"] = "Период карточек специалиста",
                ["Значение"] = GetAnalyticsPeriodDisplayText()
            });
            rows.Add(new Dictionary<string, object>
            {
                ["Раздел"] = "Параметры отчета",
                ["Показатель"] = "Период общей статистики",
                ["Значение"] = GetAnalyticsSummaryPeriodDisplayText()
            });
            rows.Add(new Dictionary<string, object>
            {
                ["Раздел"] = "Итоговая статистика",
                ["Показатель"] = "Всего заявок",
                ["Значение"] = _currentStats?.Total ?? 0
            });
            rows.Add(new Dictionary<string, object>
            {
                ["Раздел"] = "Итоговая статистика",
                ["Показатель"] = "Выполнено",
                ["Значение"] = _currentStats?.Completed ?? 0
            });
            rows.Add(new Dictionary<string, object>
            {
                ["Раздел"] = "Итоговая статистика",
                ["Показатель"] = "В работе",
                ["Значение"] = _currentStats?.InProgress ?? 0
            });
            rows.Add(new Dictionary<string, object>
            {
                ["Раздел"] = "Итоговая статистика",
                ["Показатель"] = "Среднее время выполнения, часов",
                ["Значение"] = _currentStats?.AvgCompletionHours.ToString("F1") ?? "0"
            });

            foreach (var category in _categoryLoads ?? new List<CategoryLoad>())
            {
                rows.Add(new Dictionary<string, object>
                {
                    ["Раздел"] = "Загруженные категории",
                    ["Показатель"] = category.CategoryName,
                    ["Значение"] = $"Всего: {category.RequestCount}; выполнено: {category.CompletedCount}; в работе: {category.InProgressCount}"
                });
            }

            foreach (var category in CategoriesStatsGrid?.ItemsSource as System.Collections.IEnumerable ?? new object[0])
            {
                var type = category.GetType();
                var categoryName = type.GetProperty("CategoryName")?.GetValue(category)?.ToString() ?? "";
                var total = type.GetProperty("Total")?.GetValue(category);
                var completed = type.GetProperty("Completed")?.GetValue(category);
                var inProgress = type.GetProperty("InProgress")?.GetValue(category);
                var percent = type.GetProperty("Percent")?.GetValue(category);
                var percentText = percent is double percentValue ? $"{percentValue:F1}%" : percent?.ToString();

                rows.Add(new Dictionary<string, object>
                {
                    ["Раздел"] = "Распределение по категориям",
                    ["Показатель"] = categoryName,
                    ["Значение"] = $"Всего: {total}; выполнено: {completed}; в работе: {inProgress}; процент выполнения: {percentText}"
                });
            }

            ExportRowsToExcel("Аналитика", rows);
        }

        private string GetSelectedAnalyticsSpecialistName()
        {
            if (AnalyticsSpecialistCombo?.SelectedItem is ServiceDesk.Models.Users specialist)
                return specialist.FullName;

            return "Не выбран";
        }

        private string GetAnalyticsPeriodDisplayText()
        {
            return GetPeriodDisplayText(AnalyticsPeriodCombo, AnalyticsDatePickerFrom, AnalyticsDatePickerTo);
        }

        private string GetAnalyticsSummaryPeriodDisplayText()
        {
            return GetPeriodDisplayText(AnalyticsSummaryPeriodCombo, AnalyticsSummaryDatePickerFrom, AnalyticsSummaryDatePickerTo);
        }

        private string GetPeriodDisplayText(ComboBox comboBox, DatePicker datePickerFrom, DatePicker datePickerTo)
        {
            var selectedPeriod = (comboBox?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Все время";
            if (selectedPeriod != "Свой период")
                return selectedPeriod;

            var from = datePickerFrom?.SelectedDate?.ToString("dd.MM.yyyy") ?? "не указано";
            var to = datePickerTo?.SelectedDate?.ToString("dd.MM.yyyy") ?? "не указано";
            return $"Свой период: с {from} по {to}";
        }

        private void StatusHistory_Click(object sender, RoutedEventArgs e)
        {
            if (Navigation.IsUser)
            {
                MessageBox.Show("История статусов доступна администратору и IT-специалисту.", "Доступ запрещен",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Navigation.MainFrame?.Navigate(new StatusHistoryPage());
        }

        private void ExportRowsToExcel(string sheetName, List<Dictionary<string, object>> rows)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Excel 2003 XML (*.xls)|*.xls",
                FileName = $"{sheetName}_{DateTime.Now:yyyyMMdd_HHmm}.xls"
            };

            if (dialog.ShowDialog() != true)
                return;

            try
            {
                var headers = rows.First().Keys.ToList();
                var builder = new StringBuilder();
                builder.AppendLine("<?xml version=\"1.0\"?>");
                builder.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
                builder.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
                builder.AppendLine($"<Worksheet ss:Name=\"{EscapeXml(sheetName)}\"><Table>");
                builder.AppendLine("<Row>");
                foreach (var header in headers)
                    builder.AppendLine($"<Cell><Data ss:Type=\"String\">{EscapeXml(header)}</Data></Cell>");
                builder.AppendLine("</Row>");

                foreach (var row in rows)
                {
                    builder.AppendLine("<Row>");
                    foreach (var header in headers)
                    {
                        object value = row[header];
                        string text = value is DateTime date ? date.ToString("dd.MM.yyyy HH:mm") : value?.ToString() ?? "";
                        builder.AppendLine($"<Cell><Data ss:Type=\"String\">{EscapeXml(text)}</Data></Cell>");
                    }
                    builder.AppendLine("</Row>");
                }

                builder.AppendLine("</Table></Worksheet></Workbook>");
                File.WriteAllText(dialog.FileName, builder.ToString(), Encoding.UTF8);
                MessageBox.Show("Экспорт выполнен.", "Экспорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка экспорта: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string EscapeXml(string value)
        {
            return SecurityElement.Escape(value) ?? "";
        }

        /// /////////////////////////////////Обработчики Пользователей

        private void UserSearchCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _userSearchFieldIndex = UserSearchCombo.SelectedIndex;
            ApplyUserFilter();
        }

        private void UserSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _userSearchTimer?.Stop();
            _userSearchTimer?.Start();
        }

        private void UserSearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UserSearchBox.Text == "🔍 Поиск...") UserSearchBox.Text = "";
        }

        private void UserSearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(UserSearchBox.Text)) UserSearchBox.Text = "🔍 Поиск...";
        }

        private void UsersDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedUser = UsersDataGrid.SelectedItem as ServiceDesk.Models.Users;
        }

        private void UserClearFilter_Click(object sender, RoutedEventArgs e)
        {
            UserSearchBox.Text = "🔍 Поиск...";
            if (UserSearchCombo?.Items.Count > 0) UserSearchCombo.SelectedIndex = 0;
            _userSearchFieldIndex = 0;

            ApplyUserFilter();
            UpdateUserStatus("Фильтры сброшены");
        }

        private void UserBtnAdd_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame?.Navigate(new AddEditPages.AddEditUser());
        }

        private void UserBtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedUser == null)
            {
                MessageBox.Show("Выберите пользователя для редактирования.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Navigation.MainFrame?.Navigate(new AddEditPages.AddEditUser(_selectedUser));
        }

        private void UserBtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = UsersDataGrid.SelectedItems.Cast<ServiceDesk.Models.Users>().ToList();
            if (!selectedItems.Any())
            {
                MessageBox.Show("Выберите пользователей для удаления.", "Внимание",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // нельзя удалить самого себя)
            if (selectedItems.Any(u => u.UserID == Navigation.UserID))
            {
                MessageBox.Show("Нельзя удалить свою учётную запись.", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Удалить {selectedItems.Count} пользователей?", "Подтверждение",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var context = new RequestsEntities())
                {
                    var ids = selectedItems.Select(u => u.UserID).ToList();
                    var activeRequests = context.Requests
                        .Where(r =>
                            ids.Contains(r.CreatedBy) ||
                            (r.AssignedTo.HasValue && ids.Contains(r.AssignedTo.Value)))
                        .Where(r => r.StatusID != 5 && r.StatusID != 6)
                        .Select(r => new { r.RequestID, r.StatusID })
                        .ToList();

                    if (activeRequests.Any())
                    {
                        MessageBox.Show(
                            $"Удаление невозможно: у выбранных пользователей есть активные заявки ({activeRequests.Count}). " +
                            "Сначала переведите эти заявки в статус \"Закрыта\" или \"Удалена\".",
                            "Нельзя удалить пользователя",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    int replacementUserId = context.Users
                        .Where(u => !ids.Contains(u.UserID) && u.Role == "Администратор")
                        .Select(u => (int?)u.UserID)
                        .FirstOrDefault() ?? Navigation.UserID;

                    foreach (var userId in ids)
                    {
                        context.Database.ExecuteSqlCommand(
                            "DELETE FROM dbo.NotificationReads WHERE UserID = @UserID",
                            new SqlParameter("@UserID", userId));
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.Notifications SET CreatedBy = NULL WHERE CreatedBy = @UserID",
                            new SqlParameter("@UserID", userId));
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.Notifications SET TargetUserID = NULL WHERE TargetUserID = @UserID",
                            new SqlParameter("@UserID", userId));
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.PlannedTasks SET AssignedTo = NULL WHERE AssignedTo = @UserID",
                            new SqlParameter("@UserID", userId));
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.PlannedTasks SET CreatedBy = @ReplacementUserID WHERE CreatedBy = @UserID",
                            new SqlParameter("@ReplacementUserID", replacementUserId),
                            new SqlParameter("@UserID", userId));
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.Requests SET AssignedTo = NULL WHERE AssignedTo = @UserID",
                            new SqlParameter("@UserID", userId));
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.Requests SET DeletedBy = NULL WHERE DeletedBy = @UserID",
                            new SqlParameter("@UserID", userId));
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.Requests SET CreatedBy = @ReplacementUserID WHERE CreatedBy = @UserID",
                            new SqlParameter("@ReplacementUserID", replacementUserId),
                            new SqlParameter("@UserID", userId));
                        context.Database.ExecuteSqlCommand(
                            "UPDATE dbo.StatusHistory SET ChangedBy = @ReplacementUserID WHERE ChangedBy = @UserID",
                            new SqlParameter("@ReplacementUserID", replacementUserId),
                            new SqlParameter("@UserID", userId));
                    }

                    var toDelete = context.Users.Where(u => ids.Contains(u.UserID)).ToList();
                    context.Users.RemoveRange(toDelete);
                    context.SaveChanges();
                }

                MessageBox.Show("Пользователи удалены!", "Успех",
                               MessageBoxButton.OK, MessageBoxImage.Information);
                LoadAllUsers();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка удаления: " + ex.Message, "Ошибка",
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void UpdateUserStatus(string message)
        {
            if (UserStatusMessage != null)
                UserStatusMessage.Text = message;
            if (UserInfoText != null && message.Contains("Загружено"))
                UserInfoText.Text = message;
        }

    }
}
