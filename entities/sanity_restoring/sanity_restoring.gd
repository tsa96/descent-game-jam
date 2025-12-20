extends Area2D


func _on_body_entered(body: Node2D) -> void:
	if body.name == "Player":
		(body as Player).sanity_gain(50)
		(body as Player).mushie_eaten(true)
		queue_free()
