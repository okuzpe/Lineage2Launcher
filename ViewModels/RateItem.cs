using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace L2TitanLauncher.ViewModels
{
    public class RateItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private int _value;

        public RateItem(string name, int value)
        {
            _name = name;
            _value = value;
        }

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public int Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
