using System.Collections.Generic;

public partial class WorldGenerator
{
    class Mole
    {
        public int X { get; set; } = (int)Random.Randi() % ChunkHalfWidth;
        public int Y { get; set; } = 0;

        // 0 is downwards dir, -1 left, 1 right.
        float Direction { get; set; } = 0;

        // Warning: I'l allowed to modify the moles list! Chaos ahead!
        public void DigChunk(Chunk chunk, List<Mole> moles)
        {
            Grid grid = chunk.Grid;

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
                    Direction = 0.75f; // Shove it back in the right direction
                }
                else if (X >= xMax)
                {
                    X = xMax;
                    Direction = -0.75f;
                }

                if (Y < 0)
                    Y = 0;

                if (Y < ChunkHeight - 1)
                {
                    grid.Cell(X, Y) = 1.0f;
                    for (int i = 1; i < width; i++)
                    {
                        float f = 1.0f - width * MoleHoleFalloff.Value;
                        grid.Cell(X - i, Y) = f;
                        grid.Cell(X + i, Y) = f;
                    }

                    // Clear out space above us as well - I *think* unless muncher
                    // happens to add stuff or we get weird margin issues, we should always
                    // get transferable gaps for our player size and hole size of 2.
                    if (Y > 0)
                    {
                        grid.Cell(X, Y - 1) = 1.0f;
                        for (int i = 1; i < width; i++)
                        {
                            float f = 1.0f - width * MoleHoleFalloff.Value;
                            grid.Cell(X - i, Y - 1) = f;
                            grid.Cell(X + i, Y - 1) = f;
                        }
                    }
                }
                else
                {
                    // Reached bottom
                    break;
                }

                TrySpawnNewMole(chunk, moles, lastMovedX);

                if (CheckDied(grid))
                {
                    moles.Remove(this);
                    return;
                }

                lastMovedX = Tunnel();
            }
        }

        // Tunnels downwards / to the side. Returns if we moved sideways so we
        // can avoid spawning loads of moles if we move sideways excessively.
        bool Tunnel()
        {
            // Normal distibution for new direction, mu = 0 so 0 is most common
            // (https://homepage.divms.uiowa.edu/~mbognar/applets/normal.html)
            float newDir = MoleUseNormalDist.Value
                ? Random.Randfn(0, MoleNormalSigma.Value)
                : Random.Randf() * 2.0f - 1.0f;
            bool movedX = false;

            Direction = newDir + Direction * MolePreviousDirMult.Value;
            // csharpier-ignore
            switch (Direction)
            {
                // We could allow tunnelling upwards if we really wanted but needs logic to avoid occasionally
                // tunnelling upwards out of bounds. Doesn't have much benefit and good MoleSpawnChance values do a
                // really good job at generating sections above us.
                // case < -1.0f or > 1.0f:
                //     Y--;
                //     break;
                case > -1.0f and < -0.5f:
                    X--;
					movedX = true;
                    // Y++;
                    break;
                case > -0.5f and < 0.5f:
                    Y++;
                    break;
                case > 0.5f and < 1.0f:
                    X++;
					movedX = true;
                    // Y++;
                    break;
            }

            return movedX;
        }

        void TrySpawnNewMole(Chunk chunk, List<Mole> moles, bool lastMovedX)
        {
            if (Random.Randf() > MoleSpawnChance.Value || moles.Count >= MaxMoles || lastMovedX)
                return;

            var newMole = new Mole { X = (int)Random.Randi() % ChunkWidth, Y = Y };
            moles.Add(newMole);
            newMole.DigChunk(chunk, moles);
        }

        bool CheckDied(Grid grid)
        {
            // Moles run first, so if we encounter open space below us, some other mole was here first
            if (Y == ChunkHeight - 1)
                return false;

            if (grid.Cell(X - MoleHoleSize.Value, Y + 1) >= 1.0f || grid.Cell(X + MoleHoleSize.Value, Y + 1) >= 1.0f)
            {
                // Gadzooks! We died.
                return (Random.Randf() < MoleMergeChance.Value);
            }

            return false;
        }
    }
}
