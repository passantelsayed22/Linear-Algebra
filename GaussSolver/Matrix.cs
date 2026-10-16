using System;

namespace GaussSolver
{
    public class Matrix
    {
        private readonly Fraction[,] data;

        public int Rows { get; }
        public int Columns { get; }
        public int Cols => Columns;

        public Matrix(int rows, int columns)
        {
            if (rows <= 0)
                throw new ArgumentOutOfRangeException(nameof(rows));

            if (columns <= 0)
                throw new ArgumentOutOfRangeException(nameof(columns));

            Rows = rows;
            Columns = columns;
            data = new Fraction[rows, columns];

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < columns; c++)
                    data[r, c] = Fraction.Zero;
            }
        }

        public Matrix(Fraction[,] initialData)
        {
            if (initialData == null)
                throw new ArgumentNullException(nameof(initialData));

            Rows = initialData.GetLength(0);
            Columns = initialData.GetLength(1);

            if (Rows <= 0 || Columns <= 0)
                throw new ArgumentException("Matrix dimensions must be greater than zero.");

            data = new Fraction[Rows, Columns];

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                    data[r, c] = initialData[r, c];
            }
        }

        public Fraction this[int row, int column]
        {
            get
            {
                CheckCell(row, column);
                return data[row, column];
            }
            set
            {
                CheckCell(row, column);
                data[row, column] = value;
            }
        }

        private void CheckCell(int row, int column)
        {
            CheckRow(row);

            if (column < 0 || column >= Columns)
                throw new ArgumentOutOfRangeException(nameof(column));
        }

        private void CheckRow(int row)
        {
            if (row < 0 || row >= Rows)
                throw new ArgumentOutOfRangeException(nameof(row));
        }

        public Matrix Clone()
        {
            Matrix copy = new Matrix(Rows, Columns);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Columns; c++)
                    copy[r, c] = this[r, c];
            }

            return copy;
        }

        public void SwapRows(int r1, int r2)
        {
            CheckRow(r1);
            CheckRow(r2);

            if (r1 == r2)
                return;

            for (int c = 0; c < Columns; c++)
            {
                Fraction temp = data[r1, c];
                data[r1, c] = data[r2, c];
                data[r2, c] = temp;
            }
        }

        public void ScaleRow(int row, Fraction factor)
        {
            CheckRow(row);

            for (int c = 0; c < Columns; c++)
                data[row, c] = data[row, c] * factor;
        }

        public void AddMultipleOfRow(
            int targetRow,
            int sourceRow,
            Fraction factor)
        {
            CheckRow(targetRow);
            CheckRow(sourceRow);

            if (targetRow == sourceRow)
            {
                ScaleRow(
                    targetRow,
                    new Fraction(1, 1) + factor);
                return;
            }

            for (int c = 0; c < Columns; c++)
            {
                data[targetRow, c] =
                    data[targetRow, c] +
                    factor * data[sourceRow, c];
            }
        }

        public void SetRow(int row, Fraction[] rowValues)
        {
            CheckRow(row);

            if (rowValues == null)
                throw new ArgumentNullException(nameof(rowValues));

            if (rowValues.Length != Columns)
                throw new ArgumentException(
                    "The row length must equal the number of columns.",
                    nameof(rowValues));

            for (int c = 0; c < Columns; c++)
                data[row, c] = rowValues[c];
        }
    }
}
