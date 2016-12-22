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
using System.Windows.Shapes;
using MFIGamepadFeeder.Properties;

namespace MFIGamepadFeeder
{
    /// <summary>
    /// Interaction logic for OptionsWindow.xaml
    /// </summary>
    public partial class OptionsWindow : Window
    {
        public OptionsWindow()
        {
            InitializeComponent();
        }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            AutoPlugInCheckBox.IsChecked = Settings.Default.AutoPlugIn;
            StartMinimizedCheckBox.IsChecked = Settings.Default.StartMinimized;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (AutoPlugInCheckBox.IsChecked != null) Settings.Default.AutoPlugIn = AutoPlugInCheckBox.IsChecked.Value;
            if (StartMinimizedCheckBox.IsChecked != null) Settings.Default.StartMinimized = StartMinimizedCheckBox.IsChecked.Value;
            Settings.Default.Save();
            Close();
        }

        private void StartMinimized_MouseDown(object sender, MouseButtonEventArgs e)
        {
            StartMinimizedCheckBox.IsChecked = !StartMinimizedCheckBox.IsChecked;
        }

        private void AutoPlugIn_MouseDown(object sender, MouseButtonEventArgs e)
        {
            AutoPlugInCheckBox.IsChecked = !AutoPlugInCheckBox.IsChecked;
        }
    }
}
