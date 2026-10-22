using UnityEngine;
using UnityEngine.UIElements;

namespace HeritageXR
{
    public enum MuseumIconKind { Settings, Back, Discover, Collection, Scan, Check, Arrow }

    public sealed class MuseumIcon : VisualElement
    {
        readonly MuseumIconKind kind;
        Color color = new Color(.13f, .22f, .20f);
        bool mirrored;
        public Color Color { set { color = value; MarkDirtyRepaint(); } }
        public bool Mirrored { set { mirrored = value; MarkDirtyRepaint(); } }
        public MuseumIcon(MuseumIconKind kind)
        {
            this.kind = kind;
            pickingMode = PickingMode.Ignore;
            AddToClassList("icon");
            generateVisualContent += Draw;
        }
        void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            painter.strokeColor = color;
            painter.lineWidth = 1.8f;
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;
            float size = Mathf.Min(contentRect.width, contentRect.height);
            Vector2 P(float x, float y) => new Vector2((mirrored ? 24 - x : x) * size / 24f, y * size / 24f);
            void Path(params Vector2[] points)
            {
                painter.BeginPath(); painter.MoveTo(points[0]);
                for (int i = 1; i < points.Length; i++) painter.LineTo(points[i]);
                painter.Stroke();
            }
            void Circle(float radius)
            {
                var points = new Vector2[33];
                for (int i = 0; i <= 32; i++) { float a = i * Mathf.PI / 16; points[i] = P(12 + Mathf.Cos(a) * radius, 12 + Mathf.Sin(a) * radius); }
                Path(points);
            }
            switch (kind)
            {
                case MuseumIconKind.Settings:
                    var gear = new Vector2[33];
                    for (int i = 0; i <= 32; i++)
                    {
                        float a = i * Mathf.PI / 16;
                        float radius = i % 4 == 0 || i % 4 == 3 ? 8.6f : 6.8f;
                        gear[i] = P(12 + Mathf.Cos(a) * radius, 12 + Mathf.Sin(a) * radius);
                    }
                    Path(gear); Circle(2.7f); break;
                case MuseumIconKind.Back: Path(P(14, 5), P(7, 12), P(14, 19)); Path(P(7, 12), P(20, 12)); break;
                case MuseumIconKind.Arrow: Path(P(5, 12), P(19, 12)); Path(P(13, 6), P(19, 12), P(13, 18)); break;
                case MuseumIconKind.Check: Path(P(5, 12), P(10, 17), P(19, 7)); break;
                case MuseumIconKind.Discover: Path(P(3, 10), P(12, 3), P(21, 10)); Path(P(6, 9), P(6, 21), P(18, 21), P(18, 9)); break;
                case MuseumIconKind.Collection:
                    Path(P(3, 7), P(21, 7), P(12, 2), P(3, 7)); Path(P(3, 21), P(21, 21));
                    for (int i = 6; i <= 18; i += 6) Path(P(i, 10), P(i, 18)); break;
                case MuseumIconKind.Scan:
                    Path(P(3, 9), P(3, 3), P(9, 3)); Path(P(15, 3), P(21, 3), P(21, 9));
                    Path(P(3, 15), P(3, 21), P(9, 21)); Path(P(15, 21), P(21, 21), P(21, 15));
                    Path(P(7, 12), P(17, 12)); break;
            }
        }
    }
}
