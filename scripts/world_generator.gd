class_name WorldGenerator
extends Node

enum TileType { 
	BECOMING_AIR = 0,
	AIR = 1,
	ROCK = 2
}

const TileOffsets: Dictionary[TileType, Vector2i] = {
	TileType.AIR: Vector2i(0, 8),
	TileType.ROCK: Vector2i(44, 4)
}

const OFFSETS := [[-1, -1], [0, -1], [1, -1], [-1, 0], [1, 0], [-1, 1], [0, 1], [1, 1]]

# Grid is stored as array of columns, so columns are (much faster)
# PackedByteArrays since they're far larger.
# TODO: 1D array might be more efficient here, idk, million ways to optimise this.
var grid: Array[PackedByteArray] = []
var noise := FastNoiseLite.new()
@onready var tile_map: TileMapLayer = $TileMapLayer

var world_seed: int = 0
var width: int = 0
var half_width: int = 0
var height: int = 0
var starting_rock_chance: float = 0
var iters: int = 0
@onready var perf_label: Label = $DebugUI/GridContainer/Perf
@onready var regen_button: Button = $DebugUI/GridContainer/Regenerate
@onready var world_seed_input: LineEdit = $DebugUI/GridContainer/SeedInput
@onready var width_input: LineEdit = $DebugUI/GridContainer/WidthInput
@onready var height_input: LineEdit = $DebugUI/GridContainer/HeightInput
@onready var starting_rock_chance_input: LineEdit = $DebugUI/GridContainer/StartingRockInput
@onready var iters_input: LineEdit = $DebugUI/GridContainer/ItersInput

func _ready() -> void:
	perf_label.text = ""
	world_seed_input.text = ""
	width_input.text = "256"
	height_input.text = "1024"
	starting_rock_chance_input.text = "0.4"
	iters_input.text = "5"
	world_seed_input.text = str(randi())
	regen_button.pressed.connect(generate)
	
	generate();
	
func generate() -> void:
	var start_time := Time.get_ticks_msec()
	width = width_input.text.to_int()
	@warning_ignore("integer_division")
	half_width = width / 2
	height = height_input.text.to_int()
	starting_rock_chance = starting_rock_chance_input.text.to_float()
	iters = iters_input.text.to_int()
	world_seed  = world_seed_input.text.to_int() if world_seed_input.text != "" else 0
	
	# https://www.cs.cmu.edu/~112-s23/notes/student-tp-guides/Terrain.pdf Example 7
	# First we initialize 2D grid (Array of PackedByteArray), setting each cell
	# to AIR or ROCK. Then apply iter_automata sequential to hollow out the grid.
	tile_map.clear()
	initialize_noise()
	initialize_grid()
	for i in range(iters):
		iter_automata()
	write_tile_map()
	
	perf_label.text = "Generated world in %dms" % (Time.get_ticks_msec() - start_time)

func initialize_noise() -> void:
	var s := world_seed
	seed(s)
	noise.seed = s

func initialize_grid() -> void:
	grid.resize(width)
	for x in range(width):
		var column := PackedByteArray()
		column.resize(height)
		for y in range(height):
			column[y] = TileType.ROCK if randf() > starting_rock_chance else TileType.AIR
		grid[x] = column

func iter_automata() -> void:
	for x in range(width):
		for y in range(height):
			var open_neighbours: int = 0
			for offset in OFFSETS:
				var neighbour_x = x + offset[0]
				var neighbour_y = y + offset[1]
				if neighbour_x < 0 or neighbour_x >= width or neighbour_y < 0 or neighbour_y >= height:
					continue
				var neighbour_type: int = grid[neighbour_x][neighbour_y]
				if neighbour_type == TileType.AIR or TileType.BECOMING_AIR:
					open_neighbours += 1
			if grid[x][y] == TileType.AIR:
				if open_neighbours >= 0:
					grid[x][y] = TileType.AIR
				else:
					grid[x][y] = TileType.ROCK
			else:
				if open_neighbours >= 5:
					grid[x][y] = TileType.BECOMING_AIR
				else:
					grid[x][y] = TileType.ROCK
				
	for x in range(width):
		for y in range(height):
			if grid[x][y] == TileType.BECOMING_AIR:
				grid[x][y] = TileType.AIR;
	
	
func write_tile_map() -> void:
	for x in range(width):
		for y in range(height):
			if grid[x][y] == TileType.ROCK:
				tile_map.set_cell(Vector2i(x - half_width, y), 0, Vector2i(0, 4))
