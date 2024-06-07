; homex.g
; called to home the x axis
G91 							; use relative positioning
G1 H2 X0.5 Y-0.5 F10000			; energise motors to ensure they are not stalled
M400 							; make sure everything has stopped before we change the motor currents
M913 X20 Y20 					; drop motor currents to 25%
G1 H2 Z3 F5000					; lift Z 3mm

G1 H1 X400 F3000 				; move right 400mm, stopping at the endstop
M400
if !sensors.endstops[0].triggered
	M112

G1 X-5 F2000 					; move away from end
M400
if sensors.endstops[0].triggered
	M112
	
G1 H1 X10 F300 					; bump
M400
if !sensors.endstops[0].triggered
	M112

G1 X-2 F2000 					; move away from end
G1 H2 Z-3 F1200					; lower Z
G90 							; back to absolute positioning

M400 							; make sure everything has stopped before we reset the motor currents
M913 X100 Y100 					; motor currents back to 100%
