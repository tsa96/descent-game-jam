class_name DeathScreen
extends Control

@onready var bgm := get_parent().get_parent().get_node("Audio/BGMEventEmitter") as FmodEventEmitter2D

func _ready() -> void:
	hide()

func close() -> void:
	bgm.set_parameter("Paused", 0)
	get_tree().paused = false
	hide()

func open() -> void:
	show()
	bgm.set_parameter("Paused", 1)

func _on_quit_button_pressed() -> void:
	if visible:
		get_tree().quit()
