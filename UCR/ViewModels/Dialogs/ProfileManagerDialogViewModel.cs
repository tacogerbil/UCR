using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using HidWizards.UCR.Utilities.Commands;
using HidWizards.UCR.ViewModels.Dashboard;

namespace HidWizards.UCR.ViewModels.Dialogs
{
    public enum ProfileManagerAction
    {
        Edit,
        Delete,
        Clone,
        Close
    }

    // What a row's action button asks MainWindowViewModel.ManageProfiles to do next -- this dialog
    // never performs Edit/Delete/Clone itself (that logic already lives in MainWindowViewModel,
    // shared with the pencil-icon ProfileEditDialog flow); it only reports which profile and which
    // action, then closes so the caller can act and reopen it.
    public class ProfileManagerResult
    {
        public ProfileManagerAction Action { get; }
        public Guid ProfileGuid { get; }

        private ProfileManagerResult(ProfileManagerAction action, Guid profileGuid)
        {
            Action = action;
            ProfileGuid = profileGuid;
        }

        public static ProfileManagerResult Edit(Guid profileGuid) => new ProfileManagerResult(ProfileManagerAction.Edit, profileGuid);
        public static ProfileManagerResult Delete(Guid profileGuid) => new ProfileManagerResult(ProfileManagerAction.Delete, profileGuid);
        public static ProfileManagerResult Clone(Guid profileGuid) => new ProfileManagerResult(ProfileManagerAction.Clone, profileGuid);
        public static ProfileManagerResult Close() => new ProfileManagerResult(ProfileManagerAction.Close, Guid.Empty);
    }

    public class ProfileManagerRowViewModel
    {
        public Guid ProfileGuid { get; }
        public string ChipTitle { get; }
        public int Depth { get; }
        public double IndentWidth => Depth * 22;

        public ICommand EditCommand { get; }
        public ICommand CloneCommand { get; }
        public ICommand DeleteCommand { get; }

        public ProfileManagerRowViewModel(ProfileItem item, int depth, Action<ProfileManagerResult> requestClose)
        {
            ProfileGuid = item.Id;
            ChipTitle = item.ChipTitle;
            Depth = depth;

            EditCommand = new RelayCommand(_ => requestClose(ProfileManagerResult.Edit(ProfileGuid)));
            CloneCommand = new RelayCommand(_ => requestClose(ProfileManagerResult.Clone(ProfileGuid)));
            DeleteCommand = new RelayCommand(_ => requestClose(ProfileManagerResult.Delete(ProfileGuid)));
        }
    }

    // Lists every profile (top-level and nested children, flattened with indentation) so the user can
    // edit/clone/delete any of them from one place, rather than hunting through the nested profile
    // tree's own per-item pencil icon. Deliberately thin: it reuses Dashboard.ProfileList (the same
    // tree the pencil-edit flow already builds via ProfileItem.GetProfileTree) for its data, and
    // delegates every actual Edit/Delete/Clone operation back to MainWindowViewModel -- which already
    // owns that logic (EditProfileAsync/ExecuteRemove/ExecuteClone) for the existing per-profile
    // editor, so none of it is duplicated here.
    public class ProfileManagerDialogViewModel
    {
        public string Title => "Manage Profiles";
        public ObservableCollection<ProfileManagerRowViewModel> Rows { get; } = new ObservableCollection<ProfileManagerRowViewModel>();
        public ICommand CloseCommand { get; }

        public Action<ProfileManagerResult> CloseDialogAction { get; set; }

        public ProfileManagerDialogViewModel(IEnumerable<ProfileItem> profileTree)
        {
            CloseCommand = new RelayCommand(_ => CloseDialogAction?.Invoke(ProfileManagerResult.Close()));
            PopulateRows(profileTree, 0);
        }

        private void PopulateRows(IEnumerable<ProfileItem> items, int depth)
        {
            if (items == null) return;
            foreach (var item in items)
            {
                Rows.Add(new ProfileManagerRowViewModel(item, depth, result => CloseDialogAction?.Invoke(result)));
                PopulateRows(item.Items, depth + 1);
            }
        }
    }
}
