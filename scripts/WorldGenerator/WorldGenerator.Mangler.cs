using System;

public partial class WorldGenerator
{
    class Mangler
    {
        public void MangleChunk(Chunk chunk)
        {
            Grid grid = chunk.Grid;
            for (int x = 0; x < ChunkWidth; x++)
            {
                for (int y = 0; y < ChunkHeight; y++)
                {
                    grid.Cell(x, y) = Math.Max(
                        grid.Cell(x, y),
                        ManglerNoise.GetNoise2D(x, y) * ManglerStrengthMult.Value
                    );
                }
            }
        }
    }
}
