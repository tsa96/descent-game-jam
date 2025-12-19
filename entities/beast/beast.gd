extends Node2D

@onready var collision := $Area2D as Area2D


func _process(delta: float) -> void:
	for body in collision.get_overlapping_bodies():
		if body.name == "Player":
			(body as Player).sanity_gain(-2.0 * delta)
