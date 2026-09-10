using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using HidWizards.UCR.Core;
using HidWizards.UCR.Core.Models;

namespace HidWizards.UCR.ViewModels.Mapping
{
    public class MappingViewModel : INotifyPropertyChanged
    {
        private readonly Context _context;
        private Profile _currentProfile;
        
        public ObservableCollection<MappingRowViewModel> Rows { get; } = new ObservableCollection<MappingRowViewModel>();

        public MappingViewModel(Context context)
        {
            _context = context;
        }

        public void SetProfile(Profile profile)
        {
            _currentProfile = profile;
            PopulateRows();
        }

        private void PopulateRows()
        {
            Rows.Clear();
            if (_currentProfile == null) return;
            
            // Hardcode Xbox Controller Output rows for the Patch Bay.
            AddRow("Left Stick X", "LSX", true);
            AddRow("Left Stick Y", "LSY", true);
            AddRow("Right Stick X", "RSX", true);
            AddRow("Right Stick Y", "RSY", true);
            AddRow("Left Trigger", "LT", true);
            AddRow("Right Trigger", "RT", true);
            
            AddRow("Button A", "BtnA", false);
            AddRow("Button B", "BtnB", false);
            AddRow("Button X", "BtnX", false);
            AddRow("Button Y", "BtnY", false);
            
            AddRow("Left Bumper", "LB", false);
            AddRow("Right Bumper", "RB", false);
            AddRow("D-Pad Up", "DPadUp", false);
            AddRow("D-Pad Down", "DPadDown", false);
            AddRow("D-Pad Left", "DPadLeft", false);
            AddRow("D-Pad Right", "DPadRight", false);
            
            AddRow("Start", "Start", false);
            AddRow("Back", "Back", false);
            AddRow("Guide", "Guide", false);
        }

        private void AddRow(string title, string targetOutputKey, bool isAxis)
        {
            var mapping = _currentProfile.Mappings.FirstOrDefault(m => m.TargetOutputKey == targetOutputKey);
            if (mapping == null)
            {
                mapping = new Core.Models.Mapping(_currentProfile, title)
                {
                    TargetOutputKey = targetOutputKey
                };
                _currentProfile.Mappings.Add(mapping);
            }
            
            var rowVm = new MappingRowViewModel(_context, mapping, title, targetOutputKey, isAxis);
            Rows.Add(rowVm);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
