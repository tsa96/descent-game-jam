class_name DeathScreen
extends Control

@onready var depth_label := $BackgroundShader/ColorRect/DepthVBoxContainer/Depth as Label
@onready var bgm := get_parent().get_parent().get_node("Audio/BGMEventEmitter") as FmodEventEmitter2D

func _ready() -> void:
	hide()

func close() -> void:
	bgm.set_parameter("Paused", 0)
	get_tree().paused = false
	hide()

func open(depth_reached: float) -> void:
	depth_label.text = "Depth: %d" % depth_reached
	show()
	bgm.set_parameter("Paused", 1)

func _on_quit_button_pressed() -> void:
	if visible:
		get_tree().quit()
