using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using MFIGamepadShared.Configuration;
using Microsoft.Win32;
using Newtonsoft.Json;
using Reactive.Bindings;
using vGenWrapper;

namespace MFIGamepadFeeder
{
    public partial class MappingEditorWindow : Window
    {
        public MappingEditorWindow()
        {
            InitializeComponent();
        }

        private string OpenedFileName { get; set; }

        public ReactiveCollection<ListViewItem> ListViewMappingItems { get; set; }

        private void BindMappingToUi(GamepadMapping mapping)
        {
            ListViewMappingItems = new ReactiveCollection<ListViewItem>();
            foreach (var mappingItem in mapping.MappingItems)
            {
                var item = new ListViewItem
                {
                    Content = mappingItem,
                    ContentTemplate = DataTemplateForMappingType(mappingItem.Type)
                };

                ListViewMappingItems.Add(item);
            }

            MappingItemsListView.ItemsSource = ListViewMappingItems;
            MappingItemsListView.AlternationCount = ListViewMappingItems.Count + 1;

            SaveButton.IsEnabled = true;
            AddNewItemButton.IsEnabled = true;
        }

        private DataTemplate DataTemplateForMappingType(GamepadMappingItemType type)
        {
            switch (type)
            {
                case GamepadMappingItemType.Axis:
                    return (DataTemplate) FindResource("AxisMappingItemTemplate");
                case GamepadMappingItemType.Button:
                    return (DataTemplate) FindResource("ButtonMappingItemTemplate");
                case GamepadMappingItemType.DPad:
                    return (DataTemplate) FindResource("DpadMappingItemTemplate");
                case GamepadMappingItemType.Empty:
                    return (DataTemplate) FindResource("EmptyMappingItemTemplate");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            var newMapping = new GamepadMapping(new List<GamepadMappingItem>());
            SaveButton_Click(null, null);
            BindMappingToUi(newMapping);
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                InitialDirectory = AppDomain.CurrentDomain.BaseDirectory,
                Multiselect = false,
                Filter = "MFI Mapping (*.mfimapping)|*.mfimapping"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                URLLabel.Content = openFileDialog.FileName;
                OpenedFileName = openFileDialog.FileName;
                var mapping = JsonConvert.DeserializeObject<GamepadMapping>(File.ReadAllText(openFileDialog.FileName));
                BindMappingToUi(mapping);
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "MFI Mapping (*.mfimapping)|*.mfimapping"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                using (var outputFile = new StreamWriter(saveFileDialog.FileName))
                {
                    var mappingItems = MappingItemsListView.Items.SourceCollection.Cast<ListViewItem>().Select(item => item.Content as GamepadMappingItem).ToList();
                    var newMapping = new GamepadMapping(mappingItems);

                    await outputFile.WriteAsync(JsonConvert.SerializeObject(newMapping, Formatting.Indented));
                }
            }
        }

        private void RemoveMappingItemClicked(object sender, RoutedEventArgs e)
        {
            var item = (GamepadMappingItem) ((FrameworkElement) sender).DataContext;
            ListViewMappingItems.Remove(ListViewMappingItems.First(viewItem => viewItem.Content == item));
        }

        private void MappingType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedItem == null)
            {
                return;
            }
            var item = (GamepadMappingItem)((FrameworkElement)sender).DataContext;

            var newItemType = (GamepadMappingItemType) ((ComboBox) sender).SelectedItem;
            item.Type = newItemType;

            ListViewMappingItems.First(viewItem => viewItem.Content == item).ContentTemplate = DataTemplateForMappingType(newItemType);
        }

        private void AxisType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedItem == null)
            {
                return;
            }
            var item = (GamepadMappingItem)((FrameworkElement)sender).DataContext;

            var newAxisType = (AxisType) ((ComboBox) sender).SelectedItem;
            item.AxisType = newAxisType;
        }

        private void InvertAxis_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (((CheckBox) sender).IsChecked == null)
            {
                return;
            }
            var item = (GamepadMappingItem)((FrameworkElement)sender).DataContext;

            var newIsChecked = ((CheckBox) sender).IsChecked;
            item.InvertAxis = newIsChecked;
        }

        private void ConvertAxis_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (((CheckBox) sender).IsChecked == null)
            {
                return;
            }
            var item = (GamepadMappingItem)((FrameworkElement)sender).DataContext;

            var newIsChecked = ((CheckBox) sender).IsChecked;
            item.ConvertAxis = newIsChecked;
        }

        private void ButtonType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedItem == null)
            {
                return;
            }
            var item = (GamepadMappingItem)((FrameworkElement)sender).DataContext;

            var newButtonType = (XInputGamepadButtons) ((ComboBox) sender).SelectedItem;
            item.ButtonType = newButtonType;
        }

        private void DPadType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedItem == null)
            {
                return;
            }
            var item = (GamepadMappingItem)((FrameworkElement)sender).DataContext;

            var newDpadType = (XInputGamepadDPadButtons) ((ComboBox) sender).SelectedItem;            
            item.DPadType = newDpadType;
        }

        private void AddNewMappingItem_Click(object sender, RoutedEventArgs e)
        {
            var newMappingItem = new GamepadMappingItem
            {
                Type = GamepadMappingItemType.Empty,
                ConvertAxis = false,
                InvertAxis = false
            };

            var newListViewItem = new ListViewItem
            {
                Content = newMappingItem,
                ContentTemplate = DataTemplateForMappingType(newMappingItem.Type)
            };

            ListViewMappingItems.Add(newListViewItem);
            MappingItemsListView.AlternationCount = ListViewMappingItems.Count + 1;
        }
        
    }
}