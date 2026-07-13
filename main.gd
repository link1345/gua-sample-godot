extends Control

const GuaAutoAdapterScript := preload("res://addons/gua/gua_auto_adapter.gd")
const DESIGN_SIZE := Vector2(541.0, 857.0)
const LOADING_DURATION_SECONDS := 6.0

@onready var design_root: Control = %DesignRoot
@onready var page_one: Control = %PageOne
@onready var page_two: Control = %PageTwo
@onready var loading_label: Label = %LoadingLabel
@onready var back_button: Button = %BackButton
@onready var exit_confirmation: Control = %ExitConfirmation

var gua: GuaAutoAdapter
var current_screen := "page1"
var loading_generation := 0


func _ready() -> void:
	get_viewport().size_changed.connect(_update_responsive_layout)
	_update_responsive_layout()
	gua = GuaAutoAdapterScript.new()
	_show_page_one()
	var bridge_port := _resolve_gua_bridge_port()
	if gua.start_inspector_bridge(bridge_port):
		print("Gua bridge listening at %s" % gua.inspector_bridge_url())
	else:
		push_error("Failed to start the Gua Inspector bridge on port %d." % bridge_port)
	_capture_current_screen.call_deferred()


func _process(_delta: float) -> void:
	gua.update(current_screen)


func _resolve_gua_bridge_port() -> int:
	var configured_port := OS.get_environment("GUA_BRIDGE_PORT")
	if configured_port.is_valid_int():
		var parsed_port := configured_port.to_int()
		if parsed_port > 0 and parsed_port <= 65535:
			return parsed_port
	return 8765


func _update_responsive_layout() -> void:
	var viewport_size := get_viewport_rect().size
	var uniform_scale := minf(
		viewport_size.x / DESIGN_SIZE.x,
		viewport_size.y / DESIGN_SIZE.y
	)
	design_root.scale = Vector2.ONE * uniform_scale
	design_root.position = (viewport_size - DESIGN_SIZE * uniform_scale) * 0.5
	if gua != null:
		_capture_current_screen.call_deferred()


func _show_page_one() -> void:
	loading_generation += 1
	current_screen = "page1"
	page_one.show()
	page_two.hide()
	exit_confirmation.hide()
	if gua != null:
		gua.attach(page_one)
		_capture_current_screen.call_deferred()


func _show_page_two() -> void:
	loading_generation += 1
	var generation := loading_generation
	current_screen = "page2"
	page_one.hide()
	page_two.show()
	exit_confirmation.hide()
	loading_label.show()
	back_button.disabled = true
	gua.attach(page_two)
	_capture_current_screen.call_deferred()
	_finish_loading_after_delay(generation)


func _finish_loading_after_delay(generation: int) -> void:
	await get_tree().create_timer(LOADING_DURATION_SECONDS).timeout
	if generation != loading_generation or current_screen != "page2":
		return
	loading_label.hide()
	back_button.disabled = false
	print("Loading finished: Back enabled")
	_capture_current_screen.call_deferred()


func _show_exit_confirmation() -> void:
	current_screen = "exit_confirmation"
	exit_confirmation.show()
	gua.attach(exit_confirmation)
	_capture_current_screen.call_deferred()


func _capture_current_screen() -> void:
	# Scene visibility changes can reach the render thread one frame after the
	# semantic tree changes. Wait for two complete frames so Gua never stores a
	# partially redrawn transition as its latest screenshot.
	await get_tree().process_frame
	await get_tree().process_frame
	await RenderingServer.frame_post_draw
	var result := gua.capture_viewport_screenshot()
	if not result.get("ok", false):
		push_warning("Gua screenshot capture failed: %s" % result.get("error", "unknown error"))


func _on_start_pressed() -> void:
	print("Start pressed: opening page2")
	_show_page_two()


func _on_end_pressed() -> void:
	print("End pressed: opening confirmation")
	_show_exit_confirmation()


func _on_cancel_exit_pressed() -> void:
	print("Exit canceled: returning to page1")
	_show_page_one()


func _on_confirm_exit_pressed() -> void:
	print("Exit confirmed: closing game")
	get_tree().quit()


func _on_back_pressed() -> void:
	if back_button.disabled:
		return
	print("Back pressed: returning to page1")
	_show_page_one()
