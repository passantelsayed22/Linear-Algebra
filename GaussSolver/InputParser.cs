using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
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
            matrix = null;
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
            matrix = null;
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
                        variableNames[c], out Fraction coefficient)
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
                alignedRowValues[c] = coeffMap.TryGetValue(expectedVariables[c], out Fraction val)
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

        public static bool TryExtractTextFromFile(string filePath, out string extractedText, out string error)
        {
            extractedText = "";
            error = "";

            if (!File.Exists(filePath))
            {
                error = "File does not exist.";
                return false;
            }

            string ext = Path.GetExtension(filePath).ToLowerInvariant();

            try
            {
                if (ext == ".txt" || ext == ".csv")
                {
                    extractedText = File.ReadAllText(filePath, Encoding.UTF8).Trim();
                    return !string.IsNullOrWhiteSpace(extractedText);
                }
                else if (ext == ".pdf")
                {
                    extractedText = ExtractTextFromPdf(filePath);
                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        error = "Could not extract readable text from this PDF.";
                        return false;
                    }
                    return true;
                }
                else if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp")
                {
                    extractedText = ExtractTextFromImageWindowsOcr(filePath);
                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        error = "No readable text or equations were detected in the image.";
                        return false;
                    }
                    return true;
                }
                else
                {
                    error = "Unsupported file format.";
                    return false;
                }
            }
            catch (Exception ex)
            {
                error = $"Failed to read file: {ex.Message}";
                return false;
            }
        }

        private static string PreprocessImageForOcr(string originalPath)
        {
            using (Bitmap src = new Bitmap(originalPath))
            {
                int scale = 3;
                int pad = 40;
                int newW = src.Width * scale + pad * 2;
                int newH = src.Height * scale + pad * 2;

                long totalBrightness = 0;
                int sampleCount = 0;
                for (int y = 0; y < src.Height; y += Math.Max(1, src.Height / 20))
                {
                    for (int x = 0; x < src.Width; x += Math.Max(1, src.Width / 20))
                    {
                        Color c = src.GetPixel(x, y);
                        totalBrightness += (c.R + c.G + c.B) / 3;
                        sampleCount++;
                    }
                }
                bool isDarkBackground = (sampleCount > 0) && ((totalBrightness / sampleCount) < 128);

                using (Bitmap scaled = new Bitmap(newW, newH, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;

                        if (isDarkBackground)
                        {
                            using (Bitmap inverted = new Bitmap(src.Width, src.Height))
                            {
                                for (int y = 0; y < src.Height; y++)
                                {
                                    for (int x = 0; x < src.Width; x++)
                                    {
                                        Color p = src.GetPixel(x, y);
                                        int lum = (int)(p.R * 0.3 + p.G * 0.59 + p.B * 0.11);
                                        int inv = 255 - lum;
                                        int bw = inv < 150 ? 0 : 255;
                                        inverted.SetPixel(x, y, Color.FromArgb(bw, bw, bw));
                                    }
                                }
                                g.DrawImage(inverted, new Rectangle(pad, pad, src.Width * scale, src.Height * scale));
                            }
                        }
                        else
                        {
                            using (Bitmap highContrast = new Bitmap(src.Width, src.Height))
                            {
                                for (int y = 0; y < src.Height; y++)
                                {
                                    for (int x = 0; x < src.Width; x++)
                                    {
                                        Color p = src.GetPixel(x, y);
                                        int lum = (int)(p.R * 0.3 + p.G * 0.59 + p.B * 0.11);
                                        int bw = lum < 165 ? 0 : 255;
                                        highContrast.SetPixel(x, y, Color.FromArgb(bw, bw, bw));
                                    }
                                }
                                g.DrawImage(highContrast, new Rectangle(pad, pad, src.Width * scale, src.Height * scale));
                            }
                        }
                    }

                    string tempPath = Path.Combine(Path.GetTempPath(), "gauss_ocr_" + Guid.NewGuid().ToString("N") + ".png");
                    scaled.Save(tempPath, ImageFormat.Png);
                    return tempPath;
                }
            }
        }

        private static string ExtractTextFromImageWindowsOcr(string imagePath)
        {
            string processedPath = PreprocessImageForOcr(imagePath);
            try
            {
                string escapedPath = processedPath.Replace("'", "''");
                string psScript = $@"
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Storage.StorageFile, Windows.Storage, ContentType=WindowsRuntime]
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Foundation, ContentType=WindowsRuntime]
$null = [Windows.Globalization.Language, Windows.Foundation, ContentType=WindowsRuntime]

$asTaskGeneric = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {{ $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' }})[0]
Function Await($WinRtTask, $ResultType) {{
    $asTask = $asTaskGeneric.MakeGenericMethod($ResultType)
    $netTask = $asTask.Invoke($null, @($WinRtTask))
    $netTask.Wait(-1) | Out-Null
    $netTask.Result
}}

$file = Await ([Windows.Storage.StorageFile]::GetFileFromPathAsync('{escapedPath}')) ([Windows.Storage.StorageFile])
$stream = Await ($file.OpenAsync([Windows.Storage.FileAccessMode]::Read)) ([Windows.Storage.Streams.IRandomAccessStream])
$decoder = Await ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
$bitmap = Await ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])

$lang = New-Object Windows.Globalization.Language('en-US')
$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage($lang)
if ($null -eq $engine) {{
    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
}}
$result = Await ($engine.RecognizeAsync($bitmap)) ([Windows.Media.Ocr.OcrResult])
foreach ($line in $result.Lines) {{
    $words = ($line.Words | Sort-Object {{ $_.BoundingRect.X }} | ForEach-Object {{ $_.Text }}) -join ' '
    Write-Output $words
}}
";
                string base64Script = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -EncodedCommand {base64Script}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (Process proc = Process.Start(psi))
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(15000);
                    return SanitizeOcrText(output);
                }
            }
            finally
            {
                if (File.Exists(processedPath))
                {
                    try { File.Delete(processedPath); } catch { }
                }
            }
        }

        private static string ExtractTextFromPdf(string pdfPath)
        {
            byte[] bytes = File.ReadAllBytes(pdfPath);
            string raw = Encoding.GetEncoding("ISO-8859-1").GetString(bytes);
            StringBuilder fullText = new StringBuilder();

            MatchCollection streamMatches = Regex.Matches(raw, @"stream[\r\n]+(.*?)[\r\n]+endstream", RegexOptions.Singleline);
            foreach (Match m in streamMatches)
            {
                string streamData = m.Groups[1].Value;
                string decoded = TryDecompressPdfStream(streamData);
                string source = !string.IsNullOrEmpty(decoded) ? decoded : streamData;

                MatchCollection textBlocks = Regex.Matches(source, @"\(([^()]+)\)\s*Tj|\[(.*?)\]\s*TJ", RegexOptions.Singleline);
                if (textBlocks.Count > 0)
                {
                    StringBuilder lineBuilder = new StringBuilder();
                    foreach (Match tb in textBlocks)
                    {
                        if (tb.Groups[1].Success)
                        {
                            lineBuilder.Append(tb.Groups[1].Value);
                        }
                        else if (tb.Groups[2].Success)
                        {
                            MatchCollection subParts = Regex.Matches(tb.Groups[2].Value, @"\(([^()]*)\)");
                            foreach (Match sp in subParts)
                                lineBuilder.Append(sp.Groups[1].Value);
                        }
                    }
                    fullText.AppendLine(lineBuilder.ToString());
                }
            }

            string result = fullText.ToString().Trim();
            if (string.IsNullOrWhiteSpace(result))
            {
                MatchCollection directMatches = Regex.Matches(raw, @"\(([^()]+)\)\s*Tj");
                foreach (Match dm in directMatches)
                    fullText.AppendLine(dm.Groups[1].Value);
                result = fullText.ToString().Trim();
            }

            return SanitizeOcrText(result);
        }

        private static string TryDecompressPdfStream(string latin1Stream)
        {
            try
            {
                byte[] data = Encoding.GetEncoding("ISO-8859-1").GetBytes(latin1Stream);
                if (data.Length <= 2) return "";
                using (MemoryStream ms = new MemoryStream(data, 2, data.Length - 2))
                using (DeflateStream ds = new DeflateStream(ms, CompressionMode.Decompress))
                using (StreamReader sr = new StreamReader(ds, Encoding.UTF8))
                {
                    return sr.ReadToEnd();
                }
            }
            catch
            {
                return "";
            }
        }

        public static string SanitizeOcrText(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "";

            string cleaned = raw
                .Replace('\u2212', '-')
                .Replace('\u2013', '-')
                .Replace('\u2014', '-')
                .Replace('~', '-')
                .Replace('×', '*');

            string[] lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> validLines = new List<string>();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (!trimmed.Contains("=") && (trimmed.Contains(":") || trimmed.Contains("?")))
                {
                    trimmed = Regex.Replace(trimmed, @"[\?:]+", "=");
                }

                validLines.Add(trimmed);
            }

            return string.Join(Environment.NewLine, validLines);
        }
    }
}