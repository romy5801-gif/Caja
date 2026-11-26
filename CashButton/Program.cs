using System;
using System.Drawing;
using System.Runtime.InteropServices;
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
    private const string PrinterName = "POS-80 (copy of 1)";
    private readonly Button openButton;
    private readonly NotifyIcon trayIcon;
    private readonly ContextMenuStrip trayMenu;
    private bool allowExit;

    public CashButtonForm()
    {
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
        openButton.MouseDown += DragWindow;
        Controls.Add(openButton);

        trayMenu = new ContextMenuStrip();
        trayMenu.Items.Add("Mostrar Cash Button", null, (_, _) => RestoreWindow());
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

    private async System.Threading.Tasks.Task OpenDrawerAsync()
    {
        openButton.Enabled = false;
        openButton.Text = "ENVIANDO...";
        try
        {
            // ESC/POS: ESC p m t1 t2. m=0 selecciona pin 2.
            byte[] command = { 0x1B, 0x70, 0x00, 0x19, 0xFA };
            await System.Threading.Tasks.Task.Run(() => RawPrinter.Send(PrinterName, command));

            trayIcon.BalloonTipTitle = "Cash Button";
            trayIcon.BalloonTipText = "Comando de apertura enviado a la impresora.";
            trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            trayIcon.ShowBalloonTip(1800);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "No se pudo enviar el comando a la impresora.\n\n" + ex.Message +
                "\n\nVerifica que la POS-8330 esté encendida y que el nombre de impresora coincida.",
                "Cash Button",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        finally
        {
            await System.Threading.Tasks.Task.Delay(350);
            openButton.Text = "🔓  ABRIR CAJA";
            openButton.Enabled = true;
        }
    }

    private void DragWindow(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            ReleaseCapture();
            SendMessage(Handle, 0xA1, (IntPtr)0x2, IntPtr.Zero);
        }
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
            trayMenu.Items[1].Text = enabled
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