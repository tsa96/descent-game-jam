extends Area2D




func _on_body_entered(body: Node2D) -> void:
	if body.name == "Player":
		$Player.sanity_gain(15)
		queue_free()
