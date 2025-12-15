using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Godot;
using Chunk = float[,];

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
//
// Notes on perf:
// We're not generator a huge number of tiles so .NET chews through everything.
// GDScript seemed prohibitively slow, but I was using nested arrays of PackedByteArray at the time,
// using a flat PackedByteArray may have been faster.
// Main slowdown is setting the actual tileset, haven't looked into that much.
// Chunking massively improves the C# performance. Muncher is the most expensive for tall chunks, but lowering
// to 32 drastically speeds stuff up, presumably better cacheline efficiency when doing the neighbour checks.
public partial class WorldGenerator : TileMapLayer
{
    const int ChunkWidth = 32;
    const int ChunkHeight = 32;
    const int ChunkExtraHeight = 8;
    const int ChunkExtraDiff = ChunkHeight - ChunkExtraHeight;
    const int ChunkBigHeight = ChunkHeight + ChunkExtraHeight * 2;
    const int ChunkHalfWidth = ChunkWidth / 2;

    const int MaxMoles = 8;

    static CharacterBody2D Player;

    static readonly RandomNumberGenerator Random = new RandomNumberGenerator();
    static readonly FastNoiseLite ManglerNoise = new FastNoiseLite();

    static readonly sbyte[] OffsetX = [-1, 0, 1, -1, 1, -1, 0, 1];
    static readonly sbyte[] OffsetY = [-1, -1, -1, 0, 0, 1, 1, 1];

    Chunk _nextChunk = null;
    Chunk _currChunk = null;
    Chunk _prevChunk = null;
    int _chunkDepth = 0;

    readonly List<Mole> _moles = [];

    public override void _Ready()
    {
        Player = GetNode<CharacterBody2D>("Player");

        InitializeSettings();
        Generate();

        base._Ready();
    }

    public override void _Process(double delta)
    {
        int yPos = (int)Player.Position.Y;
        if (yPos > _chunkDepth * ChunkHeight)
        {
            GenerateChunk();
        }

        base._Process(delta);
    }

    void Reset()
    {
        Player.Position = new Vector2(0, 0);
        SeedNoise();
        Clear(); // TileMapLayer method
        _moles.Clear();
        _chunkDepth = 0;
        _nextChunk = null;
        _currChunk = null;
        _prevChunk = null;
    }

    void Generate()
    {
        Reset();

        ulong startTime = Time.GetTicksMsec();

        for (int i = 0; i < MoleStartCount.Value; i++)
        {
            var mole = new Mole();

            // First mole should always start in centre.
            if (i == 0)
                mole.X = ChunkHalfWidth - 1;

            _moles.Add(mole);
        }

        for (int i = 0; i < StartingChunkCount.Value; i++)
        {
            GenerateChunk();
        }

        PerfLabel.Text = $"Generated initial world in {Time.GetTicksMsec() - startTime}ms";
    }

    void GenerateChunk()
    {
        ulong startTime = Time.GetTicksMsec();

        // Muncher needs to know some pre-munched state of previous AND next chunk to be seamless (fuck). So,
        // 1. Copy _currChunk to _prevChunk
        //    1a. If _currChunkis null, create surface chunk of air
        // 2. Copy _nextChunk to _currChunk
        //    2a. If _nextChunk is null, create new _currChunk, MoleAndMangle it
        // 3. Create new _nextChunk, MoleAndMangle it
        // 4. Create activeChunk with ChunkExtraHeight bits from _prevChunk and _nextChunk surrounding _currChunk
        // 5. Munch activeChunk
        // 6. Write activeChunk to tiles
        // Performance seems fine; main cost is still running muncher iters.
        // Jeepers!

        if (_currChunk == null)
        {
            _prevChunk = NewChunk();
            for (int x = 0; x < ChunkWidth; x++)
            for (int y = 0; y < ChunkHeight; y++)
                _prevChunk[x, y] = 1.0f;
        }
        else
        {
            MoleAndMangle(_currChunk);
            _prevChunk = CloneChunk(_currChunk);
        }

        if (_nextChunk == null)
        {
            _currChunk = NewChunk();
            MoleAndMangle(_currChunk);
            _chunkDepth++;
        }
        else
        {
            _currChunk = CloneChunk(_nextChunk);
        }

        _nextChunk = NewChunk();
        foreach (Mole mole in _moles)
            mole.Y = 0;
        MoleAndMangle(_nextChunk);

        Chunk bigChunk = new float[ChunkWidth, ChunkBigHeight];

        // We need a copy of the active chunk anyway, seems *much* faster when we worth
        // with contiguous arrays when doing neighbour checks.
        // Could memory marshall for stuff like this but eehhhhhh arrays are pretty small
        // TODO: try flipping loop order? (maybe try other, simpler places first)
        for (int x = 0; x < ChunkWidth; x++)
        {
            for (int y = 0; y < ChunkExtraHeight; y++)
                bigChunk[x, y] = _prevChunk[x, ChunkExtraDiff + y];

            for (int y = 0; y < ChunkHeight; y++)
                bigChunk[x, y + ChunkExtraHeight] = _currChunk[x, y];

            for (int y = 0; y < ChunkExtraHeight; y++)
                bigChunk[x, y + ChunkExtraHeight + ChunkHeight] = _nextChunk[x, y];
        }

        var muncher = new Muncher();
        muncher.EatChunk(bigChunk);

        WriteTileMap(bigChunk, _chunkDepth - 1);

        _chunkDepth++;

        GD.Print($"Generated chunk {_chunkDepth} in {Time.GetTicksMsec() - startTime}ms");
    }

    void MoleAndMangle(Chunk chunk)
    {
        // Moles are allowed to kill each other / go through mitosis (??) so `moles` list will be modified.
        // So obviously make a copy of list as we iter, and who knows what we'll end up with by the end.
        foreach (Mole mole in _moles.ToList())
            mole.DigChunk(chunk, _moles);

        Mangler mangler = new();
        mangler.MangleChunk(chunk, _chunkDepth);

        // Ensure sides are rock
        AddSideMargin(chunk);
    }

    Chunk NewChunk()
    {
        return new float[ChunkWidth, ChunkHeight];
    }

    Chunk CloneChunk(Chunk chunk)
    {
        Chunk cloned = NewChunk();

        for (int x = 0; x < ChunkWidth; x++)
        for (int y = 0; y < ChunkHeight; y++)
            cloned[x, y] = chunk[x, y];

        return cloned;
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

    static readonly Vector2I tmp = new Vector2I(0, 0);
    static readonly Vector2I tmp2 = new Vector2I(22, 2);

    void WriteTileMap(Chunk bigChunk, int depth)
    {
        for (int x = 0; x < ChunkWidth; x++)
        for (int y = ChunkExtraHeight; y < ChunkHeight + ChunkExtraHeight; y++)
        {
            float strength = bigChunk[x, y];
            // ADDING a block, so if strength is BELOW the threshold for air
            if (strength < AirThresold.Value)
            {
                SetCell(
                    new Vector2I(x - ChunkHalfWidth, ChunkHeight * depth + y),
                    0,
                    y % ChunkHeight == 0 ? tmp2 : tmp
                );
            }
        }
    }
}
