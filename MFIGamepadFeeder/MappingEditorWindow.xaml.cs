using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Windows;
using System.Windows.Controls;
using MFIGamepadShared;
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
            SimplifiedHidPreview = new SimplifiedHidPreview();
        }

        private string OpenedFileName { get; set; }

        public ReactiveCollection<ListViewItem> ListViewMappingItems { get; set; }
        public ReactiveCollection<ListViewItem> VirtualKeysListViewMappingItems { get; set; }
        private SimplifiedHidPreview SimplifiedHidPreview { get; }

        private void Grid_Loaded(object sender, RoutedEventArgs e)
        {
            HidDeviceCombobox.ItemsSource = SimplifiedHidPreview.HidManager.FoundDevices;
            SimplifiedHidPreview.CurrentHidState
                .ObserveOn(Application.Current.Dispatcher)
                .Subscribe(s => HidPreviewLabel.Content = s.Length > 0 ? $"Preview: {s}" : string.Empty);
        }

        private void Grid_Unloaded(object sender, RoutedEventArgs e)
        {
            SimplifiedHidPreview.Dispose();
        }

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


            VirtualKeysListViewMappingItems = new ReactiveCollection<ListViewItem>();
            foreach (var virtualKeyMappingItem in mapping.VirtualKeysItems)
            {
                var item = new ListViewItem
                {
                    Content = virtualKeyMappingItem,
                    ContentTemplate = (DataTemplate) FindResource("VirtualKeysItemTemplate")
                };

                VirtualKeysListViewMappingItems.Add(item);
            }

            VirtualKeysItemsListView.ItemsSource = VirtualKeysListViewMappingItems;

            SaveButton.IsEnabled = true;
            AddNewItemButton.IsEnabled = true;
            AddNewVirtualKeyButton.IsEnabled = true;
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
            var newMapping = new GamepadMapping(new List<GamepadMappingItem>(), new List<VirtualKeyMappingItem>());
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
                var readAllText = File.ReadAllText(openFileDialog.FileName);
                var mapping = JsonConvert.DeserializeObject<GamepadMapping>(readAllText);
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
                try
                {
                    using (var outputFile = new StreamWriter(saveFileDialog.FileName))
                    {
                        URLLabel.Content = saveFileDialog.FileName;
                        var mappingItems = MappingItemsListView.Items.SourceCollection.Cast<ListViewItem>().Select(item => item.Content as GamepadMappingItem).ToList();
                        var virtualKeysMappingItems = VirtualKeysItemsListView.Items.SourceCollection.Cast<ListViewItem>().Select(item => item.Content as VirtualKeyMappingItem).ToList();

                        var newMapping = new GamepadMapping(mappingItems, virtualKeysMappingItems);

                        await outputFile.WriteAsync(JsonConvert.SerializeObject(newMapping, Formatting.Indented));
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        private void RemoveMappingItemClicked(object sender, RoutedEventArgs e)
        {
            var item = (GamepadMappingItem) ((FrameworkElement) sender).DataContext;
            ListViewMappingItems.Remove(ListViewMappingItems.First(viewItem => viewItem.Content == item));
        }


        private void RemoveVirtualKeyItemClicked(object sender, RoutedEventArgs e)
        {
            var item = (VirtualKeyMappingItem) ((FrameworkElement) sender).DataContext;
            VirtualKeysListViewMappingItems.Remove(VirtualKeysListViewMappingItems.First(viewItem => viewItem.Content == item));
        }

        private void MappingType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedItem == null)
            {
                return;
            }
            var item = (GamepadMappingItem) ((FrameworkElement) sender).DataContext;

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
            var item = (GamepadMappingItem) ((FrameworkElement) sender).DataContext;

            var newAxisType = (AxisType) ((ComboBox) sender).SelectedItem;
            item.AxisType = newAxisType;
        }

        private void InvertAxis_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (((CheckBox) sender).IsChecked == null)
            {
                return;
            }
            var item = (GamepadMappingItem) ((FrameworkElement) sender).DataContext;

            var newIsChecked = ((CheckBox) sender).IsChecked;
            item.InvertAxis = newIsChecked;
        }

        private void ConvertAxis_CheckedChanged(object sender, RoutedEventArgs e)
        {
            if (((CheckBox) sender).IsChecked == null)
            {
                return;
            }
            var item = (GamepadMappingItem) ((FrameworkElement) sender).DataContext;

            var newIsChecked = ((CheckBox) sender).IsChecked;
            item.ConvertAxis = newIsChecked;
        }

        private void ButtonType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedItem == null)
            {
                return;
            }
            var item = (GamepadMappingItem) ((FrameworkElement) sender).DataContext;

            var newButtonType = (XInputGamepadButtons) ((ComboBox) sender).SelectedItem;
            item.ButtonType = newButtonType;
        }

        private void DPadType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (((ComboBox) sender).SelectedItem == null)
            {
                return;
            }
            var item = (GamepadMappingItem) ((FrameworkElement) sender).DataContext;

            var newDpadType = (XInputGamepadButtons) ((ComboBox) sender).SelectedItem;
            item.ButtonType = newDpadType;
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


        private void AddNewVirtualKeyItem_Click(object sender, RoutedEventArgs e)
        {
            var newVirtualKeyItem = new VirtualKeyMappingItem();

            var newListViewItem = new ListViewItem
            {
                Content = newVirtualKeyItem,
                ContentTemplate = (DataTemplate) FindResource("VirtualKeysItemTemplate")
            };

            VirtualKeysListViewMappingItems.Add(newListViewItem);
        }

        private void HidDeviceCombobox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HidDeviceCombobox.SelectedItem != null)
            {
                SimplifiedHidPreview.PlugInToHidDeviceAndStartLoop((HidDeviceRepresentation) HidDeviceCombobox.SelectedItem);
            }
        }

        private void VirtualSourceKey_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedItem = ((ComboBox) sender).SelectedItem;
            if (selectedItem == null)
            {
                return;
            }


            var item = (VirtualKeyMappingItem) ((FrameworkElement) sender).DataContext;
            var virtualKeyIndex = Convert.ToInt32(((ComboBox) sender).Tag);
            while (virtualKeyIndex > item.SourceKeys.Count - 1)
            {
                item.SourceKeys.Add(null);
            }

            item.SourceKeys[virtualKeyIndex] = selectedItem as XInputGamepadButtons?;
        }

        private void SelectVirtualKeyComboBox_Loaded(object sender, RoutedEventArgs e)
        {
            var keys = new List<object>
            {
                "",
                XInputGamepadButtons.Start,
                XInputGamepadButtons.Back,
                XInputGamepadButtons.LeftStick,
                XInputGamepadButtons.RightStick,
                XInputGamepadButtons.LBumper,
                XInputGamepadButtons.RBumper,
                XInputGamepadButtons.A,
                XInputGamepadButtons.B,
                XInputGamepadButtons.X,
                XInputGamepadButtons.Y,
                XInputGamepadButtons.DpadUp,
                XInputGamepadButtons.DpadDown,
                XInputGamepadButtons.DpadLeft,
                XInputGamepadButtons.DpadRight
            };

            ((ComboBox) sender).ItemsSource = keys;
        }
    }
}