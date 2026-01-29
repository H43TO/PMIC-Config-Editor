using PMICDumpParser.Models;
using PMICDumpParser.UI.Controls.RegisterDetailTabs;
using System;
using System.Drawing;
using System.Windows.Forms;

namespace PMICDumpParser
{
    /// <summary>
    /// Form for displaying detailed information about a PMIC register
    /// </summary>
    public partial class RegisterDetailForm : Form
    {
        private readonly ParsedRegister _register;
        private readonly ToolTip _toolTip;

        /// <summary>
        /// Initializes a new instance of the RegisterDetailForm class
        /// </summary>
        /// <param name="register">The register to display</param>
        public RegisterDetailForm(ParsedRegister register)
        {
            _register = register;
            _toolTip = new ToolTip();
            InitializeComponent();
        }

        /// <summary>
        /// Clean up any resources being used
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _toolTip?.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Initializes the form UI components
        /// </summary>
        private void InitializeComponent()
        {
            this.Text = $"{_register.Name} - 0x{_register.Address:X2}";
            this.Size = new Size(1100, 700);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(900, 600);
            this.Font = new Font("Segoe UI", 9.5f);
            this.BackColor = Color.White;
            this.Padding = new Padding(1);

            // Main layout container
            var mainLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(8, 8, 8, 8),
                Margin = new Padding(0, 0, 0, 0)
            };
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50));

            // Tab control for different views
            var tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Padding = new Point(6, 6),
                Font = new Font("Segoe UI", 9),
                Appearance = TabAppearance.FlatButtons,
                ItemSize = new Size(100, 28),
                SizeMode = TabSizeMode.Fixed
            };

            // Create tabs using separate controls
            tabControl.TabPages.Add(CreateTabPage("Overview", new OverviewTab(_register, _toolTip)));
            tabControl.TabPages.Add(CreateTabPage("Bit Field Analysis", new BitFieldAnalysisTab(_register, _toolTip)));
            tabControl.TabPages.Add(CreateTabPage("Field Details", new FieldDetailsTab(_register, _toolTip)));
            tabControl.TabPages.Add(CreateTabPage("Raw Data", new RawDataTab(_register)));
            tabControl.TabPages.Add(CreateTabPage("Comparison", new ComparisonTab(_register, _toolTip)));

            // Style tab headers
            foreach (TabPage tab in tabControl.TabPages)
            {
                tab.BackColor = Color.White;
                tab.Padding = new Padding(6, 6, 6, 6);
            }

            mainLayout.Controls.Add(tabControl, 0, 0);

            // Close button panel
            var buttonPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 8, 0, 0),
                BackColor = Color.White
            };

            var closeButton = new Button
            {
                Text = "Close",
                DialogResult = DialogResult.OK,
                Size = new Size(90, 32),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Font = new Font("Segoe UI", 9, FontStyle.Bold),
                BackColor = Color.FromArgb(70, 130, 180),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            closeButton.FlatAppearance.BorderSize = 0;
            closeButton.Location = new Point(buttonPanel.Width - closeButton.Width - 10, 0);

            buttonPanel.Controls.Add(closeButton);
            mainLayout.Controls.Add(buttonPanel, 0, 1);

            this.Controls.Add(mainLayout);
        }

        /// <summary>
        /// Creates a tab page with the specified control
        /// </summary>
        private TabPage CreateTabPage(string title, Control contentControl)
        {
            var tabPage = new TabPage(title);
            contentControl.Dock = DockStyle.Fill;
            tabPage.Controls.Add(contentControl);
            return tabPage;
        }
    }
}