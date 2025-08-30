using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Validaciones;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
    
    // Boton de Exactitud
    public void btnExactitud_Click(object sender, RoutedEventArgs e)
    {
        var win = new ExactitudWindow();
        win.Owner = this;
        win.ShowDialog();
    }
}