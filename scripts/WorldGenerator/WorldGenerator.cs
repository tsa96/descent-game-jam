using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public enum Tile : byte
{
    Rock = 0,
    Air = 1
}

public enum Direction
{
    North,
    NorthWest,
    West,
    SouthWest,
    South,
    SouthEast,
    East,
    NorthEast
}

// Chunk-based world gen powered by the MOLE, the MANGLER and the MUNCHER
// - The Mole: Weighted random tunnels downwards through chunks
// - The Mangler: Apply random noise on top of generate mole tunnels
// - The Muncher: Cellular automata that eats away at rocks with surrounding air
public partial class WorldGenerator : TileMapLayer
{
    const int ChunkWidth = 32;

    // TODO: Lower this drastically when other stuff makes sense. 8 minimum is needed for 8 muncher cycles
    const int ChunkHeight = 4096;
    const int ChunkHalfWidth = ChunkWidth / 2;
    const int MaxMoles = 8;

    static readonly RandomNumberGenerator Random = new RandomNumberGenerator();
    static readonly FastNoiseLite ManglerNoise = new FastNoiseLite();

    public override void _Ready()
    {
        InitializeSettings();
        Generate();
        base._Ready();
    }

    void Generate()
    {
        ulong startTime = Time.GetTicksMsec();

        SeedNoise();
        Clear();

        // TODO next: multiple chunks. remember to set moles's Y to 0!
        Chunk chunk = new();

        List<Mole> moles = [];
        for (int i = 0; i < MoleStartCount.Value; i++)
        {
            var mole = new Mole();
            // First mole should always start in centre.
            if (i == 0)
                mole.X = ChunkHalfWidth - 1;

            moles.Add(mole);
        }

        // Moles are allowed to kill each other / go through mitosis (??) so `moles` list will be modified.
        // So obviously make a copy of list as we iter, and who knows what we'll end up with by the end.
        foreach (Mole mole in moles.ToList())
            mole.DigChunk(chunk, moles);

        Mangler mangler = new();
        mangler.MangleChunk(chunk);

        Muncher muncher = new();
        muncher.EatChunk(chunk);

        int chunksDeep = 0;
        WriteTileMap(chunk, chunksDeep);

        PerfLabel.Text = $"Generated world in {Time.GetTicksMsec() - startTime}ms";
    }

    void SeedNoise()
    {
        // Use random seed if set to 0 in dbg ui
        var newSeed = Seed.Value;
        if (newSeed == 0)
            newSeed = (int)GD.Randi();

        Random.Seed = (ulong)newSeed;
        ManglerNoise.Seed = newSeed;

        // See https://auburn.github.io/FastNoiseLite/ to preview. Fractals probs not helpful!
        ManglerNoise.NoiseType = Enum.TryParse<FastNoiseLite.NoiseTypeEnum>(
            ManglerNoiseType.Value.ToString(),
            out var type
        )
            ? type
            : FastNoiseLite.NoiseTypeEnum.Perlin;
        ManglerNoise.Frequency = ManglerFrequency.Value;
    }

    static readonly Vector2I tmp = new Vector2I(0, 4);

    void WriteTileMap(Chunk chunk, int depth)
    {
        for (int x = 0; x < ChunkWidth; x++)
        {
            for (int y = 0; y < ChunkHeight; y++)
            {
                float strength = chunk.Grid.Cell(x, y);
                // ADDING a block, so if strength is BELOW the threshold for air
                if (strength < AirThresold.Value)
                {
                    SetCell(new Vector2I(x - ChunkHalfWidth, ChunkHeight * depth + y), 0, tmp);
                }
            }
        }
    }
}
