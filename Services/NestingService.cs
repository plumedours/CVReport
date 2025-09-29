using System;
using System.Collections.Generic;
using System.Linq;

namespace CVReport.Services
{
    public static class NestingService
    {
        public sealed class Piece
        {
            public int Id;
            public string Label = "";
            public double Width;
            public double Height;
            public int Quantity;
            public Piece(double w, double h, int q, int id = 0, string label = "")
            {
                Width = w;
                Height = h;
                Quantity = Math.Max(0, q);
                Id = id;
                Label = label ?? "";
            }
        }

        public sealed class MaterialInput
        {
            public int MaterialId;
            public string Name = "";
            public string Description = "";
            public double SheetWidth;
            public double SheetHeight;
            public List<Piece> Pieces = new();
            public double WastePercent;
            public bool allowrotate = false;
        }

        public sealed class MaterialResult
        {
            public int MaterialId;
            public string Name = "";
            public string Description = "";
            public int SheetsUsed;
            public double TotalFt2;
            public double SheetFt2;
            public double Utilization;
            public bool allowrotate = false;
        }

        // === Wrapper historique : garde l’API existante ===
        public static MaterialResult ComputeSheetsForMaterial(MaterialInput material)
        {
            // valeurs par défaut identiques à l’ancienne implémentation
            return ComputeSheetsForMaterial(material, allowRotate: true, kerf: 0.625, edgeMargin: 0.5);
        }

        // === Surcharge paramétrable (utilisée par Soumission) ===
        public static MaterialResult ComputeSheetsForMaterial(MaterialInput material, bool allowRotate, double kerf, double edgeMargin)
        {
            if (material.SheetWidth <= 0 || material.SheetHeight <= 0)
            {
                return new MaterialResult
                {
                    MaterialId = material.MaterialId,
                    Name = material.Name,
                    Description = material.Description,
                    SheetsUsed = 0,
                    TotalFt2 = 0,
                    SheetFt2 = 0,
                    Utilization = 0,
                    allowrotate = allowRotate
                };
            }

            var pieces = material.Pieces
                .Where(p => p.Quantity > 0 && p.Width > 0 && p.Height > 0)
                .Select(p => new Piece(p.Width, p.Height, p.Quantity))
                .ToList();

            static double AreaIn2(double w, double h) => w * h;

            var totalInches2 = pieces.Sum(p => p.Quantity * AreaIn2(p.Width, p.Height));
            var sheetInches2 = AreaIn2(material.SheetWidth, material.SheetHeight);

            var totalFt2 = totalInches2 / 144.0;
            var sheetFt2 = sheetInches2 / 144.0;

            // zone utilisable = feuille - marges périphériques
            var usableW = Math.Max(0, material.SheetWidth - 2 * edgeMargin);
            var usableH = Math.Max(0, material.SheetHeight - 2 * edgeMargin);

            var remaining = pieces.ToList();
            int sheetsUsed = 0;

            while (remaining.Any(p => p.Quantity > 0))
            {
                var packer = new MaxRectsBinPack(usableW, usableH);
                bool placedSomething = false;

                var ordered = remaining
                    .Where(p => p.Quantity > 0)
                    .OrderByDescending(p => Math.Max(p.Width, p.Height))
                    .ThenByDescending(p => p.Quantity)
                    .ToList();

                foreach (var piece in ordered)
                {
                    int tries = piece.Quantity;
                    while (tries-- > 0)
                    {
                        var placed = packer.TryInsert(piece.Width, piece.Height, allowRotate, kerf);
                        if (placed) { piece.Quantity--; placedSomething = true; }
                        else break;
                    }
                }

                if (!placedSomething) break; // rien ne rentre → on arrête
                sheetsUsed++;
                remaining = remaining.Where(p => p.Quantity > 0).ToList();
            }

            var utilization = sheetsUsed > 0 && sheetInches2 > 0
                ? Math.Min(1.0, totalInches2 / (sheetsUsed * sheetInches2))
                : 0.0;

            return new MaterialResult
            {
                MaterialId = material.MaterialId,
                Name = material.Name,
                Description = material.Description,
                SheetsUsed = sheetsUsed,
                TotalFt2 = Math.Round(totalFt2, 2),
                SheetFt2 = Math.Round(sheetFt2, 3),
                Utilization = utilization,
                allowrotate = allowRotate
            };
        }

        // === Méthode attendue par Main.Soumission.cs ===
        public static IEnumerable<MaterialResult> ComputePerMaterial(
            IEnumerable<MaterialInput> inputs, bool allowRotate, double kerf, double edgeMargin)
        {
            foreach (var m in inputs)
                yield return ComputeSheetsForMaterial(m, allowRotate, kerf, edgeMargin);
        }

        // ---------- Implémentation MaxRects simplifiée ----------
        private sealed class MaxRectsBinPack
        {
            public double BinWidth { get; }
            public double BinHeight { get; }
            private readonly List<Rect> _used = new();
            private readonly List<Rect> _free = new();

            public MaxRectsBinPack(double width, double height)
            {
                BinWidth = width; BinHeight = height;
                _free.Add(new Rect(0, 0, width, height));
            }

            public bool TryInsert(double w, double h, bool allowRotate, double kerf)
            {
                double actualW = w + kerf;
                double actualH = h + kerf;

                int bestIndex = -1;
                bool bestRot = false;
                double bestShort = double.MaxValue;
                double bestLong = double.MaxValue;

                for (int i = 0; i < _free.Count; i++)
                {
                    var r = _free[i];

                    // orientation normale
                    if (r.W >= actualW && r.H >= actualH)
                    {
                        double sh = Math.Min(r.H - actualH, r.W - actualW);
                        double lo = Math.Max(r.H - actualH, r.W - actualW);
                        if (sh < bestShort || (sh == bestShort && lo < bestLong))
                        { bestIndex = i; bestRot = false; bestShort = sh; bestLong = lo; }
                    }

                    // rotation
                    if (allowRotate && r.W >= actualH && r.H >= actualW)
                    {
                        double sh = Math.Min(r.H - actualW, r.W - actualH);
                        double lo = Math.Max(r.H - actualW, r.W - actualH);
                        if (sh < bestShort || (sh == bestShort && lo < bestLong))
                        { bestIndex = i; bestRot = true; bestShort = sh; bestLong = lo; }
                    }
                }

                if (bestIndex == -1) return false;

                var freeNode = _free[bestIndex];
                var usedNode = new Rect(freeNode.X, freeNode.Y,
                                        bestRot ? actualH : actualW,
                                        bestRot ? actualW : actualH);

                var snapshot = _free.ToList();
                foreach (var f in snapshot)
                    SplitFreeNode(f, usedNode);

                PruneFreeList();
                _used.Add(usedNode);
                return true;
            }

            private bool SplitFreeNode(Rect freeNode, Rect usedNode)
            {
                if (usedNode.X >= freeNode.X + freeNode.W || usedNode.X + usedNode.W <= freeNode.X ||
                    usedNode.Y >= freeNode.Y + freeNode.H || usedNode.Y + usedNode.H <= freeNode.Y)
                    return false;

                if (usedNode.X < freeNode.X + freeNode.W && usedNode.X + usedNode.W > freeNode.X)
                {
                    if (usedNode.Y > freeNode.Y && usedNode.Y < freeNode.Y + freeNode.H)
                        _free.Add(new Rect(freeNode.X, freeNode.Y, freeNode.W, usedNode.Y - freeNode.Y));

                    if (usedNode.Y + usedNode.H < freeNode.Y + freeNode.H)
                        _free.Add(new Rect(freeNode.X, usedNode.Y + usedNode.H, freeNode.W,
                                           freeNode.Y + freeNode.H - (usedNode.Y + usedNode.H)));
                }

                if (usedNode.Y < freeNode.Y + freeNode.H && usedNode.Y + usedNode.H > freeNode.Y)
                {
                    if (usedNode.X > freeNode.X && usedNode.X < freeNode.X + freeNode.W)
                        _free.Add(new Rect(freeNode.X, freeNode.Y, usedNode.X - freeNode.X, freeNode.H));

                    if (usedNode.X + usedNode.W < freeNode.X + freeNode.W)
                        _free.Add(new Rect(usedNode.X + usedNode.W, freeNode.Y,
                                           freeNode.X + freeNode.W - (usedNode.X + usedNode.W), freeNode.H));
                }

                _free.Remove(freeNode);
                return true;
            }

            private void PruneFreeList()
            {
                for (int i = 0; i < _free.Count; i++)
                {
                    for (int j = i + 1; j < _free.Count; j++)
                    {
                        if (_free[i].Contains(_free[j])) { _free.RemoveAt(j); j--; }
                        else if (_free[j].Contains(_free[i])) { _free.RemoveAt(i); i--; break; }
                    }
                }
            }

            private sealed class Rect
            {
                public double X, Y, W, H;
                public Rect(double x, double y, double w, double h) { X = x; Y = y; W = w; H = h; }
                public bool Contains(Rect r) =>
                    r.X >= X && r.Y >= Y && r.X + r.W <= X + W && r.Y + r.H <= Y + H;
            }
        }
    }
}