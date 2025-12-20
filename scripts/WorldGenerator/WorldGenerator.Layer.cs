using System.Linq;
using Godot;
using Chunk = float[,];

public partial class WorldGenerator
{
	enum Direction
	{
		Top,
		TopRight,
		Right,
		BottomRight,
		Bottom,
		BottomLeft,
		Left,
		TopLeft
	}

	static readonly Vector2I[] DirectionVectors =
	[
		new(0, -1),
		new(1, -1),
		new(1, 0),
		new(1, 1),
		new(0, 1),
		new(-1, 1),
		new(-1, 0),
		new(-1, -1),
	];

	static bool HasOpenNeighbour(Chunk chunk, int x, int y, Direction direction)
	{
		Vector2I dir = DirectionVectors[(int)direction];
		int nx = x + dir.X;
		int ny = y + dir.Y;

		return nx is >= 0 and < ChunkWidth && ny is >= 0 and < ChunkBigHeight && chunk[nx, ny] >= AirThreshold.Value;
	}

	public class Layer
	{
		Layer(TileMapLayer tileMapLayer) => TileMapLayer = tileMapLayer;

		public TileMapLayer TileMapLayer { get; }

		Chunk NextChunk;
		Chunk CurrChunk;
		Chunk PrevChunk;
		int ChunkDepth;

		public static Layer Generate()
		{
			var tml = new TileMapLayer
			{
				CollisionEnabled = true,
				TileSet = LayerTileSet,
				Scale = LayerScale,
			};

			var layer = new Layer(tml);

			for (int i = 0; i < MoleStartCount.Value; i++)
			{
				var mole = new Mole();

				// First mole should always start in centre.
				if (i == 0)
					mole.X = ChunkHalfWidth - 1;

				Moles.Add(mole);
			}

			for (int i = 0; i < ChunksPerLayer; i++)
				layer.GenerateChunk();

			return layer;
		}

		void GenerateChunk()
		{
			// Muncher needs to know some pre-munched state of previous AND next chunk to be seamless (fuck). So,
			// 1. Copy CurrChunk to PrevChunk
			//    1a. If CurrChunk is null, create surface chunk of air
			// 2. Copy NextChunk to CurrChunk
			//    2a. If NextChunk is null, create new CurrChunk, MoleAndMangle it
			// 3. Create new NextChunk, MoleAndMangle it
			// 4. Create bigChunk with ChunkExtraHeight bits from PrevChunk and NextChunk surrounding CurrChunk
			// 5. Munch bigChunk
			// 6. Write bigChunk to tiles
			// Performance seems fine; main cost is still running muncher items.
			if (CurrChunk == null)
			{
				PrevChunk = NewChunk();
				for (int x = 0; x < ChunkWidth; x++)
				for (int y = 0; y < ChunkHeight; y++)
					PrevChunk[x, y] = 1.0f;
			}
			else
			{
				MoleAndMangle(CurrChunk);
				PrevChunk = CloneChunk(CurrChunk);
			}

			if (NextChunk == null)
			{
				CurrChunk = NewChunk();
				MoleAndMangle(CurrChunk);
				ChunkDepth++;
			}
			else
			{
				CurrChunk = CloneChunk(NextChunk);
			}

			NextChunk = NewChunk();
			foreach (Mole mole in Moles)
				mole.Y = 0;
			MoleAndMangle(NextChunk);

			Chunk bigChunk = new float[ChunkWidth, ChunkBigHeight];

			// We need a copy of the active chunk anyway, seems *much* faster when we work
			// with contiguous arrays when doing neighbour checks.
			for (int x = 0; x < ChunkWidth; x++)
			{
				for (int y = 0; y < ChunkExtraHeight; y++)
					bigChunk[x, y] = PrevChunk[x, ChunkExtraDiff + y];

				for (int y = 0; y < ChunkHeight; y++)
					bigChunk[x, y + ChunkExtraHeight] = CurrChunk[x, y];

				for (int y = 0; y < ChunkExtraHeight; y++)
					bigChunk[x, y + ChunkExtraHeight + ChunkHeight] = NextChunk[x, y];
			}

			var muncher = new Muncher();
			muncher.EatChunk(bigChunk);

			WriteTileMap(bigChunk, ChunkDepth - 1);

			ChunkDepth++;
		}

		void MoleAndMangle(Chunk chunk)
		{
			// Moles are allowed to kill each other / go through mitosis (??) so `moles` list will be modified.
			// So obviously make a copy of list as we iter, and who knows what we'll end up with by the end.
			foreach (Mole mole in Moles.ToList())
				mole.DigChunk(chunk, Moles);

			Mangler mangler = new();
			mangler.MangleChunk(chunk, ChunkDepth);

			// Ensure sides are rock
			AddSideMargin(chunk);
		}

		void AddSideMargin(Chunk chunk)
		{
			for (int x = 0; x < SideMargin.Value; x++)
			for (int y = 0; y < ChunkHeight; y++)
			{
				int d = ChunkWidth - x - 1;
				chunk[x, y] = chunk[x, y] * x * SideMarginFadeFactor.Value * Random.Randf();
				chunk[d, y] = chunk[d, y] * (SideMargin.Value - x) * SideMarginFadeFactor.Value * Random.Randf();
			}
		}

		Vector2I GetAtlasCoords(Chunk c, int x, int y)
		{
			bool T = HasOpenNeighbour(c, x, y, Direction.Top);
			bool B = HasOpenNeighbour(c, x, y, Direction.Bottom);
			bool L = HasOpenNeighbour(c, x, y, Direction.Left);
			bool R = HasOpenNeighbour(c, x, y, Direction.Right);
			bool TL = HasOpenNeighbour(c, x, y, Direction.TopLeft);
			bool TR = HasOpenNeighbour(c, x, y, Direction.TopRight);
			bool BL = HasOpenNeighbour(c, x, y, Direction.BottomLeft);
			bool BR = HasOpenNeighbour(c, x, y, Direction.BottomRight);

			// Cases directly touching air
			if (T && L && R && B)
				return new(3, 1);
			if (T && R && B)
				return new(5, 2);
			if (T && L && B)
				return new(3, 2);
			if (T && L && R)
				return new(3, 3);
			if (L && R && B)
				return new(3, 5);
			if (T && L)
				return new(0, 0);
			if (T && B)
				return new(4, 2);
			if (T && R)
				return new(2, 0);
			if (L && B)
				return new(0, 2);
			if (B && R)
				return new(2, 2);
			if (L && R)
				return new(3, 4);
			if (T)
				return new(1, 0);
			if (L)
				return new(0, 1);
			if (B)
				return new(1, 2);
			if (R)
				return new(2, 1);

			// Diagonal to air
			if (TL)
				return new(2, 5);
			if (TR)
				return new(0, 5);
			if (BR) // fortnite
				return new(0, 3);
			if (BL)
				return new(2, 3);

			float r = Random.Randf();
			if (r <= 0.5f)
				return new(1, 1);
			if (r <= 0.75f)
				return new(5, 3);

			return new(5, 4);
		}

		void WriteTileMap(Chunk bigChunk, int chunkDepth)
		{
			for (int x = 0; x < ChunkWidth; x++)
			for (int y = ChunkExtraHeight; y < ChunkHeight + ChunkExtraHeight; y++)
			{
				float strength = bigChunk[x, y];
				if (strength >= AirThreshold.Value)
					continue;

				// lmao
				Vector2I atlasCoords;
				if (x is 0 or ChunkWidth - 1)
				{
					atlasCoords = new Vector2I(4, 1);
				}
				else if (x is 1 or ChunkWidth - 2)
				{
					atlasCoords = new Vector2I(4, 0);
				}
				else if (x is 2 or ChunkWidth - 3)
				{
					atlasCoords = new Vector2I(5, 1);
				}
				else if (x is 3 or ChunkWidth - 4)
				{
					atlasCoords = new Vector2I(5, 0);
				}
				else
				{
					atlasCoords = GetAtlasCoords(bigChunk, x, y);
				}

				TileMapLayer.SetCell(
					new Vector2I(x - ChunkHalfWidth, ChunkHeight * chunkDepth + (y - ChunkExtraHeight)),
					0,
					atlasCoords
				);
			}
		}
	}

	static Chunk NewChunk()
	{
		return new float[ChunkWidth, ChunkHeight];
	}

	static Chunk CloneChunk(Chunk chunk)
	{
		Chunk cloned = NewChunk();

		for (int x = 0; x < ChunkWidth; x++)
		for (int y = 0; y < ChunkHeight; y++)
			cloned[x, y] = chunk[x, y];

		return cloned;
	}
}
