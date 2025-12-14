class_name WorldGeneration
extends Node

# Called every frame. 'delta' is the elapsed time since the previous frame.

var noise: FastNoiseLite = FastNoiseLite.new()

var image: ImageTexture = ImageTexture.create_from_image(Image.load_from_file("res://assets/tileset.png"))
var texture := AtlasTexture.new()
var tile_map := TileMapLayer.new()
var tile_set := TileSet.new()
var source := TileSetAtlasSource.new()


func _ready() -> void:
	noise.seed = randi()
	
	texture.atlas = image
	source.texture = texture
	source.texture_region_size = Vector2i(16, 16) # TODO: wrongo
	source.create_tile(Vector2i(0,0), Vector2i(16,16))
	tile_set.add_source(source, 0)
	tile_map.tile_set = tile_set
	tile_map.set_cell(Vector2i(10,10), 0, Vector2i(0,0))	
	

func _process(delta: float) -> void:
	pass
