using Cells = float[,];

public partial class WorldGenerator
{
	// Based on https://www.cs.cmu.edu/~112-s23/notes/student-tp-guides/Terrain.pdf Example 7
	class Muncher
	{
		// No reason for this not to be static or even for this class to exist. But it's funny.
		public void EatChunk(Cells cells)
		{
			Cells buffer = new float[ChunkWidth, ChunkBigHeight];
			for (int i = 0; i < MuncherIters.Value; i++)
			{
				for (int x = 0; x < ChunkWidth; x++)
				for (int y = 0; y < ChunkBigHeight; y++)
				{
					int openNeighbours = 0;
					for (int j = 0; j < 8; j++)
					{
						if (HasOpenNeighbour(cells, x, y, (Direction)j))
							openNeighbours++;
					}

					float current = cells[x, y];
					if (current >= AirThreshold.Value)
					{
						buffer[x, y] = openNeighbours >= MuncherAirNeighbours.Value ? 1.0f : 0.0f;
					}
					else
					{
						buffer[x, y] = openNeighbours >= MuncherRockNeighbours.Value ? 1.0f : 0.0f;
					}
				}

				// Fast pointer swap. We always write to the buffer,
				// which always become new Grid, so this is all we need.
				(cells, buffer) = (buffer, cells);
			}
		}
	}
}
