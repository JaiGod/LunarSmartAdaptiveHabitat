extends SceneTree
var h: HTTPRequest
func _init():
	h = HTTPRequest.new(); root.add_child(h)
	h.request_completed.connect(func(r, c, hd, b): print("RESULT ", r, " code ", c, " body ", b.get_string_from_utf8().substr(0, 80)); quit())
	await process_frame
	print("request err ", h.request("http://127.0.0.1:8765/status"))
	await create_timer(5.0).timeout
	print("timeout, status ", h.get_http_client_status()); quit()
