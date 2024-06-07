M98 P"/macros/no_limits"
M104 S0 						; turn off heater
G91
G1 Z4 F2000 					; drop the bed
G90
G53 G1 U530 V-91.5 F15000	 		; move in
G53 G1 U704 V-91.5 F15000 	    ; park 
M98 P"/macros/park_limits"