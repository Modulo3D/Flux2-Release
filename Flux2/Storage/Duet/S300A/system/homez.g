; homez.g
; called to home the Z axis

M98 P"/macros/coupler_unlock"	; Open Coupler

G91 							; Relative mode
G1 H2 Z3 F5000					; Lower the bed
G90								; back to absolute positioning

G1 X150 Y100 F50000				; Position the endstop above the bed centre

M400
if sensors.probes[0].value[0]>0
	M112

M558 F1000
G30
M400
if sensors.probes[0].value[0]>0
	M112
	
M558 F300
G30
M400
if sensors.probes[0].value[0]>0
	M112
