using System.Collections.Generic;

public partial class WorldGenerator
{
    class Grid
    {
        float[,] _cells = new float[ChunkWidth, ChunkHeight];

        public ref float Cell(int x, int y) => ref _cells[x, y];

        static readonly sbyte[] OffsetX = [-1, 0, 1, -1, 1, -1, 0, 1];
        static readonly sbyte[] OffsetY = [-1, -1, -1, 0, 0, 1, 1, 1];

        public IEnumerable<float> ItersNeighbours(int x, int y)
        {
            for (int i = 0; i < 8; i++)
            {
                int neighbourX = x + OffsetX[i];
                int neighbourY = y + OffsetY[i];

                if (neighbourX < 0 || neighbourX >= ChunkWidth || neighbourY < 0 || neighbourY >= ChunkHeight)
                    continue;

                yield return _cells[neighbourX, neighbourY];
            }
        }
    }

    class Chunk
    {
        // TODO: Floats might be too slow but don't want to fuck around with bytes. See if perf is affected first!

        // 2D array, width first
        public Grid Grid { get; set; } = new Grid();

        // Buffer for writing as we read from _grid during muncher cycles
        public Grid GridBuffer { get; set; } = new Grid();
    }
}
