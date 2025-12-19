extends Area2D


func _on_body_entered(body: Node2D) -> void:
	if body.name == "Player":
		(body as Player).sanity_drain(30)
		queue_free()
