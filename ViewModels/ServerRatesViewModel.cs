using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace L2TitanLauncher.ViewModels
{
    public class ServerRatesViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<RateItem> Rates { get; } = new();

        public ServerRatesViewModel()
        {
            Rates.Add(new RateItem("Experiencia (XP)", 30));
            Rates.Add(new RateItem("Skill Points (SP)", 25));
            Rates.Add(new RateItem("Party XP", 2));
            Rates.Add(new RateItem("Party SP", 2));
            Rates.Add(new RateItem("Adena", 10));
            Rates.Add(new RateItem("Drop Items", 10));
            Rates.Add(new RateItem("Spoil", 15));
            Rates.Add(new RateItem("Seal Stones", 10));
            Rates.Add(new RateItem("Manor", 10));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
