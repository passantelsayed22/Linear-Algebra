using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace GaussSolver
{
    public static class InputParser
    {
        public static bool TryParseMatrix(
            string input,
            out Matrix matrix,
            out string error)
        {
            matrix = null!;
            error = "";

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Please enter a matrix.";
                return false;
            }

            string[] lines = input.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            var rows = new List<Fraction[]>();
            int columnCount = -1;

            foreach (string line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                string[] parts = Regex.Split(line.Trim(), @"[\s,;|]+")
                    .Where(p => p.Length > 0)
                    .ToArray();

                if (columnCount == -1)
                    columnCount = parts.Length;

                if (parts.Length != columnCount)
                {
                    error = "All matrix rows must have the same number of values.";
                    return false;
                }

                var values = new Fraction[parts.Length];

                for (int i = 0; i < parts.Length; i++)
                {
                    if (!Fraction.TryParse(parts[i], out Fraction value))
                    {
                        error = $"Invalid value '{parts[i]}' in row {rows.Count + 1}.";
                        return false;
                    }

                    values[i] = value;
                }

                rows.Add(values);
            }

            if (rows.Count == 0 || columnCount <= 0)
            {
                error = "The matrix is empty.";
                return false;
            }

            matrix = new Matrix(rows.Count, columnCount);

            for (int r = 0; r < rows.Count; r++)
                matrix.SetRow(r, rows[r]);

            return true;
        }

        public static bool TryParseEquations(
            string input,
            out Matrix matrix,
            out string error)
        {
            return TryParseEquations(
                input,
                out matrix,
                out error,
                out _,
                out _);
        }

        public static bool TryParseEquations(
            string input,
            out Matrix matrix,
            out string error,
            out string[] variableNames,
            out int failedEquationIndex)
        {
            matrix = null!;
            error = "";
            variableNames = Array.Empty<string>();
            failedEquationIndex = -1;

            if (string.IsNullOrWhiteSpace(input))
            {
                error = "Please enter equations.";
                return false;
            }

            string[] lines = input.Split(
                new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries);

            var parsedRows = new List<Dictionary<string, Fraction>>();
            var constants = new List<Fraction>();
            var variables = new SortedSet<string>(
                StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!TryParseEquation(
                    lines[i],
                    out Fraction[] values,
                    out string lineError,
                    out string[] names))
                {
                    failedEquationIndex = i;
                    error = $"Equation {i + 1}: {lineError}";
                    return false;
                }

                var row = new Dictionary<string, Fraction>(
                    StringComparer.OrdinalIgnoreCase);

                for (int j = 0; j < names.Length; j++)
                {
                    row[names[j]] = values[j];
                    variables.Add(names[j]);
                }

                parsedRows.Add(row);
                constants.Add(values[values.Length - 1]);
            }

            variableNames = variables.ToArray();

            if (variableNames.Length == 0)
            {
                error = "No variables were found in the equations.";
                return false;
            }

            matrix = new Matrix(lines.Length, variableNames.Length + 1);

            for (int r = 0; r < lines.Length; r++)
            {
                for (int c = 0; c < variableNames.Length; c++)
                {
                    matrix[r, c] = parsedRows[r].TryGetValue(
                        variableNames[c], out Fraction? coefficient)
                        ? coefficient
                        : Fraction.Zero;
                }

                matrix[r, variableNames.Length] = constants[r];
            }

            return true;
        }

        public static bool TryParseSingleEquationForMatrix(
            string equationStr,
            string[] expectedVariables,
            out Fraction[] alignedRowValues,
            out string errorMessage)
        {
            alignedRowValues = Array.Empty<Fraction>();

            if (!TryParseEquation(
                equationStr,
                out Fraction[] rawValues,
                out errorMessage,
                out string[] foundNames))
            {
                return false;
            }

            var coeffMap = new Dictionary<string, Fraction>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < foundNames.Length; i++)
            {
                if (!expectedVariables.Contains(foundNames[i], StringComparer.OrdinalIgnoreCase))
                {
                    errorMessage = $"Unexpected variable '{foundNames[i]}'. Expected variables: {string.Join(", ", expectedVariables)}.";
                    return false;
                }
                coeffMap[foundNames[i]] = rawValues[i];
            }

            alignedRowValues = new Fraction[expectedVariables.Length + 1];
            for (int c = 0; c < expectedVariables.Length; c++)
            {
                alignedRowValues[c] = coeffMap.TryGetValue(expectedVariables[c], out Fraction? val)
                    ? val
                    : Fraction.Zero;
            }

            alignedRowValues[expectedVariables.Length] = rawValues[rawValues.Length - 1];
            return true;
        }

        public static bool TryParseEquation(
            string equationStr,
            out Fraction[] rowValues,
            out string errorMessage)
        {
            return TryParseEquation(
                equationStr,
                out rowValues,
                out errorMessage,
                out _);
        }

        private static bool TryParseEquation(
            string equationStr,
            out Fraction[] rowValues,
            out string errorMessage,
            out string[] variableNames)
        {
            rowValues = Array.Empty<Fraction>();
            variableNames = Array.Empty<string>();
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(equationStr))
            {
                errorMessage = "The equation is empty.";
                return false;
            }

            string normalizedEq = equationStr
                .Replace('\u2212', '-')
                .Replace('\u2013', '-')
                .Replace('\u2014', '-')
                .Replace('×', '*');

            string[] sides = normalizedEq.Split('=');

            if (sides.Length != 2)
            {
                errorMessage = "Each equation must contain exactly one '=' sign.";
                return false;
            }

            string left = sides[0].Trim();
            string right = sides[1].Trim();

            if (!Fraction.TryParse(right, out Fraction constant))
            {
                errorMessage = "The right-hand side must be a valid number or fraction.";
                return false;
            }

            var coefficients = new Dictionary<string, Fraction>(
                StringComparer.OrdinalIgnoreCase);

            string pattern =
                @"(?<sign>[+-]?)\s*(?<coef>(?:\d+\s*/\s*\d+|\d+(?:\.\d+)?|\.\d+)?)\s*\*?\s*(?<var>[a-zA-Z]\w*)";

            MatchCollection matches = Regex.Matches(left, pattern);

            if (matches.Count == 0)
            {
                errorMessage = "No valid variables were found on the left-hand side.";
                return false;
            }

            string remainder = Regex.Replace(left, pattern, "");
            remainder = Regex.Replace(remainder, @"\s+", "");

            if (remainder.Length > 0)
            {
                errorMessage = $"Invalid expression near '{remainder}'.";
                return false;
            }

            for (int m = 0; m < matches.Count; m++)
            {
                Match match = matches[m];
                string sign = match.Groups["sign"].Value;
                string coefficientText = match.Groups["coef"].Value.Trim();
                string variable = match.Groups["var"].Value;

                if (m > 0 && string.IsNullOrEmpty(sign))
                {
                    errorMessage = $"Missing '+' or '-' sign before '{match.Value.Trim()}'.";
                    return false;
                }

                Fraction coefficient;

                if (coefficientText.Length == 0)
                {
                    coefficient = Fraction.One;
                }
                else if (!Fraction.TryParseCoefficient(
                    coefficientText, out coefficient))
                {
                    errorMessage =
                        $"Invalid coefficient '{coefficientText}' for variable {variable}.";
                    return false;
                }

                if (sign == "-")
                    coefficient = new Fraction(-1, 1) * coefficient;

                if (coefficients.ContainsKey(variable))
                {
                    coefficients[variable] =
                        coefficients[variable] + coefficient;
                }
                else
                {
                    coefficients[variable] = coefficient;
                }
            }

            variableNames = coefficients.Keys
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            rowValues = new Fraction[variableNames.Length + 1];

            for (int i = 0; i < variableNames.Length; i++)
                rowValues[i] = coefficients[variableNames[i]];

            rowValues[rowValues.Length - 1] = constant;

            return true;
        }
    }
}
