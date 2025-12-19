extends Node2D

@onready var title_sprite := $Title as Sprite2D
@onready var title_sprite_animator := $TitleAnimationPlayer as AnimationPlayer

var open_sound := preload("res://assets/audio/pause.ogg")

func _ready() -> void:
	title_sprite_animator.play("droop")

func _on_start_button_pressed() -> void:
	get_tree().change_scene_to_file("res://game.tscn")

func _on_quit_button_pressed() -> void:
	get_tree().quit()
