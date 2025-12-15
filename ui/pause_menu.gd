class_name PauseMenu
extends Control

@onready var resume_button := $ColorRect/VBoxContainer/ResumeButton

func _ready() -> void:
	hide()

func close() -> void:
	get_tree().paused = false

func open() -> void:
	show()
	resume_button.grab_focus()

func _on_resume_button_pressed() -> void:
	close()

func _on_quit_button_pressed() -> void:
	if visible:
		get_tree().quit()
