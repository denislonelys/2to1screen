using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace TwoTo1Screen.Views
{
    public partial class DeveloperView : UserControl
    {
        private const string VkUrl = "https://vk.ru/dirroladd";

        public DeveloperView()
        {
            InitializeComponent();
        }

        private void BtnVk_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = VkUrl,
                    UseShellExecute = true
                });
            }
            catch
            {
                System.Windows.MessageBox.Show("Не удалось открыть ссылку: " + VkUrl,
                    "2to1screen", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
    }
}
