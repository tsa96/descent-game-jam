using System;
using Chunk = float[,];

public partial class WorldGenerator
{
	class Mangler
	{
		public void MangleChunk(Chunk chunk, int chunkDepth)
		{
			for (int x = 0; x < ChunkWidth; x++)
			for (int y = 0; y < ChunkHeight; y++)
			{
				chunk[x, y] = Math.Max(
					chunk[x, y],
					ManglerNoise.GetNoise2D(x * 2, ChunkHeight * chunkDepth + y) * ManglerStrengthMult.Value
				);
			}
		}
	}
}
