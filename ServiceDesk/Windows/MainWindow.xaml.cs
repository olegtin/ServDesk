using ServiceDesk.Models;
using ServiceDesk.Models.Partials;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
using System.Xml.XPath;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace ServiceDesk
{
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly Forms.NotifyIcon _notifyIcon;
        private readonly Drawing.Icon _normalTrayIcon;
        private readonly Drawing.Icon _alertTrayIcon = Drawing.SystemIcons.Warning;
        private readonly ImageSource _normalWindowIcon;
        private readonly ImageSource _alertWindowIcon;
        private readonly Dictionary<string, DateTime> _shownBalloonNotifications = new Dictionary<string, DateTime>();

        public MainWindow()
        {
            InitializeComponent();
            _normalTrayIcon = LoadApplicationIcon();
            _normalWindowIcon = ToImageSource(_normalTrayIcon);
            Icon = _normalWindowIcon;
            _alertWindowIcon = ToImageSource(_alertTrayIcon);
            _notifyIcon = new Forms.NotifyIcon
            {
                Icon = _normalTrayIcon,
                Text = "ServiceDesk",
                Visible = false
            };
            Border1.Visibility = Visibility.Collapsed;
            Border2.Visibility = Visibility.Collapsed;
            MainFrame.Navigate(new Pages.Authorization());
            var mainWindow = Application.Current.MainWindow as MainWindow;
            mainWindow.ResizeMode = ResizeMode.NoResize;
            mainWindow.Height = 600;
            mainWindow.Width = 400;
            mainWindow.Show();
            Navigation.MainFrame = MainFrame;
        }

        public void ShowWindowsNotification(int userId, int notificationId, string title, string message)
        {
            var normalizedTitle = string.IsNullOrWhiteSpace(title) ? "ServiceDesk" : title;
            var normalizedMessage = string.IsNullOrWhiteSpace(message) ? "У вас новое уведомление" : message;
            var notificationKey = $"{userId}:{notificationId}";

            if (_shownBalloonNotifications.ContainsKey(notificationKey))
                return;

            _shownBalloonNotifications[notificationKey] = DateTime.Now;

            _notifyIcon.Visible = true;
            _notifyIcon.ShowBalloonTip(
                5000,
                normalizedTitle,
                normalizedMessage,
                Forms.ToolTipIcon.Info);
        }

        public void ActivateTrayIcon()
        {
            _notifyIcon.Icon = _normalTrayIcon;
            _notifyIcon.Text = "ServiceDesk";
            _notifyIcon.Visible = true;
        }

        public void SetTrayUnreadState(bool hasUnread)
        {
            _notifyIcon.Visible = true;
            _notifyIcon.Icon = hasUnread ? _alertTrayIcon : _normalTrayIcon;
            _notifyIcon.Text = hasUnread
                ? "ServiceDesk: есть непрочитанные уведомления"
                : "ServiceDesk";
            Icon = hasUnread ? _alertWindowIcon : _normalWindowIcon;
            if (TaskbarItemInfo != null)
                TaskbarItemInfo.Overlay = hasUnread ? _alertWindowIcon : null;
        }

        public void ResetTrayIcon()
        {
            _notifyIcon.Icon = _normalTrayIcon;
            _notifyIcon.Text = "ServiceDesk";
            _notifyIcon.Visible = false;
            Icon = _normalWindowIcon;
            if (TaskbarItemInfo != null)
                TaskbarItemInfo.Overlay = null;
        }

        private static Drawing.Icon LoadApplicationIcon()
        {
            try
            {
                var resourceInfo = Application.GetResourceStream(new Uri("pack://application:,,,/Resources/gaZ.ico"));
                if (resourceInfo != null)
                {
                    using (var stream = resourceInfo.Stream)
                    using (var icon = new Drawing.Icon(stream))
                    {
                        return (Drawing.Icon)icon.Clone();
                    }
                }

                return Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)
                       ?? Drawing.SystemIcons.Application;
            }
            catch
            {
                return Drawing.SystemIcons.Application;
            }
        }

        private static ImageSource ToImageSource(Drawing.Icon icon)
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        public void CenterWindow()
        {
            var screen = SystemParameters.WorkArea;
            this.Left = (screen.Width - this.Width) / 2 + screen.Left;
            this.Top = (screen.Height - this.Height) / 2 + screen.Top;
        }
        public TextBlock UserTextBlock => userr;
        public TextBlock UserRoleTextBlock => userRole;
        public void SetUserText(string text)
        {
            userr.Text = text; 
        }

        private void LogOutBtn_Click_2(object sender, RoutedEventArgs e)
        {
            var mainWindow = Application.Current.MainWindow as MainWindow;
            Border1.Visibility = Visibility.Collapsed;
            Border2.Visibility = Visibility.Collapsed;
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.MinWidth = 400;
            mainWindow.MinHeight = 600;
            mainWindow.ResizeMode = ResizeMode.NoResize;
            mainWindow.Height = 600;
            mainWindow.Width = 400;
            CenterWindow();
            mainWindow.Show();
            Navigation.CurrentUser = null;
            SetUserText(null);
            ResetTrayIcon();
            MainFrame.Navigate(new Pages.Authorization());
            Navigation.MainFrame = MainFrame;

        }

        protected override void OnClosed(EventArgs e)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            base.OnClosed(e);
        }
    }
}
