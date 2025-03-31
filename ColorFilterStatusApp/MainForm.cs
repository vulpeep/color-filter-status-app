using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ColorFilterStatusApp
{
    public partial class MainForm : Form
    {
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private Timer updateTimer;

        // Icons for different states
        private Icon iconEnabled;
        private Icon iconDisabled;

        private static readonly IntPtr HKEY_CURRENT_USER = new IntPtr(unchecked((int)0x80000001));

        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern int SHSetValue(
            IntPtr hkey,
            string pszSubKey,
            string pszValue,
            uint dwType,
            ref int pvData,
            int cbData);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr ShellExecute(
            IntPtr hwnd,
            string lpOperation,
            string lpFile,
            string lpParameters,
            string lpDirectory,
            int nShowCmd);

        // List of available color filter modes.
        // Example modes: 0 - Grayscale, 1 - Inverted, 2 - Grayscale Inverted, 3 - Deuteranopia, 4 - Protanopia, 5 - Tritanopia.
        private readonly (string name, int value)[] filterModes = new (string, int)[]
        {
            ("Grayscale", 0),
            ("Inverted", 1),
            ("Grayscale Inverted", 2),
            ("Deuteranopia", 3),
            ("Protanopia", 4),
            ("Tritanopia", 5)
        };

        public MainForm()
        {
            InitializeComponent();
            LoadIcons();
            InitializeTray();
            InitializeTimer();
        }

        public class CustomToolStripRenderer : ToolStripProfessionalRenderer
        {
            protected override void OnRenderImageMargin(ToolStripRenderEventArgs e)
            {
                Color marginColor = Color.White;
                using (SolidBrush brush = new SolidBrush(marginColor))
                {
                    e.Graphics.FillRectangle(brush, e.AffectedBounds);
                }
            }
        }

        // Basic form initialization.
        // The form will be hidden since all interaction occurs via the system tray.
        private void InitializeComponent()
        {
            // Hide the window from the taskbar and minimize it
            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.Load += MainForm_Load;
        }

        // Form Load event handler; hide the form immediately after loading.
        private void MainForm_Load(object sender, EventArgs e)
        {
            this.Hide();
        }

        // Load icons from embedded resources.
        private void LoadIcons()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                // Resource names follow the format: {DefaultNamespace}.{Folder}.{FileName}
                using (var stream = assembly.GetManifestResourceStream("ColorFilterStatusApp.Resources.icon_enabled.ico"))
                {
                    iconEnabled = new Icon(stream);
                }
                using (var stream = assembly.GetManifestResourceStream("ColorFilterStatusApp.Resources.icon_disabled.ico"))
                {
                    iconDisabled = new Icon(stream);
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
                // If loading icons fails, use the default application icon
                iconEnabled = SystemIcons.Application;
                iconDisabled = SystemIcons.Application;
            }
        }

        // Create the tray icon and context menu
        private void InitializeTray()
        {
            trayMenu = new ContextMenuStrip();

            trayMenu.Renderer = new CustomToolStripRenderer();

            // Menu item for toggling the color filter (enable/disable)
            var toggleItem = new ToolStripMenuItem("Toggle Color Filter");
            toggleItem.Click += ToggleItem_Click;
            trayMenu.Items.Add(toggleItem);

            trayMenu.Items.Add(new ToolStripSeparator());

            // Submenu for selecting the filter mode
            var modeSubMenu = new ToolStripMenuItem("Select Mode");
            foreach (var mode in filterModes)
            {
                var item = new ToolStripMenuItem(mode.name)
                {
                    Tag = mode.value
                };
                item.Click += ModeItem_Click;
                modeSubMenu.DropDownItems.Add(item);
            }
            trayMenu.Items.Add(modeSubMenu);

            trayMenu.Items.Add(new ToolStripSeparator());

            // Exit menu item
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += ExitItem_Click;
            trayMenu.Items.Add(exitItem);

            trayIcon = new NotifyIcon
            {
                Text = "Color Filter Status",
                Icon = iconDisabled, // initial state
                ContextMenuStrip = trayMenu,
                Visible = true
            };
        }

        // Initialize a timer to update the tray icon status
        private void InitializeTimer()
        {
            updateTimer = new Timer
            {
                Interval = 1000 // update every second
            };
            updateTimer.Tick += UpdateTimer_Tick;
            updateTimer.Start();
        }

        // Update the tooltip of the tray icon and the text of menu items
        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                bool isActive = GetColorFilterStatus();
                int currentMode = GetColorFilterMode();

                // Determine the mode name
                string modeName = "Unknown";
                foreach (var mode in filterModes)
                {
                    if (mode.value == currentMode)
                    {
                        modeName = mode.name;
                        break;
                    }
                }
                // Update the tooltip of the tray icon
                trayIcon.Text = $"Color Filters: {(isActive ? "Enabled" : "Disabled")}\nMode: {modeName}";

                // Update the tray icon based on the state
                trayIcon.Icon = isActive ? iconEnabled : iconDisabled;

                // Update the toggle menu item text
                if (trayMenu.Items[0] is ToolStripMenuItem toggleItem)
                {
                    toggleItem.Text = isActive ? "Disable Filters" : "Enable Filters";
                }

                // Update check marks in the mode submenu
                if (trayMenu.Items[2] is ToolStripMenuItem modeSubMenu)
                {
                    foreach (ToolStripMenuItem item in modeSubMenu.DropDownItems)
                    {
                        int modeValue = (int)item.Tag;
                        item.Checked = (modeValue == currentMode);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        // Event handler for toggling the color filter
        private void ToggleItem_Click(object sender, EventArgs e)
        {
            try
            {
                bool isActive = GetColorFilterStatus();
                SetColorFilterStatus(!isActive);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        // Event handler for selecting a filter mode
        private void ModeItem_Click(object sender, EventArgs e)
        {
            if (sender is ToolStripMenuItem item)
            {
                try
                {
                    int modeValue = (int)item.Tag;
                    // Set the selected mode and enable the filter
                    SetColorFilterMode(modeValue);
                    SetColorFilterStatus(true);
                }
                catch (Exception ex)
                {
                    LogError(ex);
                }
            }
        }

        // Event handler for exiting the application
        private void ExitItem_Click(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        #region Registry Operations

        // Get the current color filter status from the registry
        private bool GetColorFilterStatus()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\ColorFiltering"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("Active");
                        if (value != null && int.TryParse(value.ToString(), out int active))
                        {
                            return active != 0;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
            return false;
        }

        // Set the color filter status (enable/disable) in the registry
        private void SetColorFilterStatus(bool active)
        {
            try
            {
                int nActive = active ? 1 : 0;
                // REG_DWORD is 4
                int nRet = SHSetValue(HKEY_CURRENT_USER, @"Software\Microsoft\ColorFiltering", "Active", 4, ref nActive, 4);
                // Calling atbroker.exe to reset transfer keys and update the filter effect
                ShellExecute(IntPtr.Zero, null, @"C:\WINDOWS\system32\atbroker.exe", "/colorfiltershortcut /resettransferkeys", null, 1);
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        // Get the current color filter mode from the registry
        private int GetColorFilterMode()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\ColorFiltering"))
                {
                    if (key != null)
                    {
                        object value = key.GetValue("FilterType");
                        if (value != null && int.TryParse(value.ToString(), out int mode))
                        {
                            return mode;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
            return -1; // unknown mode
        }

        // Set the selected color filter mode in the registry
        private void SetColorFilterMode(int mode)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\ColorFiltering"))
                {
                    if (key != null)
                    {
                        key.SetValue("FilterType", mode, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception ex)
            {
                LogError(ex);
            }
        }

        #endregion

        // Log errors to the file error.log in the application directory
        private void LogError(Exception ex)
        {
            try
            {
                string logFilePath = "error.log";
                string errorMessage = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - " + ex.ToString();
                File.AppendAllText(logFilePath, errorMessage + Environment.NewLine);
            }
            catch
            {
                // Suppress logging errors
            }
        }
    }
}
