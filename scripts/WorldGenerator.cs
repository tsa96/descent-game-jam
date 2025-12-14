using System.Collections.Generic;
using Godot;

public partial class WorldGenerator : Node
{
    public enum Tile : byte
    {
        Air = 0,
        Rock = 1
    }

    // TODO: this
    private static readonly Dictionary<Tile, Vector2I> TileOffsets = new Dictionary<Tile, Vector2I>
    {
        { Tile.Air, new Vector2I(0, 8) },
        { Tile.Rock, new Vector2I(44, 4) }
    };

    private static readonly sbyte[] OffsetX = [-1, 0, 1, -1, 1, -1, 0, 1];
    private static readonly sbyte[] OffsetY = [-1, -1, -1, 0, 0, 1, 1, 1];

    // Flat grid array for perf, seems fair bit faster.
    private Tile[] _grid = [];

    // Buffer to writing to as we run automata cycles. Expensive, blegh!!
    private Tile[] _gridBuffer = [];

    private FastNoiseLite _noise = new FastNoiseLite();
    private TileMapLayer _tileMap;

    private int _worldSeed = 0;
    private int _width = 0;
    private int _halfWidth = 0;
    private int _height = 0;
    private float _startingRockChance = 0;
    private int _iters = 0;

    private Label _perfLabel;
    private Button _regenButton;
    private LineEdit _worldSeedInput;
    private LineEdit _widthInput;
    private LineEdit _heightInput;
    private LineEdit _startingRockChanceInput;
    private LineEdit _itersInput;

    public override void _Ready()
    {
        _tileMap = GetNode<TileMapLayer>("TileMapLayer");
        _perfLabel = GetNode<Label>("DebugUI/GridContainer/Perf");
        _regenButton = GetNode<Button>("DebugUI/GridContainer/Regenerate");
        _worldSeedInput = GetNode<LineEdit>("DebugUI/GridContainer/SeedInput");
        _widthInput = GetNode<LineEdit>("DebugUI/GridContainer/WidthInput");
        _heightInput = GetNode<LineEdit>("DebugUI/GridContainer/HeightInput");
        _startingRockChanceInput = GetNode<LineEdit>("DebugUI/GridContainer/StartingRockInput");
        _itersInput = GetNode<LineEdit>("DebugUI/GridContainer/ItersInput");

        _perfLabel.Text = "";
        _worldSeedInput.Text = "";
        _widthInput.Text = "256";
        _heightInput.Text = "1024";
        _startingRockChanceInput.Text = "0.4";
        _itersInput.Text = "5";
        _worldSeedInput.Text = (GD.Randi() / 2).ToString();
        _regenButton.Pressed += Generate;

        Generate();
    }

    private void Generate()
    {
        var startTime = Time.GetTicksMsec();
        _width = _widthInput.Text.ToInt();
        _halfWidth = _width / 2;
        _height = _heightInput.Text.ToInt();
        _startingRockChance = _startingRockChanceInput.Text.ToFloat();
        _iters = _itersInput.Text.ToInt();
        _worldSeed = !string.IsNullOrEmpty(_worldSeedInput.Text) ? _worldSeedInput.Text.ToInt() : 0;

        // https://www.cs.cmu.edu/~112-s23/notes/student-tp-guides/Terrain.pdf Example 7
        // First we initialize 2D grid (jagged array), setting each cell
        // to AIR or ROCK. Then apply IterAutomata sequential to hollow out the grid.
        _tileMap.Clear();
        InitializeNoise();
        InitializeGrid();
        for (var i = 0; i < _iters; i++)
            IterAutomata();

        WriteTileMap();

        _perfLabel.Text = $"Generated world in {Time.GetTicksMsec() - startTime}ms";
    }

    private void InitializeNoise()
    {
        var s = _worldSeed;
        GD.Seed((ulong)s);
        _noise.Seed = s;
    }

    private void InitializeGrid()
    {
        int totalSize = _width * _height;

        // Reuse if possible
        if (_grid == null || _grid.Length != totalSize)
        {
            _grid = new Tile[totalSize];
            _gridBuffer = new Tile[totalSize];
        }

        for (int x = 0; x < _width; x++)
        {
            int columnStart = x * _height;
            for (int y = 0; y < _height; y++)
            {
                _grid[columnStart + y] = GD.Randf() > _startingRockChance ? Tile.Rock : Tile.Air;
            }
        }
    }

    private void IterAutomata()
    {
        for (int x = 0; x < _width; x++)
        {
            int columnStart = x * _height;

            for (int y = 0; y < _height; y++)
            {
                int idx = columnStart + y;
                int openNeighbours = 0;

                for (int i = 0; i < 8; i++)
                {
                    int neighbourX = x + OffsetX[i];
                    int neighbourY = y + OffsetY[i];

                    // Bounds check
                    if (neighbourX < 0 || neighbourX >= _width || neighbourY < 0 || neighbourY >= _height)
                        continue;

                    int neighbourIdx = neighbourX * _height + neighbourY;
                    Tile neighbourType = _grid[neighbourIdx];

                    if (neighbourType is Tile.Air)
                        openNeighbours++;
                }

                Tile currentTile = _grid[idx];

                if (currentTile == Tile.Air)
                {
                    // Air stays air if 4+ open neighbours, else rock
                    _gridBuffer[idx] = openNeighbours >= 4 ? Tile.Air : Tile.Rock;
                }
                else
                {
                    // Rock becomes air if 5+ open neighbours, else rock
                    _gridBuffer[idx] = openNeighbours >= 5 ? Tile.Air : Tile.Rock;
                }
            }
        }

        // Just ptr swap
        (_grid, _gridBuffer) = (_gridBuffer, _grid);
    }

    static readonly Vector2I tmp = new Vector2I(0, 4);

    private void WriteTileMap()
    {
        for (var x = 0; x < _width; x++)
        {
            int columnStart = x * _height;
            for (var y = 0; y < _height; y++)
            {
                if (_grid[columnStart + y] == Tile.Rock)
                {
                    _tileMap.SetCell(new Vector2I(x - _halfWidth, y), 0, tmp);
                }
            }
        }
    }
}
