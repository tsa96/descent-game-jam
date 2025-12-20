class_name HUD
extends Control

@onready var player := get_parent().get_parent().get_node("World/Player") as Player
@onready var depth_meter := $Depth/Label as Label

func _physics_process(_delta: float) -> void:
	depth_meter.text = "Depth: %d" % player.position.y
