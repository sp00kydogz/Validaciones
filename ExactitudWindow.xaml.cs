using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Globalization;
using System.Windows.Media;


namespace Validaciones;

public partial class ExactitudWindow : Window
{
    public ExactitudWindow()
    {
        InitializeComponent();
    }
    // ################################## AYUDAS ####################################################################
    // #### Permitir solo digitos de 0  - 9 ( Enteros) ####
    private void SoloEnteros(object sender, TextCompositionEventArgs e)
    {
        Regex regex = new Regex("[^0-9]+");
        e.Handled = regex.IsMatch(e.Text);
    }
    // #### Bloquear Pegar Texto con letras 0 mas de 11 ####
    private void ControlarPegado(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (Clipboard.ContainsText())
            {
                string text = Clipboard.GetText();
                if (!Regex.IsMatch(text, @"^\d{1,11}$"))
                {
                    e.Handled = true;
                }
            }
        }
    }
    // #### Validadores de Base ####
    private static bool SoloDigitosYComa(string s)
        => Regex.IsMatch(s, @"^[0-9,]+$");

    private static bool SoloDigitos(string s)
        => Regex.IsMatch(s, @"^[0-9]+$");

    // bloquea Ctrl+V con contenido inválido según un patrón
    private void BloquearPegado(KeyEventArgs e, string patronRegex)
    {
        if (e.Key == Key.V && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            if (Clipboard.ContainsText())
            {
                var t = Clipboard.GetText();
                if (!Regex.IsMatch(t, patronRegex)) e.Handled = true;
            }
        }
    }
    // #### 3 Decimales con coma ####
    private void Dec3_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var tb = (TextBox)sender;

        if (e.Text == ".")
        {
            // inserta coma en la posición del cursor
            int i = tb.SelectionStart;
            tb.Text = tb.Text.Remove(i, tb.SelectionLength).Insert(i, ",");
            tb.SelectionStart = i + 1;
            e.Handled = true; // bloquea el "."
            return;
        }
        // sigue permitiendo solo dígitos y coma
        e.Handled = !SoloDigitosYComa(e.Text);
    }
    
    private void Dec3_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // permite coma o punto: ^\d+([,\.]\d{0,3})?$
        BloquearPegado(e, @"^\d+([,\.]\d{0,3})?$");
    }
    private void Dec3_LostFocus(object sender, RoutedEventArgs e)
    {
        var cultura = new CultureInfo("es-CL");
        var tb = (TextBox)sender;
        if (TryParseDecimal(tb.Text, out double v))
            tb.Text = v.ToString("N3", cultura); // siempre 3 decimales con coma
        else
            tb.Text = ""; // vacío si no es válido
    }
    // #### 2 Decimales con Coma ####
    private void Dec2_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        var tb = (TextBox)sender;

        if (e.Text == ".")
        {
            int i = tb.SelectionStart;
            tb.Text = tb.Text.Remove(i, tb.SelectionLength).Insert(i, ",");
            tb.SelectionStart = i + 1;
            e.Handled = true;
            return;
        }

        e.Handled = !SoloDigitosYComa(e.Text);
    }
    
    private void Dec2_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        BloquearPegado(e, @"^\d+([,\.]\d{0,2})?$");
    }
    private void Dec2_LostFocus(object sender, RoutedEventArgs e)
    {
        var cultura = new CultureInfo("es-CL");
        var tb = (TextBox)sender;
        if (TryParseDecimal(tb.Text, out double v))
            tb.Text = v.ToString("N2", cultura);
        else
            tb.Text = "";
    }
    // #### Entero + Sufijo mL ####
    private void Int_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !SoloDigitos(e.Text);
    }
    private void Int_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        BloquearPegado(e, @"^\d+$");
    }
    private void Int_LostFocus_AddMl(object sender, RoutedEventArgs e)
    {
        var tb = (TextBox)sender;
        // quitar " mL" si lo tenía y validar entero
        var limpio = tb.Text.Replace(" mL", "").Trim();
        if (int.TryParse(limpio, out int n))
            tb.Text = $"{n} mL";
        else
            tb.Text = "";
    }
    // #### Utils ####
    private bool TryParseDecimal(string s, out double v)
    {
        var cultura = new CultureInfo("es-CL");
        v = 0;
        if (string.IsNullOrWhiteSpace(s)) return false;
        // aceptar punto o coma como entrada
        s = s.Trim().Replace(".", ",");
        return double.TryParse(s, NumberStyles.Any, cultura, out v);
    }
    // #### Quitar sufijos y Prefijos para Calculo ####
    private static bool TryParseNum(string raw, out double val)
    {
        val = 0;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        // quita unidades y prefijos conocidos
        string s = raw.Trim()
            .Replace("X=", "", StringComparison.OrdinalIgnoreCase)
            .Replace("x=", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" mL", "", StringComparison.OrdinalIgnoreCase)
            .Replace("mg/mL", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" mg", "", StringComparison.OrdinalIgnoreCase)
            .Replace("%", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        // normaliza formato chileno: quita miles y cambia coma a punto
        s = s.Replace(".", "");   // quita separador de miles
        s = s.Replace(",", ".");  // usa punto decimal

        return double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out val);
    }
    
    // ########################################### CALCULOS #######################################################
    // #### Calcular promedios de Forma Dinamica STDT ####
    private void CalcularPromediosSTDT(object sender, TextChangedEventArgs e)
    {
        TextBox[] cajas = { txtInyec1, txtInyec2, txtInyec3, txtInyec4, txtInyec5, txtInyec6 };

        var valores = cajas
            .Where(tb => !string.IsNullOrEmpty(tb.Text))
            .Select(tb =>
            {
                if (long.TryParse(tb.Text, out long n))
                    return (long?)n;
                return null;
            })
            .Where(n => n.HasValue)
            .Select(n => n.Value)
            .ToList();

        if (valores.Count > 0)
        {
            var cultura = new CultureInfo("es-CL");
            // promedio double
            double promedio = valores.Average();

            // redondear al entero más cercano (desde 5 para arriba)
            int promedioEntero = (int)Math.Round(promedio, MidpointRounding.AwayFromZero);

            // mostrar con prefijo
            txtPromedioSTDT.Text = $"X={promedioEntero.ToString(cultura)}";
            
            // Calcular desviacion estandar
            double sumSq = valores.Sum(v => Math.Pow(v - promedio, 2));
            double stdDev = Math.Sqrt(sumSq / (valores.Count -1));
            
            // Calcular el RSD%
            double rsd = (promedio !=0) ? (stdDev / promedio) * 100.0 : 0.0;
            
            // Mostar con 4 decimales
            txtRsdSTDT.Text = $"RSD={rsd.ToString("N2", cultura)}%";
            // Cambiar color según criterio (<=2% verde, >2% rojo)
            if (rsd > 2.0)
            {
                txtRsdSTDT.Foreground = Brushes.Red;
            }
            else
            {
                txtRsdSTDT.Foreground = Brushes.Green;
            }
        }
        else
        {
            txtPromedioSTDT.Text = "";
            txtRsdSTDT.Text = "";
        }
    }
    // #### Calcular promedios de Forma Dinamica STDC ####
    private void CalcularPromediosSTDC(object sender, TextChangedEventArgs e)
    {
        
        TextBox[] cajasc = { txtInyec1_2, txtInyec2_2};

        var valoresc = cajasc
            .Where(tb => !string.IsNullOrEmpty(tb.Text))
            .Select(tb =>
            {
                if (long.TryParse(tb.Text, out long n))
                    return (long?)n;
                return null;
            })
            .Where(n => n.HasValue)
            .Select(n => n.Value)
            .ToList();

        if (valoresc.Count > 0)
        {
            var cultura = new CultureInfo("es-CL");
            // promedio double
            double promedioc = valoresc.Average();

            // redondear al entero más cercano (desde 5 para arriba)
            int promedioEnteroc = (int)Math.Round(promedioc, MidpointRounding.AwayFromZero);

            // mostrar con prefijo
            txtPromedioSTDC.Text = $"X={promedioEnteroc.ToString(cultura)}";
        }
        else
        {
            txtPromedioSTDC.Text = "";
        }
    }
    // #### Calculo de Diferencia Porcentual ####
    private void CalcularDifPorcentual(object sender, TextChangedEventArgs e)
    {
        if (double.TryParse(txtPesoSTDT.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double S1) &&
            double.TryParse(txtPesoSTDC.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double S2) &&
            double.TryParse(txtPromedioSTDT.Text.Replace("X=", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double A1) &&
            double.TryParse(txtPromedioSTDC.Text.Replace("X=", "").Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out double A2))
        {
            if (S2 != 0 && A1 != 0)
            {
                double dif = 100 - ((S1 / S2) * (A2 / A1) * 100);

                var cultura = new CultureInfo("es-CL");
                txtDifPorcentual.Text = dif.ToString("N2", cultura) + " %";
                // Cambiar color según criterio (<=2% verde, >2% rojo)
                if (dif > 2.0)
                {
                    txtDifPorcentual.Foreground = Brushes.Red;
                }
                else
                {
                    txtDifPorcentual.Foreground = Brushes.Green;
                }
            }
            else
            {
                txtDifPorcentual.Text = "";
            }
        }
        else
        {
            txtDifPorcentual.Text = "";
        }
    }
    // #### Calcular Concentracion ####
    public void CalcularConcentracion(object sender, RoutedEventArgs e)
    {
        // Lee y parsea todo limpiando sufijos/unidades
        if (TryParseNum(txtPesoSTDT.Text, out double S1) &&
            TryParseNum(txtPesoSTDC.Text, out double S2) &&
            TryParseNum(txtPotencia.Text, out double P) &&      
            TryParseNum(txtPMbase.Text, out double PMB) &&
            TryParseNum(txtPMsal.Text, out double PMS) &&
            TryParseNum(txtMatraz1.Text, out double M1) &&
            TryParseNum(txtAlicuota1.Text, out double A1) &&
            TryParseNum(txtMatraz2.Text, out double M2) &&
            TryParseNum(txtAlicuota2.Text, out double A2) &&
            TryParseNum(txtMatraz3.Text, out double M3))
        {
            // valida denominadores
            if (PMS == 0 || M1 == 0 || M2 == 0 || M3 == 0)
            {
                txtConcentracion.Text = "";
                return;
            }

            // fórmula:
            // Conc = (S1/M1) * (A1/M2) * (A2/M3) * (P/100) * (PMB/PMS)
            double conc = (S1 / M1) * (A1 / M2) * (A2 / M3) * (P / 100.0) * (PMB / PMS);

            double factor = (PMB / PMS);

            var cultura = new CultureInfo("es-CL");
            txtConcentracion.Text = $"{conc.ToString("N6", cultura)} mg/mL";
            txtFactor.Text = $"{factor.ToString("N4", cultura)}";
        }
        else
        {
            txtConcentracion.Text = "";
            txtFactor.Text = "";
        }
    }
    // ### Boton #####
    private void btnCalcular_Click(object sender, RoutedEventArgs e)
    {
        // Disparar eventos como si hubiera cambio de texto
        var fake = new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None);
        CalcularDifPorcentual(this, fake);
        CalcularConcentracion(this, fake);
    }
    // ############## FIN DE BLOQUE ESTANDARES ####################################################################
    // ############### MUESTRAS Y ESTANDARES ADICIONADOS ##########################################################
    // ---
    
    
}