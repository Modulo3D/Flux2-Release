; tfree0.g
; called when tool 0 is freed

; check tool presence
M400
M577 P0 S0

;Turn off
M104 S0

;Drop the bed
G91
G1 Z3 F1000
G90

;Purge nozzle
;M98 P"purge.g"

;Set tool limits
M98 P"/macros/magazine_limits"

;Move In
G53 G1 X{global.x_magazine_pos_2} Y150 F50000
G53 G1 Y200 F50000
G53 G1 Y{global.y_magazine_pos_2} F5000

;Open Coupler
M98 P"/macros/coupler_unlock"

; check no tool
M400
M577 P0 S1

;fan off
M106 P6 S0

;Move Out
G53 G1 Y200 F10000
G53 G1 Y175 F50000

;Set tool limits
M98 P"/macros/no_tool_limits"
