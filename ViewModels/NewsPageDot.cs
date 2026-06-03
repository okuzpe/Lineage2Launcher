using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace L2TitanLauncher.ViewModels
{
    public class NewsPageDot : INotifyPropertyChanged
    {
        private bool _isActive;

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DotBrush));
                }
            }
        }

        public Brush DotBrush => IsActive
            ? new SolidColorBrush(Color.FromRgb(0xC9, 0xA8, 0x4C))
            : new SolidColorBrush(Color.FromArgb(0x50, 0xFF, 0xFF, 0xFF));

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
