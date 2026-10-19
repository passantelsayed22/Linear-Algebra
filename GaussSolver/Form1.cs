using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace GaussSolver
{
    public partial class Form1 : Form
    {
        private List<StepRecord> _currentSteps = new List<StepRecord>();
        private int _currentStepIndex = 0;
        private string[] _variableNames = null;

        private TabControl tabInputMode;
        private TabPage tabGrid;
        private TabPage tabText;
        private NumericUpDown nudRows;
        private NumericUpDown nudCols;
        private DataGridView dgvInput;
        private TextBox txtInput;
        private RadioButton rbREF;
        private RadioButton rbRREF;
        private Button btnSolve;
        private Button btnSample;
        private Button btnClear;
        private Button btnImport;
        private Button btnFirst;
        private Button btnPrev;
        private Button btnNext;
        private Button btnLast;
        private Label lblStepCount;
        private Label lblOperation;
        private Label lblDescription;
        private Label lblFinalResult;
        private DataGridView dgvMatrix;
        //--------------

        public Form1()
        {
            InitializeComponent();
            SetupCustomUI();
            BuildInputGrid((int)nudRows.Value, (int)nudCols.Value);
        }

        private void SetupCustomUI()
        {
            this.Text = "GaussSolver — Step-by-Step Linear Algebra Solver";
            this.Size = new Size(1000, 760);
            this.MinimumSize = new Size(880, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            Panel topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 265,
                Padding = new Padding(12),
                BackColor = Color.White
            };

            Panel rightControlPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 310,
                Padding = new Padding(10, 0, 0, 0),
                BackColor = Color.White
            };

            GroupBox grpMethod = new GroupBox
            {
                Text = "Elimination Method",
                Dock = DockStyle.Top,
                Height = 88,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            rbREF = new RadioButton
            {
                Text = "Gaussian (REF)",
                Location = new Point(15, 26),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };

            rbRREF = new RadioButton
            {
                Text = "Gauss-Jordan (RREF)",
                Location = new Point(15, 54),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };

            grpMethod.Controls.Add(rbREF);
            grpMethod.Controls.Add(rbRREF);

            btnSolve = new Button
            {
                Text = "Solve",
                Location = new Point(10, 96),
                Size = new Size(290, 42),
                BackColor = Color.FromArgb(37, 99, 235),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 10.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSolve.FlatAppearance.BorderSize = 0;
            btnSolve.Click += btnSolve_Click;

            btnSample = new Button
            {
                Text = "Example",
                Location = new Point(10, 146),
                Size = new Size(140, 38),
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(30, 41, 59),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnSample.FlatAppearance.BorderSize = 0;
            btnSample.Click += btnSample_Click;

            btnClear = new Button
            {
                Text = "Clear",
                Location = new Point(160, 146),
                Size = new Size(140, 38),
                BackColor = Color.FromArgb(254, 226, 226),
                ForeColor = Color.FromArgb(185, 28, 28),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnClear.FlatAppearance.BorderSize = 0;
            btnClear.Click += btnClear_Click;

            btnImport = new Button
            {
                Text = "Import File / Image (OCR & PDF)",
                Location = new Point(10, 192),
                Size = new Size(290, 40),
                BackColor = Color.FromArgb(237, 233, 254),
                ForeColor = Color.FromArgb(91, 33, 182),
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            btnImport.FlatAppearance.BorderSize = 0;
            btnImport.Click += btnImport_Click;

            rightControlPanel.Controls.Add(grpMethod);
            rightControlPanel.Controls.Add(btnSolve);
            rightControlPanel.Controls.Add(btnSample);
            rightControlPanel.Controls.Add(btnClear);
            rightControlPanel.Controls.Add(btnImport);

            tabInputMode = new TabControl
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold)
            };

            tabGrid = new TabPage("Matrix Cells (Grid)")
            {
                BackColor = Color.White,
                Padding = new Padding(8)
            };

            Panel dimPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                BackColor = Color.White
            };

            Label lblRows = new Label
            {
                Text = "Rows (Equations):",
                Location = new Point(5, 8),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 50, 70)
            };

            nudRows = new NumericUpDown
            {
                Location = new Point(145, 5),
                Size = new Size(65, 28),
                Minimum = 1,
                Maximum = 10,
                Value = 3,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };
            nudRows.ValueChanged += Dimensions_ValueChanged;

            Label lblCols = new Label
            {
                Text = "Columns (Variables):",
                Location = new Point(230, 8),
                AutoSize = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = Color.FromArgb(40, 50, 70)
            };

            nudCols = new NumericUpDown
            {
                Location = new Point(390, 5),
                Size = new Size(65, 28),
                Minimum = 1,
                Maximum = 10,
                Value = 3,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                TextAlign = HorizontalAlignment.Center
            };
            nudCols.ValueChanged += Dimensions_ValueChanged;

            dimPanel.Controls.Add(lblRows);
            dimPanel.Controls.Add(nudRows);
            dimPanel.Controls.Add(lblCols);
            dimPanel.Controls.Add(nudCols);

            dgvInput = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.FromArgb(248, 250, 252),
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Both,
                EditMode = DataGridViewEditMode.EditOnEnter,
                ColumnHeadersHeight = 32,
                RowTemplate = { Height = 30 },
                Font = new Font("Consolas", 11F)
            };
            dgvInput.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvInput.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvInput.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            tabGrid.Controls.Add(dgvInput);
            tabGrid.Controls.Add(dimPanel);

            tabText = new TabPage("Equations / Text Input")
            {
                BackColor = Color.White,
                Padding = new Padding(8)
            };

            Label lblTextHint = new Label
            {
                Text = "Enter linear equations (e.g., x + 2y + 3z = 9) or space-separated matrix rows:",
                Dock = DockStyle.Top,
                Height = 28,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = Color.FromArgb(71, 85, 105)
            };

            txtInput = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 11F)
            };

            tabText.Controls.Add(txtInput);
            tabText.Controls.Add(lblTextHint);

            tabInputMode.TabPages.Add(tabGrid);
            tabInputMode.TabPages.Add(tabText);

            topPanel.Controls.Add(tabInputMode);
            topPanel.Controls.Add(rightControlPanel);

            Panel stepInfoPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 85,
                Padding = new Padding(15, 8, 15, 8),
                BackColor = Color.FromArgb(238, 242, 255)
            };

            lblStepCount = new Label
            {
                Text = "Step 0 of 0",
                Location = new Point(15, 10),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 58, 138)
            };

            lblOperation = new Label
            {
                Text = "Operation: -",
                Location = new Point(160, 10),
                AutoSize = true,
                Font = new Font("Consolas", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(180, 83, 9)
            };

            lblDescription = new Label
            {
                Text = "Choose Grid Cells, Equations tab, or Import File / Image to start.",
                Location = new Point(15, 38),
                AutoSize = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular),
                ForeColor = Color.FromArgb(51, 65, 85)
            };

            stepInfoPanel.Controls.Add(lblStepCount);
            stepInfoPanel.Controls.Add(lblOperation);
            stepInfoPanel.Controls.Add(lblDescription);

            Panel bottomPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 105,
                Padding = new Padding(15),
                BackColor = Color.White
            };

            lblFinalResult = new Label
            {
                Text = "Final Solution: -",
                Location = new Point(15, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(21, 128, 61)
            };

            btnFirst = new Button
            {
                Text = "|<< First",
                Location = new Point(15, 50),
                Size = new Size(110, 38),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnFirst.Click += btnFirst_Click;

            btnPrev = new Button
            {
                Text = "< Previous",
                Location = new Point(135, 50),
                Size = new Size(120, 38),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnPrev.Click += btnPrev_Click;

            btnNext = new Button
            {
                Text = "Next >",
                Location = new Point(265, 50),
                Size = new Size(120, 38),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnNext.Click += btnNext_Click;

            btnLast = new Button
            {
                Text = "Last >>|",
                Location = new Point(395, 50),
                Size = new Size(110, 38),
                Enabled = false,
                Cursor = Cursors.Hand
            };
            btnLast.Click += btnLast_Click;

            bottomPanel.Controls.Add(lblFinalResult);
            bottomPanel.Controls.Add(btnFirst);
            bottomPanel.Controls.Add(btnPrev);
            bottomPanel.Controls.Add(btnNext);
            bottomPanel.Controls.Add(btnLast);

            Panel gridContainer = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(15)
            };

            dgvMatrix = new DataGridView
            {
                Dock = DockStyle.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ScrollBars = ScrollBars.Both,
                ColumnHeadersHeight = 38,
                RowTemplate = { Height = 36 },
                Font = new Font("Consolas", 11F)
            };
            dgvMatrix.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            dgvMatrix.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            dgvMatrix.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

            gridContainer.Controls.Add(dgvMatrix);

            this.Controls.Add(gridContainer);
            this.Controls.Add(bottomPanel);
            this.Controls.Add(stepInfoPanel);
            this.Controls.Add(topPanel);
        }

        private void Dimensions_ValueChanged(object sender, EventArgs e)
        {
            BuildInputGrid((int)nudRows.Value, (int)nudCols.Value);
        }

        private void BuildInputGrid(int rows, int numVars)
        {
            int totalCols = numVars + 1;

            string[,] oldValues = new string[dgvInput.Rows.Count, dgvInput.Columns.Count];
            for (int r = 0; r < dgvInput.Rows.Count; r++)
            {
                for (int c = 0; c < dgvInput.Columns.Count; c++)
                {
                    oldValues[r, c] = dgvInput.Rows[r].Cells[c].Value?.ToString() ?? "";
                }
            }

            dgvInput.Columns.Clear();
            dgvInput.Rows.Clear();

            for (int c = 0; c < totalCols; c++)
            {
                string header = (c == totalCols - 1) ? "b" : $"x{c + 1}";
                int idx = dgvInput.Columns.Add($"inCol{c}", header);
                dgvInput.Columns[idx].MinimumWidth = 50;
                dgvInput.Columns[idx].SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            for (int r = 0; r < rows; r++)
            {
                string[] rowData = new string[totalCols];
                for (int c = 0; c < totalCols; c++)
                {
                    if (r < oldValues.GetLength(0) && c < oldValues.GetLength(1))
                        rowData[c] = oldValues[r, c];
                    else
                        rowData[c] = "";
                }
                dgvInput.Rows.Add(rowData);
            }
        }

        private void PopulateGridFromMatrix(Matrix m)
        {
            int rCount = Math.Min(10, Math.Max(1, m.Rows));
            int vCount = Math.Min(10, Math.Max(1, m.Columns - 1));

            nudRows.ValueChanged -= Dimensions_ValueChanged;
            nudCols.ValueChanged -= Dimensions_ValueChanged;

            nudRows.Value = rCount;
            nudCols.Value = vCount;

            nudRows.ValueChanged += Dimensions_ValueChanged;
            nudCols.ValueChanged += Dimensions_ValueChanged;

            BuildInputGrid(rCount, vCount);

            for (int r = 0; r < rCount; r++)
            {
                for (int c = 0; c < m.Columns && c < dgvInput.Columns.Count; c++)
                {
                    dgvInput.Rows[r].Cells[c].Value = m[r, c].ToString();
                }
            }
        }

        private bool TryReadGridInput(out Matrix matrix, out string error)
        {
            _variableNames = null;
            dgvInput.EndEdit();
            StringBuilder sb = new StringBuilder();

            for (int r = 0; r < dgvInput.Rows.Count; r++)
            {
                List<string> rowCells = new List<string>();
                for (int c = 0; c < dgvInput.Columns.Count; c++)
                {
                    string val = dgvInput.Rows[r].Cells[c].Value?.ToString()?.Trim() ?? "";
                    if (string.IsNullOrEmpty(val))
                    {
                        matrix = null;
                        string colName = dgvInput.Columns[c].HeaderText;
                        error = $"Please enter a value in Row {r + 1}, Column '{colName}'.";
                        return false;
                    }
                    rowCells.Add(val);
                }
                sb.AppendLine(string.Join(" ", rowCells));
            }

            return InputParser.TryParseMatrix(sb.ToString(), out matrix, out error);
        }

        private bool TryParseTextInput(string rawInput, out Matrix matrix, out string error)
        {
            _variableNames = null;

            if (InputParser.TryParseMatrix(rawInput, out matrix, out string matrixError))
            {
                error = "";
                return true;
            }

            if (InputParser.TryParseEquations(rawInput, out matrix, out string eqError, out string[] vars, out int failedIndex))
            {
                _variableNames = vars;
                error = "";
                return true;
            }

            error = !string.IsNullOrWhiteSpace(eqError) ? eqError : matrixError;
            return false;
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Title = "Import Equations or Matrix from File / Image";
                ofd.Filter = "All Supported Files (*.txt;*.csv;*.pdf;*.png;*.jpg;*.jpeg;*.bmp)|*.txt;*.csv;*.pdf;*.png;*.jpg;*.jpeg;*.bmp|Text & PDF Files (*.txt;*.csv;*.pdf)|*.txt;*.csv;*.pdf|Image Files (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp";

                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    this.Cursor = Cursors.WaitCursor;
                    try
                    {
                        if (!InputParser.TryExtractTextFromFile(ofd.FileName, out string extractedText, out string extractError))
                        {
                            MessageBox.Show(extractError, "Import Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        txtInput.Text = extractedText;
                        tabInputMode.SelectedTab = tabText;

                        if (TryParseTextInput(extractedText, out Matrix parsedMatrix, out _))
                        {
                            PopulateGridFromMatrix(parsedMatrix);
                            btnSolve_Click(sender, e);
                        }
                        else
                        {
                            MessageBox.Show("Text extracted! Please review the extracted equations in the text box and click Solve.",
                                "Text Extracted", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                    finally
                    {
                        this.Cursor = Cursors.Default;
                    }
                }
            }
        }

        private void btnSolve_Click(object sender, EventArgs e)
        {
            try
            {
                Matrix inputMatrix;
                string errorMessage;

                if (tabInputMode.SelectedTab == tabGrid)
                {
                    if (!TryReadGridInput(out inputMatrix, out errorMessage))
                    {
                        MessageBox.Show(errorMessage, "Grid Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }
                else
                {
                    if (!TryParseTextInput(txtInput.Text, out inputMatrix, out errorMessage))
                    {
                        MessageBox.Show(errorMessage, "Equations / Text Input Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                }

                if (inputMatrix.Rows > 10 || inputMatrix.Columns > 11)
                {
                    MessageBox.Show("Maximum supported system size is 10 equations and 10 variables (10×11 augmented matrix).",
                        "Dimension Limit Exceeded", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                Matrix finalMatrix;

                if (rbRREF.Checked)
                {
                    var rrefSolver = new GaussJordanEngine.RREF(inputMatrix.Rows, inputMatrix.Columns, inputMatrix);
                    finalMatrix = rrefSolver.Calculate();
                    _currentSteps = rrefSolver.Steps;
                }
                else
                {
                    var refSolver = new GaussJordanEngine.REF(inputMatrix.Rows, inputMatrix.Columns, inputMatrix);
                    finalMatrix = refSolver.Calculate();
                    _currentSteps = refSolver.Steps;
                }

                lblFinalResult.Text = GaussJordanEngine.GetSolutionSummary(finalMatrix);

                _currentStepIndex = 0;
                DisplayCurrentStep();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while solving:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DisplayCurrentStep()
        {
            if (_currentSteps == null || _currentSteps.Count == 0)
                return;

            StepRecord step = _currentSteps[_currentStepIndex];
            Matrix matrix = step.MatrixSnapshot;

            lblStepCount.Text = $"Step {step.StepNumber} of {_currentSteps.Count}";
            lblOperation.Text = $"Operation: {step.OperationText}";
            lblDescription.Text = step.Description;

            dgvMatrix.Columns.Clear();
            dgvMatrix.Rows.Clear();

            for (int c = 0; c < matrix.Columns; c++)
            {
                string colName;
                if (c == matrix.Columns - 1)
                {
                    colName = "b";
                }
                else if (_variableNames != null && c < _variableNames.Length)
                {
                    colName = _variableNames[c];
                }
                else
                {
                    colName = $"x{c + 1}";
                }

                int colIdx = dgvMatrix.Columns.Add($"col{c}", colName);
                dgvMatrix.Columns[colIdx].MinimumWidth = 65;
                dgvMatrix.Columns[colIdx].SortMode = DataGridViewColumnSortMode.NotSortable;
            }

            for (int r = 0; r < matrix.Rows; r++)
            {
                string[] rowValues = new string[matrix.Columns];
                for (int c = 0; c < matrix.Columns; c++)
                {
                    rowValues[c] = matrix[r, c].ToString();
                }
                dgvMatrix.Rows.Add(rowValues);
            }

            HighlightStepElements(step);
            dgvMatrix.ClearSelection();

            btnFirst.Enabled = (_currentStepIndex > 0);
            btnPrev.Enabled = (_currentStepIndex > 0);
            btnNext.Enabled = (_currentStepIndex < _currentSteps.Count - 1);
            btnLast.Enabled = (_currentStepIndex < _currentSteps.Count - 1);
        }

        private void HighlightStepElements(StepRecord step)
        {
            if (step.SourceRow >= 0 && step.SourceRow < dgvMatrix.Rows.Count)
            {
                dgvMatrix.Rows[step.SourceRow].DefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
            }

            if (step.TargetRow >= 0 && step.TargetRow < dgvMatrix.Rows.Count)
            {
                dgvMatrix.Rows[step.TargetRow].DefaultCellStyle.BackColor = Color.FromArgb(219, 234, 254);
            }

            if (step.PivotRow >= 0 && step.PivotRow < dgvMatrix.Rows.Count &&
                step.PivotColumn >= 0 && step.PivotColumn < dgvMatrix.Columns.Count)
            {
                DataGridViewCell pivotCell = dgvMatrix.Rows[step.PivotRow].Cells[step.PivotColumn];
                pivotCell.Style.BackColor = Color.FromArgb(254, 240, 138);
                pivotCell.Style.Font = new Font(dgvMatrix.Font, FontStyle.Bold);
            }
        }

        private void btnFirst_Click(object sender, EventArgs e)
        {
            if (_currentSteps.Count > 0)
            {
                _currentStepIndex = 0;
                DisplayCurrentStep();
            }
        }

        private void btnPrev_Click(object sender, EventArgs e)
        {
            if (_currentStepIndex > 0)
            {
                _currentStepIndex--;
                DisplayCurrentStep();
            }
        }

        private void btnNext_Click(object sender, EventArgs e)
        {
            if (_currentStepIndex < _currentSteps.Count - 1)
            {
                _currentStepIndex++;
                DisplayCurrentStep();
            }
        }

        private void btnLast_Click(object sender, EventArgs e)
        {
            if (_currentSteps.Count > 0)
            {
                _currentStepIndex = _currentSteps.Count - 1;
                DisplayCurrentStep();
            }
        }

        private void btnSample_Click(object sender, EventArgs e)
        {
            nudRows.Value = 3;
            nudCols.Value = 3;

            string[,] sample = new string[,]
            {
                { "1", "2", "3", "9" },
                { "2", "-1", "1", "8" },
                { "3", "0", "-1", "3" }
            };

            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    dgvInput.Rows[r].Cells[c].Value = sample[r, c];
                }
            }

            txtInput.Text =
                "x + 2y + 3z = 9" + Environment.NewLine +
                "2x - y + z = 8" + Environment.NewLine +
                "3x - z = 3";

            btnSolve_Click(sender, e);
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            for (int r = 0; r < dgvInput.Rows.Count; r++)
            {
                for (int c = 0; c < dgvInput.Columns.Count; c++)
                {
                    dgvInput.Rows[r].Cells[c].Value = "";
                }
            }

            txtInput.Clear();
            dgvMatrix.Rows.Clear();
            dgvMatrix.Columns.Clear();
            _currentSteps.Clear();
            _currentStepIndex = 0;
            _variableNames = null;

            lblStepCount.Text = "Step 0 of 0";
            lblOperation.Text = "Operation: -";
            lblDescription.Text = "Choose Grid Cells, Equations tab, or Import File / Image to start.";
            lblFinalResult.Text = "Final Solution: -";

            btnFirst.Enabled = false;
            btnPrev.Enabled = false;
            btnNext.Enabled = false;
            btnLast.Enabled = false;
        }
    }
}