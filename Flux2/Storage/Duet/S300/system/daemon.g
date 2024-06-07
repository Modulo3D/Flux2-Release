if !exists(global.last_extrusion_spin)
	global last_extrusion_spin = 0
	
while global.run_daemon == true
	
	G4 S1
	
	if state.upTime - global.last_extrusion_spin > 30
		set global.last_extrusion_spin = state.upTime
		
		if global.queue_pos > -1
			var current_spin = "0:/gcodes/queue/inner/spin.g" 
			M98 P{var.current_spin}