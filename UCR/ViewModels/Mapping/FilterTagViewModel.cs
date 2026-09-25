using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HidWizards.UCR.Core.Models;
using HidWizards.UCR.Utilities.Commands;

namespace HidWizards.UCR.ViewModels.Mapping
{
    /// <summary>
    /// One "only active when filter X is [Active/Inactive]" condition attached to a plugin. A plugin
    /// can carry several of these (all must pass for the plugin to run -- see Plugin.IsFiltered).
    /// </summary>
    public class FilterTagViewModel : INotifyPropertyChanged
    {
        private readonly Plugin _plugin;
        public Filter Filter { get; }

        public string Name => Filter.Name;

        public bool Negative
        {
            get => Filter.Negative;
            set
            {
                if (Filter.Negative == value) return;
                _plugin.AddFilter(Filter.Name, value);
                OnPropertyChanged();
            }
        }

        public ICommand RemoveCommand { get; }

        public FilterTagViewModel(Plugin plugin, Filter filter, Action<FilterTagViewModel> onRemove)
        {
            _plugin = plugin;
            Filter = filter;
            RemoveCommand = new RelayCommand(_ =>
            {
                _plugin.RemoveFilter(Filter);
                onRemove?.Invoke(this);
            });
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
