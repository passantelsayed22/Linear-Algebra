using System;
using System.Collections.Generic;

namespace GaussSolver
{
    public class GaussJordanEngine
    {
        // =========================
        // REF Class
        // =========================
        public class REF
        {
            private readonly int rows;
            private readonly int cols;
            private readonly Matrix originalMatrix;

            public List<StepRecord> Steps { get; private set; }

            public REF(int r, int c, Matrix m)
            {
                if (m == null)
                    throw new ArgumentNullException(nameof(m));

                if (m.Rows != r || m.Columns != c)
                    throw new ArgumentException(
                        "Invalid matrix dimensions.");

                rows = r;
                cols = c;

                originalMatrix = m.Clone();
                Steps = new List<StepRecord>();
            }

            public Matrix Calculate()
            {
                Steps.Clear();

                Matrix matrix = originalMatrix.Clone();

                int stepNumber = 1;

                // Record the initial matrix
                Steps.Add(new StepRecord(
                   stepNumber: stepNumber++,
                    matrixSnapshot: matrix,
                    operationText: "Initial Matrix",
                    description: "The starting augmented matrix."));

                int currentRow = 0;

                // Find pivots column by column
                for (int i = 0;
                     i < cols && currentRow < rows;
                     i++)
                {
                    int pivotRow = currentRow;

                    // Find a non-zero pivot
                    while (pivotRow < rows &&
                           matrix[pivotRow, i].IsZero)
                    {
                        pivotRow++;
                    }

                    // No pivot in this column
                    if (pivotRow == rows)
                        continue;

                    // Swap rows if necessary
                    if (pivotRow != currentRow)
                    {
                        matrix.SwapRows(pivotRow, currentRow);

                        Steps.Add(new StepRecord(
                           stepNumber: stepNumber++,
                            matrixSnapshot: matrix,
                           operationText: $"R{currentRow + 1} <-> R{pivotRow + 1}",
                            description: "Swap rows to obtain a non-zero pivot.",
                            pivotRow: currentRow,
                           pivotColumn: i,
                           sourceRow: pivotRow,
                           targetRow: currentRow));
                    }

                    // Make the pivot equal to 1
                    Fraction pivot = matrix[currentRow, i];

                    if (!pivot.IsOne)
                    {
                        Fraction factor = Fraction.One / pivot;

                        matrix.ScaleRow(currentRow, factor);

                        Steps.Add(new StepRecord(
                           stepNumber: stepNumber++,
                           matrixSnapshot: matrix,
                           operationText: $"R{currentRow + 1} = ({factor}) * R{currentRow + 1}",
                           description: "Make the pivot equal to 1.",
                           pivotRow: currentRow,
                           pivotColumn: i,
                            sourceRow: currentRow,
                           targetRow: currentRow));
                    }

                    // Eliminate entries below the pivot
                    for (int j = currentRow + 1;
                         j < rows;
                         j++)
                    {
                        Fraction value = matrix[j, i];

                        if (value.IsZero)
                            continue;

                        Fraction factor = -value;

                        matrix.AddMultipleOfRow(
                            j,
                            currentRow,
                            factor);

                        Steps.Add(new StepRecord(
                            stepNumber: stepNumber++,
                           matrixSnapshot:  matrix,
                           operationText: $"R{j + 1} = R{j + 1} - ({value}) * R{currentRow + 1}",
                           description: "Eliminate the entry below the pivot.",
                            pivotRow: currentRow,
                            pivotColumn: i,
                           sourceRow: currentRow,
                           targetRow: j));
                    }

                    currentRow++;
                }

                return matrix.Clone();
            }
        }

        // =========================
        // RREF Class
        // =========================
        public class RREF
        {
            private readonly int rows;
            private readonly int cols;
            private readonly Matrix originalMatrix;

            public List<StepRecord> Steps { get; private set; }

            public RREF(int r, int c, Matrix m)
            {
                if (m == null)
                    throw new ArgumentNullException(nameof(m));

                if (m.Rows != r || m.Columns != c)
                    throw new ArgumentException(
                        "Invalid matrix dimensions.");

                rows = r;
                cols = c;

                originalMatrix = m.Clone();
                Steps = new List<StepRecord>();
            }

            public Matrix Calculate()
            {
                Steps.Clear();

                // First calculate REF
                REF refSolver = new REF(
                    rows,
                    cols,
                    originalMatrix);

                Matrix matrix = refSolver.Calculate();

                // Copy all REF steps
                Steps.AddRange(refSolver.Steps);

                int stepNumber = Steps.Count + 1;

                // Eliminate entries above each pivot
                int pivotRow = 0;

                for (int i = 0;
                     i < cols && pivotRow < rows;
                     i++)
                {
                    // Skip columns without a pivot
                    if (matrix[pivotRow, i].IsZero)
                        continue;

                    Fraction pivot = matrix[pivotRow, i];

                    // Ensure that the pivot equals 1
                    if (!pivot.IsOne)
                    {
                        Fraction factor = Fraction.One / pivot;

                        matrix.ScaleRow(pivotRow, factor);

                        Steps.Add(new StepRecord(
                           stepNumber: stepNumber++,
                           matrixSnapshot: matrix,
                           operationText: $"R{pivotRow + 1} = ({factor}) * R{pivotRow + 1}",
                           description: "Make the pivot equal to 1.",
                           pivotRow: pivotRow,
                           pivotColumn: i,
                           sourceRow: pivotRow,
                           targetRow: pivotRow));
                    }

                    // Eliminate entries above the pivot
                    for (int j = 0; j < pivotRow; j++)
                    {
                        Fraction value = matrix[j, i];

                        if (value.IsZero)
                            continue;

                        Fraction factor = -value;

                        matrix.AddMultipleOfRow(
                            j,
                            pivotRow,
                            factor);

                        Steps.Add(new StepRecord(
                           stepNumber: stepNumber++,
                           matrixSnapshot: matrix,
                          operationText:  $"R{j + 1} = R{j + 1} - ({value}) * R{pivotRow + 1}",
                           description: "Eliminate the entry above the pivot.",
                           pivotRow: pivotRow,
                           pivotColumn: i,
                           sourceRow: pivotRow,
                          targetRow: j));
                    }

                    pivotRow++;
                }

                return matrix.Clone();
            }
        }
    }
}
