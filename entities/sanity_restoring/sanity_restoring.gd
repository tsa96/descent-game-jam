extends Area2D


func _ready():
	print("pog")


func _on_body_entered(body: Node2D) -> void:
	if body.name == "Player":
		(body as Player).sanity_gain(15)
		queue_free()
