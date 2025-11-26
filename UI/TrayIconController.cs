using System;
using System.Drawing;
using System.Windows.Forms;

namespace Peekerino.UI
{
    internal sealed class TrayIconController : IDisposable
    {
        private readonly NotifyIcon _trayIcon;
        private readonly ContextMenuStrip _trayMenu;
        private readonly ToolStripMenuItem _openItem;
        private readonly ToolStripMenuItem _exitItem;

        public event EventHandler? OpenRequested;
        public event EventHandler? ExitRequested;

        public TrayIconController()
        {
            _trayMenu = new ContextMenuStrip();

            _openItem = new ToolStripMenuItem("Open Peekerino");
            _openItem.Click += HandleOpenRequested;
            _exitItem = new ToolStripMenuItem("Exit");
            _exitItem.Click += HandleExitRequested;

            _trayMenu.Items.Add(_openItem);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_exitItem);

            _trayIcon = new NotifyIcon
            {
                Icon = SystemIcons.Application,
                ContextMenuStrip = _trayMenu,
                Text = "Peekerino",
                Visible = true
            };

            _trayIcon.DoubleClick += HandleOpenRequested;
        }

        public void Dispose()
        {
            _trayIcon.DoubleClick -= HandleOpenRequested;
            _trayIcon.Visible = false;
            _trayIcon.Dispose();

            _openItem.Click -= HandleOpenRequested;
            _exitItem.Click -= HandleExitRequested;

            _trayMenu.Dispose();
        }

        private void HandleOpenRequested(object? sender, EventArgs e) => OpenRequested?.Invoke(this, EventArgs.Empty);

        private void HandleExitRequested(object? sender, EventArgs e) => ExitRequested?.Invoke(this, EventArgs.Empty);
    }
}

