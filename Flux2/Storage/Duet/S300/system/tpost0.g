; tpost0.g
; called after tool 0 has been selected

M106 R1	; restore print cooling fan speed

;Set tool limits
M98 P"/macros/tool_limits"

; Set tool offset
G10 P0 X{-(global.x_home_offset_0 + global.x_user_offset_0 + global.x_probe_offset_0)} 
G10 P0 Y{-(global.y_home_offset_0 + global.y_user_offset_0 + global.y_probe_offset_0)} 
G10 P0 Z{-(global.z_user_offset_0 + global.z_probe_offset_0)}