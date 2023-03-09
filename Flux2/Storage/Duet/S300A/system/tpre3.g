; tpre0.g
; called before tool 0 is selected

; Reset tool offset
G10 P3 X0 Y0 Z0

; check no tool
M400
M577 P0 S1

;Set tool limits
M98 P"/macros/magazine_limits"

;Unlock Coupler
M98 P"/macros/coupler_unlock"

;Move to location
G1 X{global.x_magazine_pos_3} Y200 F50000

;Move in
G1 Y215 F50000

;Collect
G1 Y{global.y_magazine_pos_3} F10000

;Close Coupler
M98 P"/macros/coupler_lock"

; check tool presence
M400
M577 P0 S0

;WARNING! WARNING! WARNING! WARNING! WARNING! WARNING! WARNING! WARNING! WARNING! WARNING! WARNING! WARNING!
;if you are using non-standard length hotends ensure the bed is lowered enough BEFORE undocking the tool!
G91
G1 Z10 F1000
G90

;Move Out
G53 G1 Y150 F5000
