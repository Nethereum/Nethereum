using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nethereum.Generators.Desktop
{
    public class ViewModelBase: INotifyPropertyChanged
    { 
        public void OnPropertyChanged([CallerMemberName] string memberName = null)
	    {
		    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(memberName));
        }
        public event PropertyChangedEventHandler PropertyChanged;
    }
}
