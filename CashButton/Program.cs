using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CashButton;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new CashButtonForm());
    }
}

internal sealed class CashButtonForm : Form
{
    private readonly Button openButton;
    private readonly NotifyIcon trayIcon;
    private readonly ContextMenuStrip trayMenu;
    private AppSettings settings;
    private bool allowExit;
    private Point dragStart;
    private bool dragStarted;

    public CashButtonForm()
    {
        settings = SettingsStore.Load();

        Text = "Cash Button";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        Location = new Point(40, 180);
        Size = new Size(174, 62);
        BackColor = Color.FromArgb(24, 105, 70);

        openButton = new Button
        {
            Dock = DockStyle.Fill,
            Text = "🔓  ABRIR CAJA",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.FromArgb(24, 105, 70),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            TabStop = false
        };
        openButton.FlatAppearance.BorderSize = 0;
        openButton.FlatAppearance.MouseOverBackColor = Color.FromArgb(19, 125, 78);
        openButton.FlatAppearance.MouseDownBackColor = Color.FromArgb(15, 85, 57);
        openButton.Click += async (_, _) => await OpenDrawerAsync();
        openButton.MouseDown += BeginDrag;
        openButton.MouseMove += ContinueDrag;
        Controls.Add(openButton);

        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Mostrar Cash Button", null, (_, _) => RestoreWindow());
        trayMenu.Items.Add("Configuración de impresora y pin", null, (_, _) => OpenSettings());
        trayMenu.Items.Add("Iniciar con Windows", null, ToggleStartup);
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Salir", null, (_, _) => ExitApplication());

        trayIcon = new NotifyIcon
        {
            Text = "Cash Button — apertura de cajón",
            Icon = SystemIcons.Application,
            ContextMenuStrip = trayMenu,
            Visible = true
        };
        trayIcon.DoubleClick += (_, _) => RestoreWindow();

        FormClosing += (_, e) =>
        {
            if (!allowExit)
            {
                e.Cancel = true;
                Hide();
            }
        };

        Shown += (_, _) => UpdateStartupMenuText();
    }

    private async Task OpenDrawerAsync()
    {
        if (string.IsNullOrWhiteSpace(settings.PrinterName))
        {
            MessageBox.Show("Primero abre Configuración y selecciona una impresora.",
                "Cash Button", MessageBoxButtons.OK, MessageBoxIcon.Information);
            OpenSettings();
            return;
        }

        openButton.Enabled = false;
        openButton.Text = "ENVIANDO...";
        try
        {
            // ESC/POS: ESC p m t1 t2. El valor m se configura manualmente.
            byte pinValue = checked((byte)settings.PinValue);
            byte[] command = { 0x1B, 0x70, pinValue, 0x19, 0xFA };
            await Task.Run(() => RawPrinter.Send(settings.PrinterName, command));

            trayIcon.BalloonTipTitle = "Cash Button";
            trayIcon.BalloonTipText = $"Comando enviado a {settings.PrinterName}.";
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayIcon.ShowBalloonTip(1800);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "No se pudo enviar el comando a la impresora.\n\n" + ex.Message +
                "\n\nVerifica que la impresora esté encendida y disponible.",
                "Cash Button", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            await Task.Delay(350);
            openButton.Text = "🔓  ABRIR CAJA";
            openButton.Enabled = true;
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(settings);
        if (dialog.ShowDialog(this) == DialogResult.OK)
            settings = dialog.SavedSettings;
    }

    private void BeginDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left)
            return;

        dragStart = e.Location;
        dragStarted = false;
    }

    private void ContinueDrag(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Left || dragStarted)
            return;

        int dx = Math.Abs(e.X - dragStart.X);
        int dy = Math.Abs(e.Y - dragStart.Y);
        if (dx < SystemInformation.DragSize.Width / 2 &&
            dy < SystemInformation.DragSize.Height / 2)
            return;

        dragStarted = true;
        ReleaseCapture();
        SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
    }

    private void RestoreWindow()
    {
        Show();
        WindowState = FormWindowState.Normal;
        TopMost = true;
        Activate();
    }

    private void ToggleStartup(object? sender, EventArgs e)
    {
        try
        {
            using RegistryKey key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run", writable: true)!;

            if (key.GetValue("CashButton") is not null)
                key.DeleteValue("CashButton", throwOnMissingValue: false);
            else
                key.SetValue("CashButton", $"\"{Application.ExecutablePath}\"");

            UpdateStartupMenuText();
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo modificar el inicio automático.\n\n" + ex.Message,
                "Cash Button", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void UpdateStartupMenuText()
    {
        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Run");
            bool enabled = key?.GetValue("CashButton") is not null;
            trayMenu.Items[2].Text = enabled
                ? "Quitar inicio con Windows"
                : "Iniciar con Windows";
        }
        catch { }
    }

    private void ExitApplication()
    {
        allowExit = true;
        trayIcon.Visible = false;
        trayIcon.Dispose();
        trayMenu.Dispose();
        Application.Exit();
    }

    [DllImport("user32.dll")]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll")]
    private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
}

internal sealed class SettingsForm : Form
{
    private readonly ComboBox printerCombo;
    private readonly Label portLabel;
    private readonly NumericUpDown pinInput;
    private readonly List<PrinterOption> printers;

    public AppSettings SavedSettings { get; private set; }

    public SettingsForm(AppSettings current)
    {
        SavedSettings = new AppSettings
        {
            PrinterName = current.PrinterName,
            PinValue = current.PinValue
        };

        Text = "Configuración — Cash Button";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(430, 235);
        Font = new Font("Segoe UI", 9.5f);

        var heading = new Label
        {
            Text = "Configuración del cajón de dinero",
            Font = new Font("Segoe UI", 13, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(20, 18)
        };
        Controls.Add(heading);

        Controls.Add(new Label { Text = "Impresora instalada:", AutoSize = true, Location = new Point(22, 66) });
        printerCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(22, 88),
            Size = new Size(385, 28)
        };
        printers = PrinterDiscovery.GetPrinters();
        printerCombo.Items.AddRange(printers.Cast<object>().ToArray());
        Controls.Add(printerCombo);

        Controls.Add(new Label { Text = "Puerto detectado:", AutoSize = true, Location = new Point(22, 126) });
        portLabel = new Label
        {
            Text = "No disponible",
            AutoSize = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(135, 126)
        };
        Controls.Add(portLabel);

        Controls.Add(new Label { Text = "Valor del pin (m):", AutoSize = true, Location = new Point(22, 161) });
        pinInput = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 255,
            Value = Math.Clamp(current.PinValue, 0, 255),
            Location = new Point(135, 157),
            Size = new Size(75, 26)
        };
        Controls.Add(pinInput);

        var note = new Label
        {
            Text = "ESC/POS habitual: 0 = pin 2; 1 = pin 5. Otros valores dependen del modelo.",
            AutoSize = false,
            Location = new Point(22, 190),
            Size = new Size(390, 20),
            ForeColor = Color.DimGray,
            Font = new Font("Segoe UI", 8)
        };
        Controls.Add(note);

        var saveButton = new Button
        {
            Text = "Guardar",
            DialogResult = DialogResult.None,
            Location = new Point(237, 211),
            Size = new Size(80, 28)
        };
        saveButton.Click += SaveSettings;
        Controls.Add(saveButton);

        var cancelButton = new Button
        {
            Text = "Cancelar",
            DialogResult = DialogResult.Cancel,
            Location = new Point(327, 211),
            Size = new Size(80, 28)
        };
        Controls.Add(cancelButton);
        AcceptButton = saveButton;
        CancelButton = cancelButton;

        if (printers.Count == 0)
        {
            printerCombo.Items.Add("No se detectaron impresoras instaladas");
            printerCombo.SelectedIndex = 0;
            printerCombo.Enabled = false;
        }
        else
        {
            int selected = printers.FindIndex(p => string.Equals(p.Name, current.PrinterName, StringComparison.OrdinalIgnoreCase));
            if (selected < 0)
                selected = printers.FindIndex(p => p.Name.Contains("POS-80", StringComparison.OrdinalIgnoreCase));
            printerCombo.SelectedIndex = selected >= 0 ? selected : 0;
            printerCombo.SelectedIndexChanged += (_, _) => UpdatePortLabel();
            UpdatePortLabel();
        }
    }

    private void UpdatePortLabel()
    {
        if (printerCombo.SelectedItem is PrinterOption selected)
            portLabel.Text = string.IsNullOrWhiteSpace(selected.Port) ? "No identificado" : selected.Port;
    }

    private void SaveSettings(object? sender, EventArgs e)
    {
        if (printerCombo.SelectedItem is not PrinterOption selected)
        {
            MessageBox.Show("Selecciona una impresora instalada antes de guardar.",
                "Cash Button", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SavedSettings.PrinterName = selected.Name;
        SavedSettings.PinValue = decimal.ToInt32(pinInput.Value);

        try
        {
            SettingsStore.Save(SavedSettings);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("No se pudo guardar la configuración.\n\n" + ex.Message,
                "Cash Button", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

internal sealed class AppSettings
{
    public string PrinterName { get; set; } = "";
    public int PinValue { get; set; } = 0;
}

internal static class SettingsStore
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CashButton");
    private static readonly string FilePath = Path.Combine(Folder, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
        }
        catch { }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
    }
}

internal sealed class PrinterOption
{
    public string Name { get; }
    public string Port { get; }

    public PrinterOption(string name, string port)
    {
        Name = name;
        Port = port;
    }

    public override string ToString() => Name;
}

internal static class PrinterDiscovery
{
    private const uint PRINTER_ENUM_LOCAL = 0x2;
    private const uint PRINTER_ENUM_CONNECTIONS = 0x4;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct PRINTER_INFO_2
    {
        public IntPtr pServerName;
        public IntPtr pPrinterName;
        public IntPtr pShareName;
        public IntPtr pPortName;
        public IntPtr pDriverName;
        public IntPtr pComment;
        public IntPtr pLocation;
        public IntPtr pDevMode;
        public IntPtr pSepFile;
        public IntPtr pPrintProcessor;
        public IntPtr pDatatype;
        public IntPtr pParameters;
        public IntPtr pSecurityDescriptor;
        public uint Attributes;
        public uint Priority;
        public uint DefaultPriority;
        public uint StartTime;
        public uint UntilTime;
        public uint Status;
        public uint cJobs;
        public uint AveragePPM;
    }

    [DllImport("winspool.drv", EntryPoint = "EnumPrintersW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool EnumPrinters(uint flags, string? name, uint level, IntPtr buffer,
        uint bufferSize, out uint needed, out uint returned);

    public static List<PrinterOption> GetPrinters()
    {
        var result = new List<PrinterOption>();
        EnumPrinters(PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS, null, 2, IntPtr.Zero, 0,
            out uint needed, out _);

        if (needed > 0)
        {
            IntPtr buffer = Marshal.AllocHGlobal((int)needed);
            try
            {
                if (EnumPrinters(PRINTER_ENUM_LOCAL | PRINTER_ENUM_CONNECTIONS, null, 2,
                    buffer, needed, out _, out uint returned))
                {
                    int size = Marshal.SizeOf<PRINTER_INFO_2>();
                    for (int i = 0; i < returned; i++)
                    {
                        var info = Marshal.PtrToStructure<PRINTER_INFO_2>(IntPtr.Add(buffer, i * size));
                        string name = Marshal.PtrToStringUni(info.pPrinterName) ?? "";
                        string port = Marshal.PtrToStringUni(info.pPortName) ?? "";
                        if (!string.IsNullOrWhiteSpace(name))
                            result.Add(new PrinterOption(name, port));
                    }
                }
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        if (result.Count == 0)
        {
            foreach (string name in PrinterSettings.InstalledPrinters)
                result.Add(new PrinterOption(name, ""));
        }

        return result.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}

internal static class RawPrinter
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private class DOC_INFO_1
    {
        public string pDocName = "Cash Button - apertura de cajon";
        public string pOutputFile = string.Empty;
        public string pDataType = "RAW";
    }

    [DllImport("winspool.drv", EntryPoint = "OpenPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string pPrinterName, out IntPtr phPrinter, IntPtr pDefault);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", EntryPoint = "StartDocPrinterW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern int StartDocPrinter(IntPtr hPrinter, int level, [In] DOC_INFO_1 di);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(IntPtr hPrinter);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(IntPtr hPrinter, byte[] pBytes, int dwCount, out int dwWritten);

    public static void Send(string printerName, byte[] bytes)
    {
        if (!OpenPrinter(printerName, out IntPtr printer, IntPtr.Zero))
            throw new InvalidOperationException($"Windows no pudo abrir la cola «{printerName}». Código: {Marshal.GetLastWin32Error()}.");

        bool documentStarted = false;
        bool pageStarted = false;
        try
        {
            int job = StartDocPrinter(printer, 1, new DOC_INFO_1());
            if (job == 0)
                throw new InvalidOperationException($"No se pudo iniciar el trabajo RAW. Código: {Marshal.GetLastWin32Error()}.");
            documentStarted = true;

            if (!StartPagePrinter(printer))
                throw new InvalidOperationException($"No se pudo iniciar la página RAW. Código: {Marshal.GetLastWin32Error()}.");
            pageStarted = true;

            if (!WritePrinter(printer, bytes, bytes.Length, out int written) || written != bytes.Length)
                throw new InvalidOperationException($"No se pudo transmitir el comando completo. Código: {Marshal.GetLastWin32Error()}.");
        }
        finally
        {
            if (pageStarted) EndPagePrinter(printer);
            if (documentStarted) EndDocPrinter(printer);
            ClosePrinter(printer);
        }
    }
}