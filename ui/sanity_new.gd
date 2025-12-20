extends Node2D

@onready var player := $"../../../World/Player" as Player
@onready var bar_mask := $BarMask as ColorRect
@onready var bar := $BarMask/Bar as Sprite2D
@onready var mask_height := bar_mask.size.y as float

func _process(_delta: float) -> void:
	var sanity_normalized = player.sanity / 100.0
	bar_mask.size.y = sanity_normalized * mask_height
