using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Lineage2Launcher.ViewModels
{
    public enum ServerStatus
    {
        Online,
        Maintenance,
        Offline
    }

    public class ServerViewModel : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private ServerStatus _status = ServerStatus.Offline;
        private bool _isFavorite = false;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public ServerStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusBrush));
                }
            }
        }

        public Brush StatusBrush
        {
            get
            {
                return Status switch
                {
                    ServerStatus.Online => new SolidColorBrush(Colors.Green),
                    ServerStatus.Maintenance => new SolidColorBrush(Colors.Yellow),
                    ServerStatus.Offline => new SolidColorBrush(Colors.Red),
                    _ => new SolidColorBrush(Colors.Gray)
                };
            }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    OnPropertyChanged();
                }
            }
        }

        public ICommand ToggleFavoriteCommand { get; }

        public ServerViewModel()
        {
            ToggleFavoriteCommand = new ViewModels.RelayCommand(() => IsFavorite = !IsFavorite);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

