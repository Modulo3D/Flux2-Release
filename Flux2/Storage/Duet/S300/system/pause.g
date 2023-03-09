G91 					; use relative positioning
G1 Z5 F5000				; lift Z 5mm
G90 					; back to absolute positioning
G1 X-10 Y200 F50000		; move out the way.

; turn off heating
G10 P0 S0 R0
G10 P1 S0 R0
G10 P2 S0 R0
G10 P3 S0 R0

; deselect tool
T-1