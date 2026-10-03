using System;
using System.Collections.Generic;
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

namespace ServiceDesk.Pages
{
    /// <summary>
    /// Логика взаимодействия для Authorization.xaml
    /// </summary>
    public partial class Authorization : Page
    {
        public Authorization()
        {
            InitializeComponent();
        }

        private void Login_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var context = RequestsEntitiesContext.GetContext())
                {
                    string login = Login.Text.Trim();
                    string password = PasswordBox.Password;
                    Users currentUser = context.Users.FirstOrDefault(p => p.Login == login);

                    if (currentUser != null && PasswordHasher.VerifyPassword(password, currentUser.Password))
                    {
                        if (!PasswordHasher.IsHashedPassword(currentUser.Password))
                        {
                            currentUser.Password = PasswordHasher.HashPassword(password);
                            context.SaveChanges();
                        }

                        string userName = string.Join(" ", currentUser.LastName, currentUser.FirstName, currentUser.Patronymic);
                        MessageBox.Show($"Добро пожаловать {userName}.\nВаша роль - {currentUser.Role}");
                        Navigation.CurrentUser = currentUser;
                        var mainWindow = Application.Current.MainWindow as MainWindow;
                        if (mainWindow != null)
                        {
                            mainWindow.Height = 700;
                            mainWindow.Width = 1400;
                            mainWindow.MinWidth = 1400;
                            mainWindow.MinHeight = 700;
                            mainWindow.ResizeMode = ResizeMode.CanResize;
                            mainWindow.WindowState = WindowState.Maximized;
                            mainWindow.Border1.Visibility = Visibility.Visible;
                            mainWindow.Border2.Visibility = Visibility.Visible;
                            mainWindow.UserTextBlock.Text = userName;
                            mainWindow.UserRoleTextBlock.Text = string.Join(" ", currentUser.Role);
                            mainWindow.ActivateTrayIcon();
                        }
                        Navigation.MainFrame.Navigate(new Pages.AdminDashboard());

                    }
                    else
                    {
                        MessageBox.Show("Неверный логин или пароль");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message.ToString());
            }
        }
    }
}
