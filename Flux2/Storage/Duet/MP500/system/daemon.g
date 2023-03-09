var last_extrusion_spin = 0
while global.run_daemon == true

	; dwell for 1 second
	G4 S1	

	;spin extrusions
	if state.upTime - var.last_extrusion_spin > 30
		set var.last_extrusion_spin = state.upTime
		M98 P"/macros/job/spin.g"

	; locks control
	if (!exists(global.chamber_lock_timer))
		global chamber_lock_timer = 0
	if (!exists(global.spools_lock_timer))
		global spools_lock_timer = 0

	if state.gpOut[0].pwm > 0 && global.chamber_lock_timer == 0
		set global.chamber_lock_timer = state.upTime
	if state.gpOut[1].pwm > 0 && global.spools_lock_timer == 0
		set global.spools_lock_timer = state.upTime

	if state.upTime > global.chamber_lock_timer + 4
		set global.chamber_lock_timer = 0
		M42 P0 S0
	if state.upTime > global.spools_lock_timer + 4
		set global.spools_lock_timer = 0
		M42 P1 S0
		
	; chamber fan
	if heat.heaters[1].active > 0 || heat.heaters[1].current > 50
		M106 P0 S1.0
	if heat.heaters[1].active == 0 && heat.heaters[1].current < 45
		M106 P0 S0.0
			
	; humidity control
	M42 P5 S1
	if sensors.gpIn[1].value == 1
		if sensors.analog[9].lastReading > 10.0 || sensors.analog[10].lastReading > 10.0 || sensors.analog[11].lastReading > 10.0 || sensors.analog[12].lastReading > 10.0
			M42 P6 S1
	if (sensors.analog[9].lastReading < 8.0 && sensors.analog[10].lastReading < 8.0 && sensors.analog[11].lastReading < 8.0 && sensors.analog[12].lastReading < 8.0) || sensors.gpIn[1].value == 0
		M42 P6 S0