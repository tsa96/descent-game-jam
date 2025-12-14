class_name WorldGeneration
extends Node

# Called every frame. 'delta' is the elapsed time since the previous frame.

var noise: FastNoiseLite = FastNoiseLite.new()

#var image: ImageTexture = ImageTexture.create_from_image(Image.load_from_file("res://assets/tileset.png"))
#var texture := AtlasTexture.new()
#var tile_map := TileMapLayer.new()
#var tile_set := TileSet.new()
var tile_map: TileMapLayer
#var source := TileSetAtlasSource.new()


func _ready() -> void:
	noise.seed = randi()
	
	tile_map = $Tiles;
	for i in range(-16, 16):
		for j in range(0, 16):
			for k in range(0, 48):
				for p in range(0, 16):
					tile_map.set_cell(Vector2i(i, j + 4), 0, Vector2i(k, p))	
	

func _process(delta: float) -> void:
	pass
