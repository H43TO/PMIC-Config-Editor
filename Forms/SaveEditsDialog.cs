using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PMICDumpParser
{
    /// <summary>
    /// Dialog for saving edited PMIC dump files
    /// </summary>
    public partial class SaveEditsDialog : Form
    {
        private string _originalFilePath;

        public string SavePath { get; private set; }
        public bool SaveAsNewFile { get; private set; }

        public SaveEditsDialog(string originalFilePath)
        {
            _originalFilePath = originalFilePath;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Save Edited Dump";
            this.Size = new Size(500, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font = new Font("Segoe UI", 9);
            this.BackColor = Color.White;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var mainPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 5,
                Padding = new Padding(15),
                BackColor = Color.White
            };

            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Title
            var titleLabel = new Label
            {
                Text = "Save Edited PMIC Dump",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 10),
                ForeColor = Color.FromArgb(70, 70, 70)
            };

            // Original file info
            var originalFileLabel = new Label
            {
                Text = $"Original file: {Path.GetFileName(_originalFilePath)}",
                Font = new Font("Segoe UI", 9),
                Dock = DockStyle.Fill,
                Margin = new Padding(0, 0, 0, 5),
                ForeColor = Color.FromArgb(90, 90, 90)
            };

            // Radio buttons for save options
            var overwriteRadio = new RadioButton
            {
                Text = "Overwrite original file",
                Checked = true,
                Font = new Font("Segoe UI", 9),
                Dock = DockStyle.Fill,
                Margin = new Padding(20, 10, 0, 5)
            };

            var newFileRadio = new RadioButton
            {
                Text = "Save as new file",
                Font = new Font("Segoe UI", 9),
                Dock = DockStyle.Fill,
                Margin = new Padding(20, 5, 0, 10)
            };

            // File path for new file
            var filePanel = new Panel
            {
                Height = 30,
                Dock = DockStyle.Fill,
                Enabled = false
            };

            var fileTextBox = new TextBox
            {
                Location = new Point(0, 0),
                Size = new Size(350, 25),
                Font = new Font("Segoe UI", 9),
                Text = GetDefaultNewFileName()
            };

            var browseButton = new Button
            {
                Text = "Browse...",
                Location = new Point(360, 0),
                Size = new Size(80, 25),
                Font = new Font("Segoe UI", 9)
            };

            browseButton.Click += (s, e) =>
            {
                using (var dialog = new SaveFileDialog
                {
                    Filter = "PMIC Dump Files (*.bin)|*.bin|All files (*.*)|*.*",
                    FileName = Path.GetFileName(fileTextBox.Text),
                    DefaultExt = ".bin"
                })
                {
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        fileTextBox.Text = dialog.FileName;
                    }
                }
            };

            filePanel.Controls.Add(fileTextBox);
            filePanel.Controls.Add(browseButton);

            // Update file panel state based on radio button selection
            overwriteRadio.CheckedChanged += (s, e) =>
            {
                filePanel.Enabled = !overwriteRadio.Checked;
            };

            newFileRadio.CheckedChanged += (s, e) =>
            {
                filePanel.Enabled = newFileRadio.Checked;
            };

            // Buttons
            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                Height = 40,
                Margin = new Padding(0, 20, 0, 0)
            };

            var cancelButton = new Button
            {
                Text = "Cancel",
                DialogResult = DialogResult.Cancel,
                Size = new Size(80, 30),
                Font = new Font("Segoe UI", 9)
            };

            var saveButton = new Button
            {
                Text = "Save",
                Size = new Size(80, 30),
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            saveButton.FlatAppearance.BorderSize = 0;

            saveButton.Click += (s, e) =>
            {
                if (overwriteRadio.Checked)
                {
                    SavePath = _originalFilePath;
                    SaveAsNewFile = false;
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(fileTextBox.Text))
                    {
                        MessageBox.Show("Please specify a file path.", "Error",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }

                    SavePath = fileTextBox.Text;
                    SaveAsNewFile = true;
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            };

            buttonPanel.Controls.Add(saveButton);
            buttonPanel.Controls.Add(cancelButton);

            mainPanel.Controls.Add(titleLabel, 0, 0);
            mainPanel.Controls.Add(originalFileLabel, 0, 1);
            mainPanel.Controls.Add(overwriteRadio, 0, 2);
            mainPanel.Controls.Add(newFileRadio, 0, 3);
            mainPanel.Controls.Add(filePanel, 0, 4);

            this.Controls.Add(mainPanel);
            this.Controls.Add(buttonPanel);

            // Set Accept and Cancel buttons
            this.AcceptButton = saveButton;
            this.CancelButton = cancelButton;
        }

        private string GetDefaultNewFileName()
        {
            string dir = Path.GetDirectoryName(_originalFilePath);
            string name = Path.GetFileNameWithoutExtension(_originalFilePath);
            string ext = Path.GetExtension(_originalFilePath);

            return Path.Combine(dir, $"{name}_edited{ext}");
        }
    }
}