; tpost1.g
; called after tool 1 has been selected

M106 R1	; restore print cooling fan speed

;Set tool limits
M98 P"/macros/tool_limits"

; Set tool offset
G10 P1 X{-(global.x_home_offset_1 + global.x_user_offset_1 + global.x_probe_offset_1)} 
G10 P1 Y{-(global.y_home_offset_1 + global.y_user_offset_1 + global.y_probe_offset_1)} 
G10 P1 Z{-(global.z_user_offset_1 + global.z_probe_offset_1)}