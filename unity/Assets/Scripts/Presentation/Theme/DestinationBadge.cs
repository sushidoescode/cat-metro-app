using CatMetro.Content;
using CatMetro.Domain;

namespace CatMetro.Presentation.Theme
{
    // Import normalizes omitted shapes to round. Boards with independent shape matching
    // use that channel for every rider and berth; legacy boards keep the line vocabulary.
    public static class DestinationBadge
    {
        public static bool UsesShapes(LevelDto level)
        {
            foreach (var mechanic in level.Meta.Mechanics.Span)
                if (mechanic == "shape") return true;
            foreach (var station in level.Stations.Span)
                if (station.Shape == "square" || station.Shape == "triangle") return true;
            foreach (var wave in level.Waves.Span)
                if (wave.Shape == "square" || wave.Shape == "triangle") return true;
            return false;
        }

        public static DestinationShape Resolve(string line, string shape = null)
        {
            if (line == "wild") return DestinationShape.Star;
            switch (shape)
            {
                case "round": return DestinationShape.Circle;
                case "square": return DestinationShape.Square;
                case "triangle": return DestinationShape.Triangle;
                default: return CatLine.ShapeOf(line);
            }
        }

        public static DestinationShape Resolve(byte token, bool usesShapes)
        {
            string line = CatLine.NameOfCode(CatToken.Color(token));
            if (!usesShapes || line == "wild") return CatLine.ShapeOf(line);
            switch (CatToken.Shape(token))
            {
                case CatShape.Square: return DestinationShape.Square;
                case CatShape.Triangle: return DestinationShape.Triangle;
                default: return DestinationShape.Circle;
            }
        }
    }
}
