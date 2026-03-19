using System.Windows;
using FileLister.ViewModels;

namespace FileLister
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(() => this.Close());
        }
    }
}
