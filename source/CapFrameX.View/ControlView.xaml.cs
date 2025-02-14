﻿using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Extensions;
using CapFrameX.Extensions.NetStandard;
using CapFrameX.ViewModel;
using System;
using System.IO;
using System.Windows;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Reactive.Linq;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using CapFrameX.View.Controls;
using System.Threading.Tasks;

namespace CapFrameX.View
{
    /// <summary>
    /// Interaction logic for ControlView.xaml
    /// </summary>
    public partial class ControlView : UserControl
    {
        private ControlViewModel _viewModel => DataContext as ControlViewModel;

        private const int SEARCH_REFRESH_DELAY_MS = 100;
        private readonly CollectionViewSource _recordInfoCollection;

        private bool CreateFolderDialogIsOpen;

        private bool FixedExpanderPosition => _viewModel.FixedExpanderPosition;

        private bool RecordInfoExpanderinitialPosition => _viewModel.AppConfiguration.IsRecordInfoExpanded;

        private string ObservedDirectory
            => _viewModel.AppConfiguration.ObservedDirectory;
        private string CaptureRootDirectory
            => _viewModel.AppConfiguration.CaptureRootDirectory;

        private string DefaultPath = Path.Combine
                        (Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        @"CapFrameX\Captures\");

        private bool IsDragDropActive;
        private TreeViewItem ObservedTreeViewItem;

        public ControlView()
        {
            InitializeComponent();
            SetHeaders();

            if (FixedExpanderPosition)
                Expander.IsExpanded = true;

            if (RecordInfoExpanderinitialPosition)
                RecordInfoExpander.IsExpanded = true;

            _recordInfoCollection = (CollectionViewSource)Resources["RecordInfoListKey"];
            Observable.FromEventPattern<TextChangedEventArgs>(RecordSearchBox, "TextChanged")
                .Throttle(TimeSpan.FromMilliseconds(SEARCH_REFRESH_DELAY_MS))
                .ObserveOnDispatcher()
                .Subscribe(t => _recordInfoCollection.View.Refresh());

            _viewModel.TreeViewUpdateStream.Subscribe(_ => BuildTreeView());

            _viewModel.CreateFolderdialogIsOpenStream
                .SelectMany(isOpen =>
                {
                    if (isOpen)
                    {
                        return Observable.Return(true);
                    }
                    return Observable.Return(false).Delay(TimeSpan.FromMilliseconds(500));
                })
                .Subscribe(isOpen => CreateFolderDialogIsOpen = isOpen);

            BuildTreeView();
            SetSortSettings(_viewModel.AppConfiguration);

            Observable.FromEventPattern(Expander, "MouseLeave")
                .Where(_ => !trvStructure.ContextMenu.IsOpen)
                .Where(_ => Expander.IsExpanded)
                .Where(isOpen => !CreateFolderDialogIsOpen)
                .Where(_ => !FixedExpanderPosition)
                .ObserveOnDispatcher()
                .Subscribe(_ =>
                {
                    Expander.IsExpanded = false;
                });


        }

        private void BuildTreeView()
        {
            var root = CreateTreeViewRoot();
            CreateTreeViewRecursive(trvStructure.Items[0] as TreeViewItem);
            JumpToObservedDirectoryItem(root, out var directoryFound);

            if ((ExtractFullPath(CaptureRootDirectory) == ObservedDirectory))
                root.IsSelected = true;

            if (!directoryFound)
            {
                if (Directory.Exists(ObservedDirectory))
                    _viewModel.RootDirectory = ObservedDirectory;
                else
                {
                    _viewModel.AppConfiguration.ObservedDirectory = DefaultPath;
                    _viewModel.RootDirectory = DefaultPath;
                }
                BuildTreeView();
            }
        }

        private TreeViewItem CreateTreeViewRoot()
        {
            trvStructure.Items.Clear();
            var mainfoldername = new DirectoryInfo(ExtractFullPath(CaptureRootDirectory));
            var rootNode = CreateTreeItem(mainfoldername, mainfoldername.Name);
            trvStructure.Items.Add(rootNode);
            rootNode.IsExpanded = true;
            return rootNode;
        }

        private TreeViewItem CreateTreeItem(object o, string name)
        {
            TreeViewItem item = new TreeViewItem
            {
                Header = name,
                Tag = o
            };
            item.Items.Add("Loading...");
            return item;
        }

        private void CreateTreeViewRecursive(TreeViewItem item)
        {
            if ((item.Items.Count == 1) && (item.Items[0] is string))
            {
                item.Items.Clear();

                DirectoryInfo expandedDir = null;
                if (item.Tag is DriveInfo)
                    expandedDir = (item.Tag as DriveInfo).RootDirectory;
                if (item.Tag is DirectoryInfo)
                    expandedDir = (item.Tag as DirectoryInfo);
                try
                {
                    foreach (DirectoryInfo subDir in expandedDir.GetDirectories())
                    {
                        var subItem = CreateTreeItem(subDir, subDir.ToString());
                        item.Items.Add(subItem);
                        CreateTreeViewRecursive(subItem);
                    }
                }
                catch { }
            }
        }

        private void JumpToObservedDirectoryItem(TreeViewItem tvi, out bool directoryFound)
        {
            directoryFound = false;
            if (tvi == null)
                return;

            if ((tvi.Tag as DirectoryInfo).FullName == ObservedDirectory)
            {
                tvi.BringIntoView();
                tvi.IsSelected = true;
                directoryFound = true;
                return;
            }
            else
            {
                tvi.IsExpanded = false;
            }

            if (tvi.HasItems)
            {
                foreach (var item in tvi.Items)
                {
                    TreeViewItem temp = item as TreeViewItem;
                    JumpToObservedDirectoryItem(temp, out directoryFound);
                    if (directoryFound) break;
                }
            }
        }

        private void SetSortSettings(IAppConfiguration appConfiguration)
        {
            string sortMemberPath = appConfiguration.RecordingListSortMemberPath;
            var direction = appConfiguration.RecordingListSortDirection.ConvertToEnum<ListSortDirection>();
            var collectionView = CollectionViewSource.GetDefaultView(RecordDataGrid.ItemsSource);

            collectionView.SortDescriptions.Clear();
            AddSortColumn(RecordDataGrid, sortMemberPath, direction);
            AddSortColumnsByMemberPath(RecordDataGrid, direction, sortMemberPath);
        }

        private void RecordDataGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            var dataGrid = (DataGrid)sender;
            var appConfiguration = _viewModel.AppConfiguration;
            var collectionView = CollectionViewSource.GetDefaultView(dataGrid.ItemsSource);

            ListSortDirection direction = ListSortDirection.Ascending;
            if (collectionView.SortDescriptions.FirstOrDefault().PropertyName == e.Column.SortMemberPath)
                direction = collectionView.SortDescriptions.FirstOrDefault().Direction ==
                    ListSortDirection.Descending ? ListSortDirection.Ascending : ListSortDirection.Descending;

            collectionView.SortDescriptions.Clear();
            AddSortColumn((DataGrid)sender, e.Column.SortMemberPath, direction);
            AddSortColumnsByMemberPath((DataGrid)sender, direction, e.Column.SortMemberPath);

            appConfiguration.RecordingListSortMemberPath = e.Column.SortMemberPath;
            appConfiguration.RecordingListSortDirection = direction.ConvertToString();

            e.Handled = true;
        }

        private void AddSortColumn(DataGrid sender, string sortColumn, ListSortDirection direction)
        {
            var collectionView = CollectionViewSource.GetDefaultView(sender.ItemsSource);
            collectionView.SortDescriptions.Add(new SortDescription(sortColumn, direction));

            foreach (var col in sender.Columns.Where(x => x.SortMemberPath == sortColumn))
            {
                col.SortDirection = direction;
            }
        }

        private void AddSortColumnsByMemberPath(DataGrid dataGrid, ListSortDirection sortDirection, string sortMemberPath)
        {
            if (sortMemberPath == "GameName")
            {
                AddSortColumn(dataGrid, "CreationTimestamp", sortDirection);
            }
            else if (sortMemberPath != "CreationTimestamp")
            {
                AddSortColumn(dataGrid, "GameName", sortDirection);
                AddSortColumn(dataGrid, "CreationTimestamp", sortDirection);
            }
        }

        private void RecordInfoListOnFilter(object sender, FilterEventArgs e)
        {
            e.FilterCollectionByText<IFileRecordInfo>(RecordSearchBox.Text,
                                        (record, word) => record.CombinedInfo.NullSafeContains(word, true)
                                        || record.GameName.NullSafeContains(word, true)
                                        || record.ProcessorName.NullSafeContains(word, true)
                                        || record.GraphicCardName.NullSafeContains(word, true)
                                        || record.SystemRamInfo.NullSafeContains(word, true)
                                        || record.Comment.NullSafeContains(word, true));
        }

        private void DataGridRow_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            _viewModel.OnRecordSelectByDoubleClick();
        }

        private void ScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer scv = (ScrollViewer)sender;
            scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta / 10);
            e.Handled = true;
        }



        private void TreeViewItem_Selected(object sender, RoutedEventArgs e)
        {
            if (!IsDragDropActive) // When using D&D, select items to highlight them, but don't change directory
            {
                TreeViewItem item = e.Source as TreeViewItem;               
                _viewModel.RecordObserver.ObserveDirectory((item.Tag as DirectoryInfo).FullName);

                // Save the TreeviewItem of the observed directory
                ObservedTreeViewItem = item;
            }
        }

        private string ExtractFullPath(string path)
        {
            if (path.Contains(@"MyDocuments\CapFrameX\Captures"))
            {
                var documentFolder = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                path = Path.Combine(documentFolder, @"CapFrameX\Captures");
            }

            return path;
        }

        private void RootFolder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var result = _viewModel.OnSelectRootFolder();
            if (result)
            {
                BuildTreeView();
            }
        }

        private void TreeView_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            TreeViewItem treeViewItem = VisualUpwardSearch(e.OriginalSource as DependencyObject);

            if (treeViewItem != null)
            {
                treeViewItem.Focus();
                e.Handled = true;
            }
        }

        static TreeViewItem VisualUpwardSearch(DependencyObject source)
        {
            while (source != null && !(source is TreeViewItem))
                source = VisualTreeHelper.GetParent(source);

            return source as TreeViewItem;
        }

        private void TextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            Keyboard.ClearFocus();
            _viewModel.SaveDescriptions();
            e.Handled = true;
        }

        private void RecordDataGrid_PreviewLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (_viewModel.SelectedRecordInfo == null)
                return;

            var myCell = (sender as MultiSelectionDataGrid).CurrentCell;
            if (myCell.Column.Header.ToString() == "Comment")
            {
                if (_viewModel.CustomComment != _viewModel.SelectedRecordInfo.Comment)
                {
                    _viewModel.SelectedRecordInfo.Comment = _viewModel.CustomComment;
                }

            }
            else if (myCell.Column.Header.ToString() == "CPU")
            {
                if (_viewModel.CustomCpuDescription != _viewModel.SelectedRecordInfo.ProcessorName)
                {
                    _viewModel.SelectedRecordInfo.ProcessorName = _viewModel.CustomCpuDescription;
                }

            }
            else if (myCell.Column.Header.ToString() == "GPU")
            {
                if (_viewModel.CustomGpuDescription != _viewModel.SelectedRecordInfo.GraphicCardName)
                {
                    _viewModel.SelectedRecordInfo.GraphicCardName = _viewModel.CustomGpuDescription;
                }

            }
            else if (myCell.Column.Header.ToString() == "RAM")
            {
                if (_viewModel.CustomRamDescription != _viewModel.SelectedRecordInfo.SystemRamInfo)
                {
                    _viewModel.SelectedRecordInfo.SystemRamInfo = _viewModel.CustomRamDescription;
                }

            }
        }

        private void RecordDataGrid_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (_viewModel.SelectedRecordInfo == null)
                return;

            var myCell = (sender as MultiSelectionDataGrid).CurrentCell;
            if (myCell.Column.Header.ToString() == "Comment")
            {
                if (_viewModel.CustomComment != _viewModel.SelectedRecordInfo.Comment)
                {
                    _viewModel.CustomComment = _viewModel.SelectedRecordInfo.Comment;
                    _viewModel.SaveDescriptions();
                }
            }
            else if (myCell.Column.Header.ToString() == "CPU")
            {
                if (_viewModel.CustomCpuDescription != _viewModel.SelectedRecordInfo.ProcessorName)
                {
                    _viewModel.CustomCpuDescription = _viewModel.SelectedRecordInfo.ProcessorName;
                    _viewModel.SaveDescriptions();
                }
            }
            else if (myCell.Column.Header.ToString() == "GPU")
            {
                if (_viewModel.CustomGpuDescription != _viewModel.SelectedRecordInfo.GraphicCardName)
                {
                    _viewModel.CustomGpuDescription = _viewModel.SelectedRecordInfo.GraphicCardName;
                    _viewModel.SaveDescriptions();
                }
            }
            else if (myCell.Column.Header.ToString() == "RAM")
            {
                if (_viewModel.CustomRamDescription != _viewModel.SelectedRecordInfo.SystemRamInfo)
                {
                    _viewModel.CustomRamDescription = _viewModel.SelectedRecordInfo.SystemRamInfo;
                    _viewModel.SaveDescriptions();
                }
            }
        }

        private void RecordDataGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var myCell = (sender as MultiSelectionDataGrid).CurrentCell;
            // The TextBlock control doesn't support vertical text alignment.
            // Must be a child of a Grid or a Border to be able to center the text.
            // myCell.Column.GetCellContent(myCell.Item).VerticalAlignment = VerticalAlignment.Center;
            RecordDataGrid.SelectedItem = myCell.Item;
        }

        private void RecordInfoExpander_Change(object sender, RoutedEventArgs e)
        {
            _viewModel.AppConfiguration.IsRecordInfoExpanded = RecordInfoExpander.IsExpanded;
        }

        private void RecordDataGrid_ColumnDisplayIndexChanged(object sender, DataGridColumnEventArgs e)
        {
            _viewModel.AppConfiguration.RecordListHeaderOrder = new int[7]
            {
                 RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Game").DisplayIndex,
                 RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Date / Time").DisplayIndex,
                 RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Comment").DisplayIndex,
                 RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Aggregated").DisplayIndex,
                 RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "CPU").DisplayIndex,
                 RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "GPU").DisplayIndex,
                 RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "RAM").DisplayIndex
            };
        }

        private void SetHeaders()
        {
            var indices = _viewModel.AppConfiguration.RecordListHeaderOrder;

            RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Game").DisplayIndex = indices[0];
            RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Date / Time").DisplayIndex = indices[1];
            RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Comment").DisplayIndex = indices[2];
            RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "Aggregated").DisplayIndex = indices[3];
            RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "CPU").DisplayIndex = indices[4];
            RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "GPU").DisplayIndex = indices[5];
            RecordDataGrid.Columns.FirstOrDefault(c => c.Header.ToString() == "RAM").DisplayIndex = indices[6];
        }



        // TreeView Drag & Drop from record list

        private async void trvStructure_DragOver(object sender, DragEventArgs e)
        {
            var item = e.Source as TreeViewItem;

            if (item != null)
            {
                // select items to highlight them, expand when possible. Set bool to stop directory from changing
                IsDragDropActive = true;
                item.IsSelected = true;

                await Task.Delay(TimeSpan.FromMilliseconds(500));
                if (trvStructure.SelectedItem == item)
                    item.IsExpanded = true;
            }
            else
            {
                IsDragDropActive = false;
                ObservedTreeViewItem.IsSelected = true;
            }
        }

        private void trvStructure_MouseEnter(object sender, MouseEventArgs e)
        {
            // return to default state when canceling D&D
            IsDragDropActive = false;
            ObservedTreeViewItem.IsSelected = true;
        }


        private void trvStructure_Drop(object sender, DragEventArgs e)
        {
            var item = e.Source as TreeViewItem;
            var path = GetFullPath(item);
            _viewModel.OnMoveRecordFile(path);
            IsDragDropActive = false;

            // Keep the original observed directory after drop
            ObservedTreeViewItem.IsSelected = true;

            // Switch observed directory after drop
            //ObservedTreeViewItem = item;
            //_viewModel.RecordObserver.ObserveDirectory((item.Tag as DirectoryInfo).FullName);
        }



        public string GetFullPath(TreeViewItem node)
        {
            if (node == null)
                throw new ArgumentNullException();

            var result = Convert.ToString(node.Header);

            for (var i = GetParentItem(node); i != null; i = GetParentItem(i))
                result = i.Header + "\\" + result;

            DirectoryInfo parentDir = Directory.GetParent(CaptureRootDirectory);

            return parentDir + "\\" + result;
        }

        static TreeViewItem GetParentItem(TreeViewItem item)
        {
            for (var i = VisualTreeHelper.GetParent(item); i != null; i = VisualTreeHelper.GetParent(i))
                if (i is TreeViewItem)
                    return (TreeViewItem)i;

            return null;
        }


    }
}
