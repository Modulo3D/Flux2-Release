M98 P"/macros/work_limits_0"
; Set tool offset
G10 P0 X{-(global.x_home_offset_0 + global.x_user_offset_0 + global.x_probe_offset_0)} 
G10 P0 Y{-(global.y_home_offset_0 + global.y_user_offset_0 + global.y_probe_offset_0)} 
G10 P0 Z{-(global.z_probe_correction + global.z_bed_height + global.z_user_offset_0 + global.z_probe_offset_0)}
; M567 P0 E1:{global.extruder_mixing_0 == 0 ? 1 : 0}:{global.extruder_mixing_0 == 0 ? 1 : 0}