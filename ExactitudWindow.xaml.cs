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
        s = s.Replace(".", ""); // quita separador de miles
        s = s.Replace(",", "."); // usa punto decimal

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
            double stdDev = Math.Sqrt(sumSq / (valores.Count - 1));

            // Calcular el RSD%
            double rsd = (promedio != 0) ? (stdDev / promedio) * 100.0 : 0.0;

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

        TextBox[] cajasc = { txtInyec1_2, txtInyec2_2 };

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
        if (double.TryParse(txtPesoSTDT.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture,
                out double S1) &&
            double.TryParse(txtPesoSTDC.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture,
                out double S2) &&
            double.TryParse(txtPromedioSTDT.Text.Replace("X=", "").Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture, out double A1) &&
            double.TryParse(txtPromedioSTDC.Text.Replace("X=", "").Replace(",", "."), NumberStyles.Any,
                CultureInfo.InvariantCulture, out double A2))
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

    double Get(TextBox tb)
    {
        var s = (tb.Text ?? "").Trim();
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.CurrentCulture, out var v)) return v;
        if (double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out v)) return v;
        return 0.0;
    }

    void CalcFila(TextBox tbPesoStd, TextBox tbPesoMuestra, TextBox tbSalida,
        double pmBase, double pmSal, double potenciaPct,
        double valoracion, double pesoPromedio,
        double matraz1, double al1, double matraz2, double al2, double matraz3)
    {
        double pesoStd = Get(tbPesoStd);
        double pesoMuestra = Get(tbPesoMuestra);

        // validaciones mínimas comunes
        if (pmSal == 0 || pesoPromedio == 0 || matraz1 == 0 || matraz2 == 0 || matraz3 == 0)
        {
            tbSalida.Text = "ERR";
            return;
        }

        double factorBaseSal = pmBase / pmSal;
        double factorPotencia = potenciaPct / 100.0;

        double parteStd = pesoStd * factorBaseSal * factorPotencia;
        double parteMuestra = pesoMuestra * (valoracion / pesoPromedio);

        double expr = parteStd + parteMuestra;

        double conc = expr * (1.0 / matraz1) * (al1 / matraz2) * (al2 / matraz3);

        tbSalida.Text = conc.ToString("0.###", CultureInfo.CurrentCulture);
    }
    // Calculo Concentracion Experimental ###################
    double Read(TextBox tb)
    {
        return TryParseNum(tb.Text, out var v) ? v : double.NaN;
    }

    void CalcConcExp_Area(TextBox tbArea, TextBox tbOut, double concStd, double promAreasStd)
    {
        double area = Read(tbArea);
        if (double.IsNaN(area) || double.IsNaN(concStd) || double.IsNaN(promAreasStd) || promAreasStd == 0)
        {
            tbOut.Text = ""; // o "ERR"
            return;
        }

        double concExp = (area * concStd) / promAreasStd;
        tbOut.Text = concExp.ToString("0.###", CultureInfo.CurrentCulture);
    }
    // Calculo Recuperacion ###################
    void CalcRecuperacion(TextBox tbExp, TextBox tbTeo, TextBox tbOut)
    {
        double exp = Read(tbExp);
        double teo = Read(tbTeo);

        if (double.IsNaN(exp) || double.IsNaN(teo) || teo == 0)
        {
            tbOut.Text = ""; // o "ERR"
            return;
        }

        double rec = (exp / teo) * 100.0;
        tbOut.Text = rec.ToString("0.##", CultureInfo.CurrentCulture);
    }
    // ### Boton #####
    private void btnCalcular_Click(object sender, RoutedEventArgs e)
    {
        // Disparar eventos como si hubiera cambio de texto
        var fake = new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None);
        CalcularDifPorcentual(this, fake);
        CalcularConcentracion(this, fake);

        // (opcional) si quieres recalcular cosas previas enlazadas a TextChanged
        // var fake = new TextChangedEventArgs(TextBox.TextChangedEvent, UndoAction.None);
        // CalcularDifPorcentual(this, fake);
        // CalcularConcentracion(this, fake);

        // --- Diluciones: parsea y valida ---
        if (!(TryParseNum(txtMatrazEX1.Text, out double m1)
              && TryParseNum(txtMatrazEX2.Text, out double m2)
              && TryParseNum(txtMatrazEX3.Text, out double m3)
              && TryParseNum(txtAlicuotaEX1.Text, out double a1)
              && TryParseNum(txtAlicuotaEX2.Text, out double a2)))
        {
            MessageBox.Show("Revisa Matraz/Alicuotas (usa solo números).");
            return;
        }

        if (m1 <= 0 || m2 <= 0 || m3 <= 0)
        {
            MessageBox.Show("Matraz1/2/3 no pueden ser 0.");
            return;
        }

        // --- Parámetros comunes ---
        double pmBase = Get(txtPMbase);
        double pmSal = Get(txtPMsal);
        double potencia = Get(txtPotencia); // %
        double valoracion = Get(txtValoracion);
        double pesoProm = Get(txtPesoPromedio);

        if (pmSal == 0 || pesoProm == 0)
        {
            MessageBox.Show("PM Sal y Peso Promedio no pueden ser 0.");
            return;
        }

        // --- Factor de dilución común ---
        double fDil = (1.0 / m1) * (a1 / m2) * (a2 / m3);

        // --- Helper por fila (usa tus nombres reales) ---
        void CalcFila(TextBox tbStd, TextBox tbMuestra, TextBox tbOut)
        {
            double pesoStd = Get(tbStd);
            double pesoMuestra = Get(tbMuestra);

            double parteStd = pesoStd * (pmBase / pmSal) * (potencia / 100.0);
            double parteMuestra = pesoMuestra * (valoracion / pesoProm);

            double conc = (parteStd + parteMuestra) * fDil;
            tbOut.Text = conc.ToString("0.###", CultureInfo.CurrentCulture);
        }

        // ---- Aplica a las 9 filas ----
        CalcFila(txtSTDbajo1, txtMTABajo1, txtTeo1);
        CalcFila(txtSTDbajo2, txtMTABajo2, txtTeo2);
        CalcFila(txtSTDbajo3, txtMTABajo3, txtTeo3);
        CalcFila(txtSTDMedio1, txtMTAMedio1, txtTeo4);
        CalcFila(txtSTDMedio2, txtMTAMedio2, txtTeo5);
        CalcFila(txtSTDMedio3, txtMTAMedio3, txtTeo6);
        CalcFila(txtSTDAlto1, txtMTAAlto1, txtTeo7);
        CalcFila(txtSTDAlto2, txtMTAAlto2, txtTeo8);
        CalcFila(txtSTDAlto3, txtMTAAlto3, txtTeo9);
        
        // Calculo Concentracion Experimental
        
        // Lee Concentración del STD y Promedio de Áreas del STD (las estrellitas del panel izquierdo)
        double concStd = Read(txtConcentracion); // "Concentración" en Estándares de Trabajo
        double promAreasStd = Read(txtPromedioSTDT); // "Promedio" de Áreas STD Trabajo

        // Áreas de cada réplica (columna “Áreas” a la derecha) y salidas “Conc. Exp.”
        CalcConcExp_Area(txtAreaBajo1, txtExp1, concStd, promAreasStd);
        CalcConcExp_Area(txtAreaBajo2, txtExp2, concStd, promAreasStd);
        CalcConcExp_Area(txtAreaBajo3, txtExp3, concStd, promAreasStd);
        CalcConcExp_Area(txtAreaMedio1, txtExp4, concStd, promAreasStd);
        CalcConcExp_Area(txtAreaMedio2, txtExp5, concStd, promAreasStd);
        CalcConcExp_Area(txtAreaMedio3, txtExp6, concStd, promAreasStd);
        CalcConcExp_Area(txtAreaAlto1, txtExp7, concStd, promAreasStd);
        CalcConcExp_Area(txtAreaAlto2, txtExp8, concStd, promAreasStd);
        CalcConcExp_Area(txtAreaAlto3, txtExp9, concStd, promAreasStd);
        
        // --- Recuperación por las 9 réplicas ---
        CalcRecuperacion(txtExp1, txtTeo1, txtREC1);
        CalcRecuperacion(txtExp2, txtTeo2, txtREC2);
        CalcRecuperacion(txtExp3, txtTeo3, txtREC3);

        CalcRecuperacion(txtExp4, txtTeo4, txtREC4);
        CalcRecuperacion(txtExp5, txtTeo5, txtREC5);
        CalcRecuperacion(txtExp6, txtTeo6, txtREC6);

        CalcRecuperacion(txtExp7, txtTeo7, txtREC7);
        CalcRecuperacion(txtExp8, txtTeo8, txtREC8);
        CalcRecuperacion(txtExp9, txtTeo9, txtREC9);
        //Calcular promedio y RSD
        double Read(TextBox tb) => TryParseNum(tb.Text, out var v) ? v : double.NaN;

        (double mean, double sd) Stats(IEnumerable<double> xs)
        {
            var data = xs.Where(d => !double.IsNaN(d)).ToArray();
            if (data.Length == 0) return (double.NaN, double.NaN);
            double m = data.Average();
            if (data.Length == 1) return (m, 0);
            double var = data.Sum(x => (x - m) * (x - m)) / (data.Length - 1);
            return (m, Math.Sqrt(var));
        }
        string F(double v) => double.IsNaN(v) ? "" : v.ToString("0.##", CultureInfo.CurrentCulture);

// --- Recuperaciones individuales (9) ---
        var recs = new[]
        {
            Read(txtREC1), Read(txtREC2), Read(txtREC3),
            Read(txtREC4), Read(txtREC5), Read(txtREC6),
            Read(txtREC7), Read(txtREC8), Read(txtREC9)
        };

// --- Promedio y SD global ---
        (double mean, double sd) = Stats(recs);
        double rsd = (double.IsNaN(mean) || mean == 0) ? double.NaN : sd / mean * 100.0;

// --- Mostrar en las casillas "Promedio" y "RSD (%)" ---
        txtPromedioFinal.Text = F(mean);
        txtRSDRec.Text      = F(rsd);
        
    }
}

