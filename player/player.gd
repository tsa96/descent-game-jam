class_name Player
extends CharacterBody2D

const SANITY_MAX = 100.0
var sanity: float

const START_POS =  Vector2(-30 * 8, -32);

const WALK_SPEED = 240.0
const WALK_ACCEL = WALK_SPEED * 6.0
const DASH_SPEED = 700.0
const JUMP_VELOCITY = -525.0
const SMALL_JUMP_MULT = 0.4
const TERMINAL_VELOCITY = 500.0
const FAST_FALL_MULT = 0.7
const FAST_FALL_RECOVER_MULT = 2.5
const WALL_CLIMB_SLOWDOWN = 30
const WALL_JUMP_VELOCITY = -350.0
const WALL_JUMP_COOLDOWN = 1
const WALL_JUMP_MAX_SPEED = 250

var gravity: int = ProjectSettings.get(&"physics/2d/default_gravity")
var dash_charged: bool
var last_walljump: float

@onready var wall_jump_ray_left := $WallClimbRayLeft as RayCast2D
@onready var wall_jump_ray_right := $WallClimbRayRight as RayCast2D
@onready var animation_player := $AnimationPlayer as AnimationPlayer
@onready var sprite := $Sprite2D as Sprite2D
@onready var jump_sound := $Jump as AudioStreamPlayer2D
@onready var camera := $Camera as Camera2D


func _ready() -> void:
	reset()


func reset() -> void:
	sanity = SANITY_MAX
	position = START_POS
	velocity = Vector2(0, 0)
	dash_charged = false
	last_walljump = 1000
	jump_sound.stop()


func _physics_process(delta: float) -> void:
	if is_on_floor():
		dash_charged = true
		
	var direction := Input.get_axis("move_left", "move_right")
		
	# Wall climbing
	last_walljump += delta
	if Input.is_action_pressed("jump") and is_on_wall() and direction != 0 and last_walljump > WALL_JUMP_COOLDOWN:
		velocity.y = maxf(0, velocity.y - gravity * WALL_CLIMB_SLOWDOWN * delta)
		
	# Jumping
	if Input.is_action_just_pressed("jump"):
		print(velocity.y)
		if is_on_floor():
			velocity.y = JUMP_VELOCITY
			jump_sound.play()
		elif is_close_to_wall() and velocity.y < WALL_JUMP_MAX_SPEED and last_walljump > WALL_JUMP_COOLDOWN:
			velocity.y = WALL_JUMP_VELOCITY
			jump_sound.play()
			last_walljump = 0
	elif Input.is_action_just_released("jump") and velocity.y < 0.0:
		# The player let go of jump early, reduce vertical momentum
		velocity.y *= SMALL_JUMP_MULT
		
	# Falling
	if Input.is_action_pressed("fall"):
		# Fast-falling: allowed to exceed TV, acceleration even greater
		velocity.y = velocity.y + gravity * delta * FAST_FALL_MULT
	elif velocity.y > TERMINAL_VELOCITY - 0.001:
		# Speed exceeds TV; we must have just stopped fast-falling, decelerate towards TV
		velocity.y = velocity.y - gravity * delta * FAST_FALL_RECOVER_MULT
	else:
		# Regular falling, accelarate towards to TV
		velocity.y = minf(TERMINAL_VELOCITY, velocity.y + gravity * delta)	

	# A/D movement
	velocity.x = move_toward(velocity.x, direction * WALK_SPEED, WALK_ACCEL * delta)

	# Dash
	if Input.is_action_just_pressed("dash") and dash_charged and direction != 0:
		velocity.x = direction * DASH_SPEED
		dash_charged = false

	if not is_zero_approx(velocity.x):
		if velocity.x > 0.0:
			sprite.scale.x = 1.0
		else:
			sprite.scale.x = -1.0

	move_and_slide()

	var animation := get_new_animation()
	if animation != animation_player.current_animation:
		animation_player.play(animation)


func get_new_animation() -> String:
	var animation_new: String
	if is_on_floor():
		if absf(velocity.x) > 0.1:
			animation_new = "run"
		else:
			animation_new = "idle"
	else:
		if velocity.y > 0.0:
			animation_new = "falling"
		else:
			animation_new = "jumping"
	return animation_new
	

func is_close_to_wall() -> bool:
	# is_on_wall is too tight, extremely hard to do a walljump in the opposite direction
	# before sideways movement makes the check fail - use raycasts instead (configure in 2D view!)
	return wall_jump_ray_left.is_colliding() or wall_jump_ray_right.is_colliding()
	
