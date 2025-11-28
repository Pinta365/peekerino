using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Peekerino.Services;

namespace Peekerino.UI
{
    internal sealed class PreviewForm : Form
    {
        private readonly string _path;
        private readonly FileSummaryService _summaryService;
        private CancellationTokenSource? _cts;
        private TextBox? _textBox;
        private DataGridView? _grid;
        private Label? _gridLabel;
        private SplitContainer? _splitContainer;
        private System.Windows.Forms.Timer? _statusTimer;
        private int _statusFrameIndex;
        private static readonly string[] _statusFrames = { "Peeking", "Peeking.", "Peeking..", "Peeking..." };
        private ComboBox? _tableSelector;
        private IReadOnlyList<TableSummary>? _tables;

        public PreviewForm(string path, FileSummaryService summaryService)
        {
            _path = path;
            _summaryService = summaryService;

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            Width = 720;
            Height = 520;
            StartPosition = FormStartPosition.CenterParent;
            ShowInTaskbar = false;
            Text = $"Peekerino - Preview: {Path.GetFileName(_path)}";

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToOrderColumns = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                BackgroundColor = System.Drawing.SystemColors.Window
            };

            _gridLabel = new Label
            {
                AutoSize = true,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Margin = new Padding(0, 8, 8, 0)
            };

            _tableSelector = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 260,
                Visible = false,
                Margin = new Padding(0, 4, 0, 0)
            };
            _tableSelector.SelectedIndexChanged += TableSelectorOnSelectedIndexChanged;

            var tableHeaderPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = false,
                Padding = new Padding(3, 0, 3, 0)
            };
            tableHeaderPanel.Controls.Add(_gridLabel);
            tableHeaderPanel.Controls.Add(_tableSelector);

            var gridContainer = new Panel
            {
                Dock = DockStyle.Fill
            };
            gridContainer.Controls.Add(_grid);
            gridContainer.Controls.Add(tableHeaderPanel);

            _textBox = new TextBox
            {
                Name = "txtSummary",
                Multiline = true,
                ReadOnly = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Both,
                Font = new System.Drawing.Font("Consolas", 10),
                WordWrap = false
            };

            _splitContainer = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 240,
                SplitterWidth = 6,
                Panel1MinSize = 80,
                Panel2MinSize = 120
            };
            _splitContainer.Panel1.Controls.Add(gridContainer);
            _splitContainer.Panel2.Controls.Add(_textBox);

            var buttonPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 44,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(6),
                WrapContents = false
            };

            var closeButton = new Button { Text = "Close", Width = 90 };
            closeButton.Click += (_, _) => Close();

            buttonPanel.Controls.Add(closeButton);

            Controls.Add(_splitContainer);
            Controls.Add(buttonPanel);

            Load += PreviewForm_Load;
            Shown += PreviewForm_Shown;
            FormClosing += PreviewForm_FormClosing;
        }

        private async void PreviewForm_Load(object? sender, EventArgs e)
        {
            if (_textBox == null)
            {
                return;
            }

            _cts = new CancellationTokenSource();
            StartLoadingAnimation();
            HideTable();

            try
            {
                FileSummaryResult result = await Task.Run(async () => await _summaryService.BuildSummaryAsync(_path, _cts.Token), _cts.Token);
                StopLoadingAnimation();
                _textBox.Text = BuildText(result);

                if (result.Tables.Count > 0)
                {
                    ShowTables(result.Tables);
                }
                else
                {
                    HideTable();
                }
            }
            catch (OperationCanceledException)
            {
                StopLoadingAnimation();
                _textBox.Text = "Summary canceled.";
            }
            catch (Exception ex)
            {
                StopLoadingAnimation();
                _textBox.Text = $"Failed to create summary:{Environment.NewLine}{ex.Message}";
            }
        }

        private void PreviewForm_Shown(object? sender, EventArgs e)
        {
            BeginInvoke(new Action(() =>
            {
                Activate();
                BringToFront();
                if (_textBox != null)
                {
                    _textBox.SelectionStart = 0;
                    _textBox.SelectionLength = 0;
                    _textBox.ScrollToCaret();
                    _textBox.Focus();
                }
            }));
        }

        private string BuildText(FileSummaryResult result)
        {
            var builder = new StringBuilder(result.Body ?? string.Empty);

            if (result.Preview != null && !string.IsNullOrWhiteSpace(result.Preview.Content))
            {
                if (builder.Length > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                builder.AppendLine($"Preview: {result.Preview.Title}");
                builder.AppendLine(result.Preview.Content);
                if (result.Preview.IsTruncated)
                {
                    builder.AppendLine("... (truncated preview)");
                }
            }

            return builder.ToString();
        }

        private void ShowTables(IReadOnlyList<TableSummary> tables)
        {
            _tables = tables;

            if (tables == null || tables.Count == 0)
            {
                HideTable();
                return;
            }

            if (_tableSelector != null)
            {
                _tableSelector.SelectedIndexChanged -= TableSelectorOnSelectedIndexChanged;
                _tableSelector.Items.Clear();
                foreach (var table in tables)
                {
                    _tableSelector.Items.Add(table.Title);
                }

                _tableSelector.Visible = tables.Count > 1;
                if (tables.Count > 0)
                {
                    _tableSelector.SelectedIndex = 0;
                }
                _tableSelector.SelectedIndexChanged += TableSelectorOnSelectedIndexChanged;
            }

            PopulateTable(tables[0]);
        }

        private void PopulateTable(TableSummary table)
        {
            if (_grid == null || _splitContainer == null || _gridLabel == null)
            {
                return;
            }

            if (table.Rows.Count == 0)
            {
                HideTable();
                return;
            }

            var dataTable = new DataTable();
            for (int i = 0; i < table.Headers.Count; i++)
            {
                string header = table.Headers[i];
                string columnName = string.IsNullOrWhiteSpace(header) ? $"Column {i + 1}" : header;
                string uniqueName = columnName;
                int suffix = 1;
                while (dataTable.Columns.Contains(uniqueName))
                {
                    suffix++;
                    uniqueName = $"{columnName} ({suffix})";
                }

                dataTable.Columns.Add(uniqueName);
            }

            foreach (var row in table.Rows)
            {
                var values = new object[dataTable.Columns.Count];
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = i < row.Length ? row[i] : string.Empty;
                }
                dataTable.Rows.Add(values);
            }

            _grid.DataSource = dataTable;
            _grid.AutoSizeColumnsMode = dataTable.Columns.Count > 8
                ? DataGridViewAutoSizeColumnsMode.DisplayedCells
                : DataGridViewAutoSizeColumnsMode.AllCells;

            _gridLabel.Text = table.Title + (table.IsTruncated ? " (truncated)" : string.Empty);
            _splitContainer.Panel1Collapsed = false;
            _splitContainer.SplitterDistance = Math.Min(Math.Max(150, _splitContainer.SplitterDistance), _splitContainer.Height - 120);
        }

        private void HideTable()
        {
            if (_splitContainer != null)
            {
                _splitContainer.Panel1Collapsed = true;
            }
            if (_grid != null)
            {
                _grid.DataSource = null;
            }
            if (_tableSelector != null)
            {
                _tableSelector.Visible = false;
                _tableSelector.SelectedIndexChanged -= TableSelectorOnSelectedIndexChanged;
                _tableSelector.Items.Clear();
            }
            _tables = null;
            if (_gridLabel != null)
            {
                _gridLabel.Text = string.Empty;
            }
        }

        private void PreviewForm_FormClosing(object? sender, FormClosingEventArgs e)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            StopLoadingAnimation();
            _statusTimer?.Dispose();
            _statusTimer = null;
            if (_tableSelector != null)
            {
                _tableSelector.SelectedIndexChanged -= TableSelectorOnSelectedIndexChanged;
            }
        }

        private void StartLoadingAnimation()
        {
            if (_textBox == null)
            {
                return;
            }

            _statusFrameIndex = 0;
            _textBox.Text = _statusFrames[_statusFrameIndex];

            _statusTimer ??= new System.Windows.Forms.Timer { Interval = 300 };
            _statusTimer.Tick -= StatusTimerOnTick;
            _statusTimer.Tick += StatusTimerOnTick;
            _statusTimer.Start();
        }

        private void StopLoadingAnimation()
        {
            if (_statusTimer == null)
            {
                return;
            }

            _statusTimer.Stop();
            _statusTimer.Tick -= StatusTimerOnTick;
        }

        private void StatusTimerOnTick(object? sender, EventArgs e)
        {
            if (_textBox == null)
            {
                return;
            }

            _statusFrameIndex = (_statusFrameIndex + 1) % _statusFrames.Length;
            _textBox.Text = _statusFrames[_statusFrameIndex];
        }

        private void TableSelectorOnSelectedIndexChanged(object? sender, EventArgs e)
        {
            if (_tables == null || _tableSelector == null)
            {
                return;
            }

            int index = _tableSelector.SelectedIndex;
            if (index < 0 || index >= _tables.Count)
            {
                return;
            }

            PopulateTable(_tables[index]);
        }
    }
}

