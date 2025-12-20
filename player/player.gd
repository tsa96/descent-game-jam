class_name Player
extends CharacterBody2D

const SANITY_MAX = 100.0
var sanity: float

const START_POS =  Vector2(-30 * 8, -32);

const WALK_SPEED = 240.0
const WALK_ACCEL = WALK_SPEED * 6.0
const DASH_SPEED = 700.0
const DASH_SANITY_DRAIN = 1.0
const JUMP_VELOCITY = -525.0
const SMALL_JUMP_MULT = 0.4
const TERMINAL_VELOCITY = 500.0
const FAST_FALL_MULT = 0.7
const FAST_FALL_RECOVER_MULT = 2.5
const WALL_CLIMB_SLOWDOWN = 30
const WALL_JUMP_VELOCITY = -350.0
const WALL_JUMP_COOLDOWN = 1
const WALL_JUMP_MAX_SPEED = 250
const FOOTSTEP_AUDIO_TIMER_RESET = .3
const HIGH_LANDING_VELOCITY = 400.0
const COYOTE_TIME = 0.1
const TERMINAL_VELOCITY_SANITY_DRAIN = 0.125
const FAST_FALL_SANITY_DRAIN = 0.0002

const IDLE_ANIM = "idle"
const WALK_ANIM = "walk"
const JUMPING_ANIM = "jump"
const FALLING_ANIM = "fall"
const FALLING_FAST_ANIM = "fastfall"
const DASH_ANIM = "dash"
const LANDING_ANIM = "land"
const DEAD_ANIM = "death"

var scroll_speed: float
var gravity: int = ProjectSettings.get(&"physics/2d/default_gravity")
var dash_charged: bool
var last_walljump: float
var footstep_audio_timer: float
var regen_on: float
var dead: bool = false
var coyote_timer: float

@onready var wall_jump_ray_left := $WallClimbRayLeft as RayCast2D
@onready var wall_jump_ray_right := $WallClimbRayRight as RayCast2D
@onready var player_animator := $PlayerAnimator as AnimationPlayer
@onready var player_sprite := $PlayerSprite as Sprite2D
@onready var sticky := $"../Sticky" as Node2D
@onready var world := $"../"

@onready var footstep_audio_emitter := $Audio/FootstepAudioEventEmitter as FmodEventEmitter2D
@onready var land_high_vel_audio_emitter := $Audio/LandHighVelocityAudioEventEmitter as FmodEventEmitter2D
@onready var dash_audio_emitter := $Audio/DashAudioEventEmitter as FmodEventEmitter2D
@onready var death_audio_emitter := $Audio/DeathAudioEventEmitter as FmodEventEmitter2D
@onready var eating_audio_emitter := $Audio/EatingAudioEventEmitter as FmodEventEmitter2D
@onready var eating_psyc_audio_emitter := $Audio/EatingPsycAudioEventEmitter as FmodEventEmitter2D
@onready var death_finish_audio_emitter := $Audio/DeathFinishAudioEventEmitter as FmodEventEmitter2D

signal on_reset()
signal on_start_death()
signal on_death()


func _ready() -> void:
	reset(true)


func reset(silent: bool = false) -> void:
	sanity = SANITY_MAX
	position = START_POS
	velocity = Vector2(0, 0)
	sticky.position = START_POS
	dash_charged = false
	last_walljump = 1000
	footstep_audio_timer = 0
	scroll_speed = 0
	dead = false
	coyote_timer = 1000
	if not silent:
		on_reset.emit()


func _physics_process(delta: float) -> void:
	if dead:
		velocity.x = move_toward(velocity.x, 0, WALK_ACCEL * delta)
		velocity.y = minf(TERMINAL_VELOCITY, velocity.y + gravity * delta)
		move_and_slide()
		return
	
	if is_on_floor():
		dash_charged = true
		coyote_timer = 0
	else:
		coyote_timer += delta
		
	var direction := Input.get_axis("move_left", "move_right")
		
	# Wall climbing
	last_walljump += delta
	if Input.is_action_pressed("jump") and is_on_wall() and direction != 0 and last_walljump > WALL_JUMP_COOLDOWN and velocity.y > 0:
		velocity.y = maxf(0, velocity.y - gravity * WALL_CLIMB_SLOWDOWN * delta)
		
	# Jumping
	if Input.is_action_just_pressed("jump"):
		if is_on_floor() or coyote_timer < COYOTE_TIME:
			velocity.y = JUMP_VELOCITY
			footstep_audio_emitter.play()
		elif is_close_to_wall() and velocity.y < WALL_JUMP_MAX_SPEED and last_walljump > WALL_JUMP_COOLDOWN:
			velocity.y = WALL_JUMP_VELOCITY
			footstep_audio_emitter.play()
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
	var just_dashed: bool = false
	if Input.is_action_just_pressed("dash") and dash_charged and direction != 0:
		velocity.x = direction * DASH_SPEED
		dash_charged = false
		sanity -= DASH_SANITY_DRAIN
		regen_on = false
		just_dashed = true
		$Regen.start()

	var prev_fall_speed := abs(velocity.y) as float
	var was_on_ground := is_on_floor() as bool
	move_and_slide()
	
	var just_landed := not was_on_ground and is_on_floor() as bool
	var just_fell := was_on_ground and not is_on_floor() as bool
	
	play_character_audio(just_dashed, just_fell, just_landed, prev_fall_speed)
	play_animation(direction, just_dashed, just_landed)
		
	# Sanity
	if sanity > SANITY_MAX: 
		sanity = SANITY_MAX
	
	# Sanity Regen
	if sanity < SANITY_MAX and regen_on:
		sanity += 0.1
	
	# If you are falling at terminal velocity, sanity will go down
	if velocity.y == TERMINAL_VELOCITY:
		sanity -= TERMINAL_VELOCITY_SANITY_DRAIN
		regen_on = false
		$Regen.start()
	
	# The same but if you're falling faster it'll go down faster
	if velocity.y > TERMINAL_VELOCITY:
		sanity -= velocity.y * FAST_FALL_SANITY_DRAIN
		regen_on = false
		$Regen.start()
	
	# Death
	if sanity <= 0:
		dead = true
		play_animation(direction, false, false)
		play_character_audio(false, false, false)
		on_start_death.emit()

	sticky.position.y = maxf(position.y, sticky.position.y + scroll_speed * delta)
	
	# Fuck you, stop falling off
	position.x = clamp(position.x, -33 * 8, 33 * 8)


func play_character_audio(just_dashed: bool, just_fell: bool, just_landed: bool, prev_fall_speed: float = 0.0) -> void:
	if dead:
		death_audio_emitter.play_one_shot()
		return
	
	if just_dashed:
		dash_audio_emitter.play_one_shot()
	
	if just_landed and prev_fall_speed >= HIGH_LANDING_VELOCITY:
		land_high_vel_audio_emitter.play_one_shot()
	elif just_landed or just_fell:
		footstep_audio_emitter.play_one_shot()
	else:
		footstep_audio_timer = 0


func play_animation(direction: float, just_dashed: bool, just_landed: bool) -> void:
	if not is_zero_approx(direction):
		if direction > 0.0:
			player_sprite.scale.x = 1.0
		else:
			player_sprite.scale.x = -1.0
	
	var animation: String
	
	if dead:
		animation = DEAD_ANIM
	elif just_dashed:
		animation = DASH_ANIM
	elif just_landed:
		animation = LANDING_ANIM
	elif is_on_floor():
		if absf(velocity.x) > 0.1:
			animation = WALK_ANIM
		else:
			animation = IDLE_ANIM
	else:
		if velocity.y > 0.0:
			if velocity.y >= HIGH_LANDING_VELOCITY:
				animation = FALLING_FAST_ANIM
			else:
				animation = FALLING_ANIM
		else:
			animation = JUMPING_ANIM
	
	var cur_animation: String = player_animator.current_animation
	if cur_animation == animation:
		return
	
	if player_animator.is_playing() and (cur_animation == DASH_ANIM or cur_animation == LANDING_ANIM) and (animation != LANDING_ANIM and animation != JUMPING_ANIM and animation != DEAD_ANIM):
		# Let dash/land anim play fully before continuing with walk/fall. Prevents those from squashing these anims
		return
	
	player_animator.play(animation)


func is_close_to_wall() -> bool:
	# is_on_wall is too tight, extremely hard to do a walljump in the opposite direction
	# before sideways movement makes the check fail - use raycasts instead (configure in 2D view!)
	return wall_jump_ray_left.is_colliding() or wall_jump_ray_right.is_colliding()


func _on_regen_timeout() -> void:
	regen_on = true


func sanity_gain(damage: float):
	sanity += damage
	dash_charged = true
	regen_on = true


func sanity_drain(damage: float):
	sanity -= damage


func mushie_eaten(good: bool):
	if good:
		eating_audio_emitter.play_one_shot()
	else:
		eating_psyc_audio_emitter.play_one_shot()


func _on_player_animator_animation_finished(anim_name: StringName) -> void:
	if anim_name == DEAD_ANIM:
		death_finish_audio_emitter.play_one_shot()
		on_death.emit()


func _on_player_death_hit_floor() -> void:
	land_high_vel_audio_emitter.play_one_shot()


func _on_player_death_tripped() -> void:
	footstep_audio_emitter.play_one_shot()


func _on_player_footstep() -> void:
	footstep_audio_emitter.play_one_shot()
