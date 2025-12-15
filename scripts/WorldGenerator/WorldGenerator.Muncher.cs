public partial class WorldGenerator
{
    // Based on https://www.cs.cmu.edu/~112-s23/notes/student-tp-guides/Terrain.pdf Example 7
    class Muncher
    {
        // No reason for this not to be static or even for this class to exist. But it's funny.
        public void EatChunk(Chunk chunk)
        {
            for (int i = 0; i < MuncherIters.Value; i++)
            {
                for (int x = 0; x < ChunkWidth; x++)
                {
                    for (int y = 0; y < ChunkHeight; y++)
                    {
                        int openNeighbours = 0;
                        foreach (float strength in chunk.Grid.ItersNeighbours(x, y))
                        {
                            if (strength > AirThresold.Value)
                                openNeighbours++;
                        }

                        float current = chunk.Grid.Cell(x, y);
                        if (current >= AirThresold.Value)
                        {
                            chunk.GridBuffer.Cell(x, y) = openNeighbours >= MuncherAirNeighbours.Value ? 1 : 0;
                        }
                        else
                        {
                            chunk.GridBuffer.Cell(x, y) = openNeighbours >= MuncherRockNeighbours.Value ? 1 : 0;
                        }
                    }
                }

                // Just ptr swap
                (chunk.Grid, chunk.GridBuffer) = (chunk.GridBuffer, chunk.Grid);
            }
        }
    }
}
