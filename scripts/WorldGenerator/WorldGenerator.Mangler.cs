using System;
using Cells = float[,];

public partial class WorldGenerator
{
	class Mangler
	{
		public void MangleChunk(Cells cells)
		{
			for (int x = 0; x < ChunkWidth; x++)
			for (int y = 0; y < ChunkHeight; y++)
			{
				cells[x, y] = Math.Max(cells[x, y], ManglerNoise.GetNoise2D(x * 2, y) * ManglerStrengthMult.Value);
			}
		}
	}
}
