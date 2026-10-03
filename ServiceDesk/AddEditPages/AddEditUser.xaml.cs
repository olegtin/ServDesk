using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
using ServiceDesk.Security;

namespace ServiceDesk.AddEditPages
{
    /// <summary>
    /// Логика взаимодействия для AddEditUser.xaml
    /// </summary>
    public partial class AddEditUser : Page
    {
        private ServiceDesk.Models.Users _currentUser;
        bool isEdit = false;
        private bool _isFormattingPhone;
        public AddEditUser(ServiceDesk.Models.Users CurrentUs)
        {
            InitializeComponent();
            if (CurrentUs != null)
            {
                _currentUser = CurrentUs;

                isEdit = true;
            }
            DataContext = _currentUser;
        }
        public AddEditUser()
        {
            InitializeComponent();
            _currentUser = new Users();
            DataContext = _currentUser;
            isEdit = false;

        }
        private void TelephoneNum_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = FreePhoneCheckBox?.IsChecked != true && !IsTextAllowed(e.Text);
        }
        private void TelephoneNum_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (e.DataObject.GetDataPresent(typeof(string)))
            {
                string text = (string)e.DataObject.GetData(typeof(string));
                if (FreePhoneCheckBox?.IsChecked != true && !IsTextAllowed(text))
                {
                    e.CancelCommand();
                }
            }
            else
            {
                e.CancelCommand();
            }
        }
        private static bool IsTextAllowed(string text)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(text, @"^[0-9]+$");
        }

        private void TelephoneNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isFormattingPhone || FreePhoneCheckBox?.IsChecked == true)
                return;

            _isFormattingPhone = true;
            string digits = new string((TelephoneNum.Text ?? "").Where(char.IsDigit).ToArray());
            if (digits.Length == 0)
            {
                TelephoneNum.Text = "";
                TelephoneNum.CaretIndex = 0;
                _isFormattingPhone = false;
                return;
            }

            if (digits.StartsWith("8"))
                digits = "7" + digits.Substring(1);
            if (digits.StartsWith("7"))
                digits = digits.Substring(1);
            if (digits.Length > 10)
                digits = digits.Substring(0, 10);

            TelephoneNum.Text = FormatRussianPhone(digits);
            TelephoneNum.CaretIndex = TelephoneNum.Text.Length;
            _isFormattingPhone = false;
        }

        private static string FormatRussianPhone(string digits)
        {
            var builder = new StringBuilder("+7");
            if (digits.Length > 0)
                builder.Append(" (" + digits.Substring(0, Math.Min(3, digits.Length)));
            if (digits.Length >= 3)
                builder.Append(")");
            if (digits.Length > 3)
                builder.Append(" " + digits.Substring(3, Math.Min(3, digits.Length - 3)));
            if (digits.Length > 6)
                builder.Append("-" + digits.Substring(6, Math.Min(2, digits.Length - 6)));
            if (digits.Length > 8)
                builder.Append("-" + digits.Substring(8, Math.Min(2, digits.Length - 8)));
            return builder.ToString();
        }

        private void FreePhoneCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (TelephoneNum == null)
                return;

            TelephoneNum.MaxLength = FreePhoneCheckBox.IsChecked == true ? 50 : 18;
            if (FreePhoneCheckBox.IsChecked != true)
                TelephoneNum_TextChanged(TelephoneNum, null);
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var context = new RequestsEntities())
                {
                    string password = PasswordBox.Password;
                    _currentUser.LastName = _currentUser.LastName?.Trim();
                    _currentUser.FirstName = _currentUser.FirstName?.Trim();
                    _currentUser.Patronymic = _currentUser.Patronymic?.Trim();
                    _currentUser.Department = _currentUser.Department?.Trim();
                    _currentUser.Email = _currentUser.Email?.Trim();
                    _currentUser.TelephoneNum = GetPhoneValueForSave();
                    _currentUser.Login = _currentUser.Login?.Trim();
                    _currentUser.Role = ComboRole.SelectedValue?.ToString() ?? _currentUser.Role;

                    if (string.IsNullOrWhiteSpace(_currentUser.LastName))
                    {
                        MessageBox.Show("Введите фамилию пользователя.", "Ошибка валидации",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(_currentUser.FirstName))
                    {
                        MessageBox.Show("Введите имя пользователя.", "Ошибка валидации",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(_currentUser.Role))
                    {
                        MessageBox.Show("Выберите роль пользователя.", "Ошибка валидации",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(_currentUser.Login))
                    {
                        MessageBox.Show("Введите логин пользователя.", "Ошибка валидации",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!isEdit && string.IsNullOrWhiteSpace(password))
                    {
                        MessageBox.Show("Введите пароль пользователя.", "Ошибка валидации",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    bool loginExists = context.Users.Any(u =>
                        u.Login == _currentUser.Login &&
                        (!isEdit || u.UserID != _currentUser.UserID));
                    if (loginExists)
                    {
                        MessageBox.Show("Пользователь с таким логином уже существует.", "Ошибка валидации",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(password))
                    {
                        _currentUser.Password = PasswordHasher.HashPassword(password);
                    }

                    if (isEdit)
                    {
                        context.Users.Attach(_currentUser);
                        var entry = context.Entry(_currentUser);
                        entry.State = System.Data.Entity.EntityState.Modified;
                    }
                    else
                    {
                        context.Users.Add(_currentUser);
                    }

                    context.SaveChanges();
                    MessageBox.Show("Информация сохранена");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
                return;
            }

            if (Navigation.MainFrame?.CanGoBack == true)
                Navigation.MainFrame.GoBack();
            else
                Navigation.MainFrame?.Navigate(new Pages.AdminDashboard());
        }
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            Navigation.MainFrame.GoBack();
        }

        private string GetPhoneValueForSave()
        {
            var phone = TelephoneNum?.Text?.Trim();
            if (string.IsNullOrWhiteSpace(phone) || phone == "+7")
                return null;

            return phone;
        }
    }
}
