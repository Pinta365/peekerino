using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Peekerino.Configuration;
using Peekerino.Shell;
using Peekerino.Services;

namespace Peekerino.UI
{
    public class MainForm : Form
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 0x1337;

        private readonly ExplorerSelectionProvider _selectionProvider;
        private readonly FileSummaryService _summaryService;
        private readonly PeekerinoOptions _options;
        private readonly TrayIconController _trayController;
        private readonly uint _hotkeyModifier;
        private readonly uint _hotkeyKey;
        private readonly string _hotkeyDescription;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        public MainForm(ExplorerSelectionProvider selectionProvider, FileSummaryService summaryService, PeekerinoOptions options)
        {
            _selectionProvider = selectionProvider;
            _summaryService = summaryService;
            _options = options;
            _trayController = new TrayIconController();
            _trayController.OpenRequested += HandleTrayOpenRequested;
            _trayController.ExitRequested += HandleTrayExitRequested;

            _hotkeyModifier = options.Hotkey.Modifier;
            _hotkeyKey = options.Hotkey.Key;
            _hotkeyDescription = BuildHotkeyDescription(_hotkeyModifier, _hotkeyKey);

            Load += (_, _) =>
            {
                Hide();
                ShowInTaskbar = false;
            };

            Text = "Peekerino (hidden)";
            Size = new Size(300, 200);

            var label = new Label
            {
                Text = $"Peekerino running ({_hotkeyDescription}). Use the tray icon to open or exit.",
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleCenter
            };
            Controls.Add(label);
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            bool ok = RegisterHotKey(Handle, HOTKEY_ID, _hotkeyModifier, _hotkeyKey);
            if (!ok)
            {
                MessageBox.Show($"Failed to register the global hotkey ({_hotkeyDescription}). Try a different key or run as admin.", "Peekerino", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID)
            {
                OnPeekerinoHotkey();
            }

            base.WndProc(ref m);
        }

        private void ToggleMainWindow()
        {
            if (!Visible)
            {
                Show();
                ShowInTaskbar = true;
                WindowState = FormWindowState.Normal;
                BringToFront();
            }
            else
            {
                Hide();
                ShowInTaskbar = false;
            }
        }

        private void OnPeekerinoHotkey()
        {
            if (_selectionProvider.TryGetSelectedExplorerItemPath(out var selectedPath) && !string.IsNullOrWhiteSpace(selectedPath))
            {
                ShowPreview(selectedPath);
            }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            UnregisterHotKey(Handle, HOTKEY_ID);
            _trayController.OpenRequested -= HandleTrayOpenRequested;
            _trayController.ExitRequested -= HandleTrayExitRequested;
            _trayController.Dispose();

            base.OnFormClosed(e);
        }

        private void HandleTrayOpenRequested(object? sender, EventArgs e) => ToggleMainWindow();

        private void HandleTrayExitRequested(object? sender, EventArgs e) => Application.Exit();

        private void ShowPreview(string path)
        {
            using var preview = new PreviewForm(path, _summaryService);
            preview.ShowDialog(this);
        }

        private static string BuildHotkeyDescription(uint modifier, uint key)
        {
            var parts = new StringBuilder();

            void Append(string text)
            {
                if (parts.Length > 0)
                {
                    parts.Append("+");
                }
                parts.Append(text);
            }

            const uint MOD_ALT = 0x0001;
            const uint MOD_CONTROL = 0x0002;
            const uint MOD_SHIFT = 0x0004;
            const uint MOD_WIN = 0x0008;

            if ((modifier & MOD_CONTROL) != 0) Append("Ctrl");
            if ((modifier & MOD_ALT) != 0) Append("Alt");
            if ((modifier & MOD_SHIFT) != 0) Append("Shift");
            if ((modifier & MOD_WIN) != 0) Append("Win");

            var keyName = ((Keys)key).ToString();
            Append(keyName);

            return parts.ToString();
        }
    }
}

