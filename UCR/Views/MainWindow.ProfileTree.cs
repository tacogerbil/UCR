using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HidWizards.UCR.ViewModels.Dashboard;

namespace HidWizards.UCR.Views
{
    public partial class MainWindow
    {
        private Point _profileDragStartPoint;
        private ProfileItem _draggedProfileItem;

        // TODO Deprecated, replace with property notifications
        private void ReloadProfileTree()
        {
            var profileTree = ProfileItem.GetProfileTree(Context.Profiles);
            _mainWindowViewModel.Dashboard.ReplaceProfileList(profileTree);
        }

        private void ProfileTree_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
        }

        private void ProfileTree_OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
        }

        private void ProfileTree_OnDragOver(object sender, DragEventArgs e)
        {
            if (!string.Equals(_mainWindowViewModel.Dashboard.ProfileGroupingMode, "Tree", StringComparison.Ordinal))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var sourceItem = e.Data.GetData(typeof(ProfileItem)) as ProfileItem;
            var targetContainer = GetTreeViewItem(e.OriginalSource as DependencyObject);
            var targetItem = targetContainer?.DataContext as ProfileItem;

            e.Effects = CanReorderProfile(sourceItem, targetItem) ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void ProfileTree_OnDrop(object sender, DragEventArgs e)
        {
            if (!string.Equals(_mainWindowViewModel.Dashboard.ProfileGroupingMode, "Tree", StringComparison.Ordinal))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            var sourceItem = e.Data.GetData(typeof(ProfileItem)) as ProfileItem;
            var targetContainer = GetTreeViewItem(e.OriginalSource as DependencyObject);
            var targetItem = targetContainer?.DataContext as ProfileItem;
            if (!CanReorderProfile(sourceItem, targetItem) || targetContainer == null) return;

            var siblings = sourceItem.Profile.ParentProfile == null
                ? Context.Profiles
                : sourceItem.Profile.ParentProfile.ChildProfiles;

            var sourceIndex = siblings.IndexOf(sourceItem.Profile);
            var targetIndex = siblings.IndexOf(targetItem.Profile);
            if (sourceIndex < 0 || targetIndex < 0) return;

            var targetHeader = GetTreeViewItemHeaderElement(targetContainer);
            var dropPosition = e.GetPosition(targetHeader);
            var insertAfterTarget = dropPosition.Y > targetHeader.ActualHeight / 2;
            var insertIndex = targetIndex + (insertAfterTarget ? 1 : 0);

            siblings.RemoveAt(sourceIndex);
            if (sourceIndex < insertIndex) insertIndex--;
            if (insertIndex < 0) insertIndex = 0;
            if (insertIndex > siblings.Count) insertIndex = siblings.Count;
            siblings.Insert(insertIndex, sourceItem.Profile);

            Context.ContextChanged();
            ReloadProfileTree();
            e.Handled = true;
        }

        private static bool CanReorderProfile(ProfileItem sourceItem, ProfileItem targetItem)
        {
            if (sourceItem?.Profile == null || targetItem?.Profile == null) return false;
            if (ReferenceEquals(sourceItem.Profile, targetItem.Profile)) return false;
            return ReferenceEquals(sourceItem.Profile.ParentProfile, targetItem.Profile.ParentProfile);
        }

        private static FrameworkElement GetTreeViewItemHeaderElement(TreeViewItem container)
        {
            container.ApplyTemplate();

            var header = container.Template?.FindName("ContentGrid", container) as FrameworkElement
                         ?? container.Template?.FindName("PART_Header", container) as FrameworkElement;

            return header != null && header.ActualHeight > 0 ? header : container;
        }

        private TreeViewItem GetTreeViewItem(DependencyObject source)
        {
            while (source != null)
            {
                if (source is TreeViewItem treeViewItem) return treeViewItem;

                if (source is Visual || source is System.Windows.Media.Media3D.Visual3D)
                {
                    source = VisualTreeHelper.GetParent(source);
                }
                else if (source is FrameworkContentElement contentElement)
                {
                    source = contentElement.Parent;
                }
                else
                {
                    source = LogicalTreeHelper.GetParent(source);
                }
            }

            return null;
        }

        private void ProfileTree_OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            var treeView = sender as TreeView;
            _mainWindowViewModel.Dashboard.SelectedProfileItem = treeView?.SelectedItem as ProfileItem;
        }
    }
}
