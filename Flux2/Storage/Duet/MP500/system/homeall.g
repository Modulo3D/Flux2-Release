M98 P"/macros/no_limits"

; set tool
T-1 P0

; home Z
G91 					; use relative position
G1 H1 Z700 F1000    	; move stopping at the endstop
M400
if !sensors.endstops[2].triggered
	M99	
G1 Z-2 F1000 			; move away from end
M400
if sensors.endstops[2].triggered
	M99	
G1 H1 Z5 F200			; bump
M400
if !sensors.endstops[2].triggered
	M99	
G90 					; back to absolute positioning
M400 					; wait

; home Y
G91 					; use relative position
G1 H1 Y-800 F1500    	; move stopping at the endstop
M400
if !sensors.endstops[1].triggered
	M99	
G1 Y2 F1000 			; move away from end
M400
if sensors.endstops[1].triggered
	M99	
G1 H1 Y-5 F200			; bump
M400
if !sensors.endstops[1].triggered
	M99	
G90 					; back to absolute positioning
M400 					; wait

; home X
G91 					; use relative position
G1 H1 X-1000 F1500    	; move stopping at the endstop
M400
if !sensors.endstops[0].triggered
	M99	
G1 X2 F1000 			; move away from end
M400					
if sensors.endstops[0].triggered
	M99	
G1 H1 X-5 F200			; bump
M400
if !sensors.endstops[0].triggered
	M99	
G90 					; back to absolute positioning
M400 					; wait

; home V
G91 					; use relative position
G1 H1 V-800 F1500    	; move stopping at the endstop
M400	
if !sensors.endstops[4].triggered
	M99	
G1 V2 F1000 			; move away from end
M400	
if sensors.endstops[4].triggered
	M99	
G1 H1 V-5 F200			; bump
M400	
if !sensors.endstops[4].triggered
	M99	
G90 					; back to absolute positioning
M400 					; wait

; home U
G91 					; use relative position
G1 H1 U1000 F1500    	; move stopping at the endstop
M400	
if !sensors.endstops[3].triggered
	M99	
G1 U-2 F1000 			; move away from end
M400	
if sensors.endstops[3].triggered
	M99	
G1 H1 U5 F200			; bump
M400	
if !sensors.endstops[3].triggered
	M99	
G90 					; back to absolute positioning
M400 					; wait

M98 P"/macros/park_limits"