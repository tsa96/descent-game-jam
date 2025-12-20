using System.Collections.Generic;
using Cells = float[,];

public partial class WorldGenerator
{
	class Mole
	{
		public int X { get; set; } = Random.RandiRange(0, ChunkWidth - 1);
		public int Y { get; set; } = 0;

		// 0 is downwards dir, -1 left, 1 right.
		float Dir { get; set; } = 0;

		// Warning: I'm allowed to modify the moles list! Chaos ahead!
		public void DigChunk(Cells cells, List<Mole> moles)
		{
			int width = MoleHoleSize.Value;
			int side = SideMargin.Value;
			if (width <= 0)
				return;

			bool lastMovedX = false;
			while (true)
			{
				// Stop tunnelling into walls. Idiot.
				int xMin = side + width;
				int xMax = ChunkWidth - side - width - 1;
				if (X <= xMin)
				{
					X = xMin;
					Dir = 0.75f; // Shove it back in the right direction
				}
				else if (X >= xMax)
				{
					X = xMax;
					Dir = -0.75f;
				}

				if (Y < 0)
					Y = 0;

				if (Y < ChunkHeight)
				{
					cells[X, Y] = 1.0f;
					for (int i = 1; i < width; i++)
					{
						float f = 1.0f - width * MoleHoleFalloff.Value;
						cells[X - i, Y] = f;
						cells[X + i, Y] = f;
					}

					// Clear out space above us as well - I *think* unless muncher
					// happens to add stuff, or we get weird margin issues, we should always
					// get transferable gaps for our player size and hole size of 2.
					if (Y > 0)
					{
						cells[X, Y - 1] = 1.0f;
						for (int i = 1; i < width; i++)
						{
							float f = 1.0f - width * MoleHoleFalloff.Value;
							cells[X - i, Y - 1] = f;
							cells[X + i, Y - 1] = f;
						}
					}
				}
				else
				{
					// Reached bottom
					break;
				}

				TrySpawnNewMole(cells, moles, lastMovedX);

				if (CheckDied(cells))
				{
					moles.Remove(this);
					return;
				}

				lastMovedX = Tunnel();
			}
		}

		int downwardsCounter = 0;

		// Tunnels downwards / to the side. Returns if we moved sideways so we
		// can avoid spawning loads of moles if we move sideways excessively.
		bool Tunnel()
		{
			// Normal distribution for new direction, mu = 0 so 0 is most common
			// (https://homepage.divms.uiowa.edu/~mbognar/applets/normal.html)
			float newDir = MoleUseNormalDist.Value
				? Random.Randfn(0, MoleNormalSigma.Value)
				: Random.Randf() * 2.0f - 1.0f;
			bool movedX = false;
			bool movedOnlyY = false;

			Dir = newDir + Dir * MolePreviousDirMult.Value;
			// csharpier-ignore
			switch (Dir)
			{
				case > -1.0f and < -0.5f:
					X--;
					movedX = true;
					Y++;
					break;
				case > -0.5f and < 0.5f:
					Y++;
					movedOnlyY = true;
					break;
				case > 0.5f and < 1.0f:
					X++;
					movedX = true;
					Y++;
					break;
			}

			// Moles should be *heavily* biased to avoid going downwards for long periods of time.
			if (movedOnlyY)
			{
				downwardsCounter++;
				if (downwardsCounter >= 10)
				{
					// Force left or right move, biased away from nearest wall
					if (X < ChunkHalfWidth)
						X++;
					else
						X--;
					movedX = true;
					downwardsCounter -= 2;
				}
			}
			else
			{
				downwardsCounter -= 1;
			}

			return movedX;
		}

		void TrySpawnNewMole(Cells cells, List<Mole> moles, bool lastMovedX)
		{
			if (Random.Randf() > MoleSpawnChance.Value || moles.Count >= MaxMoles || lastMovedX)
				return;

			var newMole = new Mole { X = (int)Random.RandiRange(0, ChunkWidth - 1), Y = Y };
			moles.Add(newMole);
			newMole.DigChunk(cells, moles);
		}

		bool CheckDied(Cells cells)
		{
			// Moles run first, so if we encounter open space below us, some other mole was here first
			if (Y == ChunkHeight - 1)
				return false;

			if (cells[X - MoleHoleSize.Value, Y + 1] >= 1.0f || cells[X + MoleHoleSize.Value, Y + 1] >= 1.0f)
			{
				// Gadzooks! We died.
				return (Random.Randf() < MoleMergeChance.Value);
			}

			return false;
		}
	}
}
