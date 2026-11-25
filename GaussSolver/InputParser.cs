#nullable disable
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
using Tesseract;

namespace GaussSolver
{
    public static class InputParser
    {
        private sealed class OcrVariant
        {
            public string Label { get; set; }
            public string Path { get; set; }
        }

        private const string OcrWhitelist =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ+-=/*().,";

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
                .Replace('×', '*')
                .Replace('÷', '/');

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
                @"(?<sign>[+-]?)\s*(?<coef>(?:\d+\s*/\s*\d+|\d+(?:\.\d+)?|\.\d+)?)\s*\*?\s*(?<var>[a-zA-Z])(?:\s*/\s*(?<denom>\d+(?:\.\d+)?|\.\d+))?";

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
                string variable = match.Groups["var"].Value.ToLowerInvariant();
                string denomText = match.Groups["denom"].Value.Trim();

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

                if (denomText.Length > 0)
                {
                    if (!Fraction.TryParse(denomText, out Fraction denomVal) || denomVal.IsZero)
                    {
                        errorMessage = $"Invalid denominator '/{denomText}' for variable {variable}.";
                        return false;
                    }
                    coefficient = coefficient / denomVal;
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

        private static List<OcrVariant> BuildOcrVariants(string originalPath)
        {
            var variants = new List<OcrVariant>();

            using (Bitmap src = new Bitmap(originalPath))
            {
                int fullW = src.Width;
                int fullH = src.Height;
                int[,] rawLum = new int[fullW, fullH];
                int[,] rawMinRgb = new int[fullW, fullH];
                int[,] rawMaxRgb = new int[fullW, fullH];

                for (int y = 0; y < fullH; y++)
                {
                    for (int x = 0; x < fullW; x++)
                    {
                        Color c = src.GetPixel(x, y);
                        rawLum[x, y] = (int)(c.R * 0.299 + c.G * 0.587 + c.B * 0.114);
                        rawMinRgb[x, y] = Math.Min(c.R, Math.Min(c.G, c.B));
                        rawMaxRgb[x, y] = Math.Max(c.R, Math.Max(c.G, c.B));
                    }
                }

                // 1) قص أي حواف سوداء كبيرة (مثل سكرين شوت الموبايل)
                int top = 0, bottom = fullH - 1, left = 0, right = fullW - 1;
                while (top < bottom && GetRowAverage(rawLum, fullW, top) < 45) top++;
                while (bottom > top && GetRowAverage(rawLum, fullW, bottom) < 45) bottom--;
                while (left < right && GetColAverage(rawLum, fullH, left) < 45) left++;
                while (right > left && GetColAverage(rawLum, fullH, right) < 45) right--;

                if (bottom - top < 15 || right - left < 15)
                {
                    top = 0; bottom = fullH - 1; left = 0; right = fullW - 1;
                }

                int w = right - left + 1;
                int h = bottom - top + 1;
                int[,] lum = new int[w, h];
                int[,] minRgb = new int[w, h];

                long sumLum = 0;
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int l = rawLum[left + x, top + y];
                        lum[x, y] = l;
                        minRgb[x, y] = rawMinRgb[left + x, top + y];
                        sumLum += l;
                    }
                }

                bool isDarkBackground = (sumLum / Math.Max(1, w * h)) < 128;
                if (isDarkBackground)
                {
                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            lum[x, y] = 255 - lum[x, y];
                            minRgb[x, y] = 255 - rawMaxRgb[left + x, top + y];
                        }
                    }
                }

                // 2) حساب متوسط ارتفاع السطر الواحد (Single Line Height)
                List<int> lineHeights = new List<int>();
                int bandStart = -1;
                for (int y = 0; y < h; y++)
                {
                    bool hasInk = CountDarkInRow(minRgb, w, y, 175) >= 2;
                    if (hasInk && bandStart == -1)
                    {
                        bandStart = y;
                    }
                    else if (!hasInk && bandStart != -1)
                    {
                        if (y - bandStart >= 5)
                            lineHeights.Add(y - bandStart);
                        bandStart = -1;
                    }
                }
                if (bandStart != -1 && h - bandStart >= 5)
                    lineHeights.Add(h - bandStart);

                double avgLineHeight = lineHeights.Count > 0 ? lineHeights.Average() : 30.0;

                // 3) قص الهوامش البيضاء الفارغة مع ترك هامش 24 بكسل حتى لا تُقص إشارة السالب '-' في أول السطر
                int inkTop = 0, inkBottom = h - 1, inkLeft = 0, inkRight = w - 1;
                while (inkTop < inkBottom && CountDarkInRow(minRgb, w, inkTop, 180) < 2) inkTop++;
                while (inkBottom > inkTop && CountDarkInRow(minRgb, w, inkBottom, 180) < 2) inkBottom--;
                while (inkLeft < inkRight && CountDarkInCol(minRgb, h, inkLeft, 180) < 1) inkLeft++;
                while (inkRight > inkLeft && CountDarkInCol(minRgb, h, inkRight, 180) < 1) inkRight--;

                if (inkBottom - inkTop >= 12 && inkRight - inkLeft >= 12)
                {
                    inkTop = Math.Max(0, inkTop - 20);
                    inkBottom = Math.Min(h - 1, inkBottom + 20);
                    inkLeft = Math.Max(0, inkLeft - 24);
                    inkRight = Math.Min(w - 1, inkRight + 24);

                    int nw = inkRight - inkLeft + 1;
                    int nh = inkBottom - inkTop + 1;
                    int[,] tightLum = new int[nw, nh];
                    int[,] tightMin = new int[nw, nh];

                    for (int y = 0; y < nh; y++)
                    {
                        for (int x = 0; x < nw; x++)
                        {
                            tightLum[x, y] = lum[inkLeft + x, inkTop + y];
                            tightMin[x, y] = minRgb[inkLeft + x, inkTop + y];
                        }
                    }

                    lum = tightLum;
                    minRgb = tightMin;
                    w = nw;
                    h = nh;
                }

                // 4) ضبط ارتفاع السطر الواحد ليكون ~34 بكسل (المقاس المثالي لمحرك Tesseract)
                double idealScale = 34.0 / Math.Max(1.0, avgLineHeight);
                if (idealScale > 3.2) idealScale = 3.2;
                if (idealScale < 0.65) idealScale = 0.65;

                variants.Add(new OcrVariant
                {
                    Label = "ideal_lum_gray",
                    Path = SaveProcessedVariant(lum, w, h, idealScale, mode: 0, threshold: 0)
                });

                variants.Add(new OcrVariant
                {
                    Label = "ideal_minrgb_gray",
                    Path = SaveProcessedVariant(minRgb, w, h, idealScale, mode: 0, threshold: 0)
                });

                variants.Add(new OcrVariant
                {
                    Label = "ideal_minrgb_t145",
                    Path = SaveProcessedVariant(minRgb, w, h, idealScale, mode: 2, threshold: 145)
                });

                variants.Add(new OcrVariant
                {
                    Label = "ideal_lum_t160",
                    Path = SaveProcessedVariant(lum, w, h, idealScale, mode: 2, threshold: 160)
                });
            }

            return variants;
        }

        private static int GetRowAverage(int[,] mat, int width, int y)
        {
            long sum = 0;
            for (int x = 0; x < width; x++) sum += mat[x, y];
            return (int)(sum / Math.Max(1, width));
        }

        private static int GetColAverage(int[,] mat, int height, int x)
        {
            long sum = 0;
            for (int y = 0; y < height; y++) sum += mat[x, y];
            return (int)(sum / Math.Max(1, height));
        }

        private static int CountDarkInRow(int[,] mat, int width, int y, int thresh)
        {
            int c = 0;
            for (int x = 0; x < width; x++) if (mat[x, y] < thresh) c++;
            return c;
        }

        private static int CountDarkInCol(int[,] mat, int height, int x, int thresh)
        {
            int c = 0;
            for (int y = 0; y < height; y++) if (mat[x, y] < thresh) c++;
            return c;
        }

        private static string SaveProcessedVariant(int[,] lum, int w, int h, double scale, int mode, int threshold)
        {
            using (Bitmap baseBmp = new Bitmap(w, h, PixelFormat.Format24bppRgb))
            {
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        int l = lum[x, y];
                        int v = l;
                        if (mode == 1)
                        {
                            if (l > 215) v = 255;
                            else if (l < 95) v = 0;
                            else v = (int)((l - 95) * 255.0 / (215 - 95));
                        }
                        baseBmp.SetPixel(x, y, Color.FromArgb(v, v, v));
                    }
                }

                int pad = 40;
                int scaledW = Math.Max(1, (int)Math.Round(w * scale));
                int scaledH = Math.Max(1, (int)Math.Round(h * scale));
                int outW = scaledW + pad * 2;
                int outH = scaledH + pad * 2;

                using (Bitmap scaled = new Bitmap(outW, outH, PixelFormat.Format24bppRgb))
                {
                    using (Graphics g = Graphics.FromImage(scaled))
                    {
                        g.Clear(Color.White);
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.DrawImage(baseBmp, new Rectangle(pad, pad, scaledW, scaledH));
                    }

                    if (mode == 2)
                    {
                        for (int y = pad; y < pad + scaledH; y++)
                        {
                            for (int x = pad; x < pad + scaledW; x++)
                            {
                                Color c = scaled.GetPixel(x, y);
                                int bw = c.R < threshold ? 0 : 255;
                                scaled.SetPixel(x, y, Color.FromArgb(bw, bw, bw));
                            }
                        }
                    }

                    string tempPath = Path.Combine(
                        Path.GetTempPath(),
                        $"gauss_tess_{Guid.NewGuid():N}.png");
                    scaled.Save(tempPath, System.Drawing.Imaging.ImageFormat.Png);
                    return tempPath;
                }
            }
        }

        private static long ScoreOcrText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return -1;

            string[] lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
                return -1;

            long score = 0;

            if (TryParseEquations(text, out Matrix eqMat, out _, out string[] vars, out _))
            {
                score += 100000 + (eqMat.Rows * 6000);
                int diff = Math.Abs(vars.Length - eqMat.Rows);
                score -= diff * 4000;

                foreach (string line in lines)
                {
                    if (Regex.IsMatch(line, @"\b0\d+\b") || Regex.IsMatch(line, @"\b0[a-zA-Z]\b"))
                        score -= 8000;
                }
            }
            else if (TryParseMatrix(text, out Matrix mMat, out _))
            {
                score += 80000 + (mMat.Rows * 4000) + (mMat.Columns * 800);
            }

            int validEqLines = 0;
            foreach (string line in lines)
            {
                if (TryParseEquation(line, out Fraction[] vals, out _))
                {
                    validEqLines++;
                    score += 5000 + vals.Length * 300;
                }
                else
                {
                    if (line.Contains("=")) score += 400;
                    score += Regex.Matches(line, @"[a-zA-Z]").Count * 80;
                    score += Regex.Matches(line, @"\d+").Count * 60;
                    score += Regex.Matches(line, @"[+\-/]").Count * 50;
                }
            }

            if (validEqLines == lines.Length && lines.Length >= 2)
                score += 25000;

            return score;
        }

        private static string ResolveTessDataFolder()
        {
            string[] candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "tessdata"),
                Path.Combine(Environment.CurrentDirectory, "tessdata"),
                @"C:\Program Files\Tesseract-OCR\tessdata",
                @"C:\Program Files (x86)\Tesseract-OCR\tessdata"
            };

            foreach (string folder in candidates)
            {
                if (File.Exists(Path.Combine(folder, "eng.traineddata")))
                    return folder;
            }

            return Path.Combine(AppContext.BaseDirectory, "tessdata");
        }

        private static string ExtractTextFromImageWindowsOcr(string imagePath)
        {
            string tessData = ResolveTessDataFolder();
            if (!File.Exists(Path.Combine(tessData, "eng.traineddata")))
                throw new FileNotFoundException(
                    "Missing 'tessdata\\eng.traineddata' next to the program.\n" +
                    "Expected folder: " + tessData);

            List<OcrVariant> variants = BuildOcrVariants(imagePath);
            string debugPath = Path.Combine(Path.GetTempPath(), "gauss_ocr_debug.txt");
            string lastImagePath = Path.Combine(Path.GetTempPath(), "gauss_ocr_last.png");
            var debug = new StringBuilder();

            string bestText = "";
            long bestScore = -1;
            string bestPath = null;

            try
            {
                using (var engine = new TesseractEngine(tessData, "eng", EngineMode.LstmOnly))
                {
                    var configs = new[]
                    {
                        new { Name = "whitelist+block", Whitelist = OcrWhitelist, Psm = PageSegMode.SingleBlock },
                        new { Name = "free+auto",       Whitelist = "",           Psm = PageSegMode.Auto }
                    };

                    foreach (OcrVariant v in variants)
                    {
                        using (Pix pix = Pix.LoadFromFile(v.Path))
                        {
                            foreach (var cfg in configs)
                            {
                                engine.SetVariable("tessedit_char_whitelist", cfg.Whitelist);
                                engine.DefaultPageSegMode = cfg.Psm;

                                string text;
                                using (Page page = engine.Process(pix))
                                    text = SanitizeOcrText(page.GetText());

                                long score = ScoreOcrText(text);

                                debug.AppendLine($"=== {v.Label} | {cfg.Name} | score={score}");
                                debug.AppendLine(text);
                                debug.AppendLine();

                                if (score > bestScore)
                                {
                                    bestScore = score;
                                    bestText = text;
                                    bestPath = v.Path;
                                }
                            }
                        }
                    }
                }

                debug.AppendLine("BEST score = " + bestScore);
                try { File.WriteAllText(debugPath, debug.ToString(), Encoding.UTF8); } catch { }
                if (bestPath != null)
                {
                    try { File.Copy(bestPath, lastImagePath, true); } catch { }
                }

                return bestText;
            }
            finally
            {
                foreach (OcrVariant v in variants)
                {
                    try { if (File.Exists(v.Path)) File.Delete(v.Path); } catch { }
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
                .Replace('×', '*')
                .Replace('÷', '/');

            string[] lines = cleaned.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            List<string> validLines = new List<string>();

            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                trimmed = Regex.Replace(trimmed, @"\s*/\s*", "/");

                if (!trimmed.Contains("=") && (trimmed.Contains(":") || trimmed.Contains("?")))
                {
                    trimmed = Regex.Replace(trimmed, @"[\?:]+", "=");
                }

                trimmed = Regex.Replace(trimmed, @"(\d)7[zZ]\b", "$1z");
                trimmed = Regex.Replace(trimmed, @"([a-zA-Z])[247]\b", "$1");
                trimmed = Regex.Replace(trimmed, @"[xX]{2,}", "x");
                trimmed = Regex.Replace(trimmed, @"[yY]{2,}", "y");
                trimmed = Regex.Replace(trimmed, @"[zZ]{2,}", "z");
                trimmed = Regex.Replace(trimmed, @"[wW]{2,}", "w");
                trimmed = Regex.Replace(trimmed, @"\b([a-zA-Z])e\s*=", "$1 =");

                if (Regex.IsMatch(trimmed, @"\bx\b", RegexOptions.IgnoreCase) &&
                    Regex.IsMatch(trimmed, @"\by\b", RegexOptions.IgnoreCase) &&
                    !Regex.IsMatch(trimmed, @"\bz\b", RegexOptions.IgnoreCase))
                {
                    trimmed = Regex.Replace(trimmed, @"([+-]\s*)2(/\d+)", "$1z$2");
                }

                trimmed = Regex.Replace(trimmed, @"\s*([+=])\s*", " $1 ");
                trimmed = Regex.Replace(trimmed, @"(?<=[a-zA-Z0-9])\s*-\s*", " - ");

                if (!trimmed.Contains("="))
                {
                    trimmed = Regex.Replace(
                        trimmed,
                        @"([a-zA-Z]\w*(?:/\d+(?:\.\d+)?)?)\s+([+-]?\d+(?:/\d+|\.\d+)?)\s*$",
                        "$1 = $2");
                }

                validLines.Add(trimmed.Trim());
            }

            return string.Join(Environment.NewLine, validLines);
        }
    }
}
