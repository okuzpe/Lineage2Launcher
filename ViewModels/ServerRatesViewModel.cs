using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Lineage2Launcher.ViewModels
{
    public class ServerRatesViewModel : INotifyPropertyChanged
    {
        private int _experienceRate = 30;
        private int _skillPointsRate = 25;
        private int _partyXpRate = 2;
        private int _partySpRate = 2;
        private int _adenaRate = 10;
        private int _dropItemsRate = 10;
        private int _spoilRate = 15;
        private int _sealStonesRate = 10;
        private int _manorRate = 10;

        public int ExperienceRate
        {
            get => _experienceRate;
            set { _experienceRate = value; OnPropertyChanged(); }
        }

        public int SkillPointsRate
        {
            get => _skillPointsRate;
            set { _skillPointsRate = value; OnPropertyChanged(); }
        }

        public int PartyXpRate
        {
            get => _partyXpRate;
            set { _partyXpRate = value; OnPropertyChanged(); }
        }

        public int PartySpRate
        {
            get => _partySpRate;
            set { _partySpRate = value; OnPropertyChanged(); }
        }

        public int AdenaRate
        {
            get => _adenaRate;
            set { _adenaRate = value; OnPropertyChanged(); }
        }

        public int DropItemsRate
        {
            get => _dropItemsRate;
            set { _dropItemsRate = value; OnPropertyChanged(); }
        }

        public int SpoilRate
        {
            get => _spoilRate;
            set { _spoilRate = value; OnPropertyChanged(); }
        }

        public int SealStonesRate
        {
            get => _sealStonesRate;
            set { _sealStonesRate = value; OnPropertyChanged(); }
        }

        public int ManorRate
        {
            get => _manorRate;
            set { _manorRate = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}


